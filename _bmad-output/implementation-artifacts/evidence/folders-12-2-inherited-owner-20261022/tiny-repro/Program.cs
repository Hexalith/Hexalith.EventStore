using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Server;
using Microsoft.Extensions.DependencyInjection;

var rootAssembly = Assembly.Load("Hexalith.Folders.Tests");
foreach(var assembly in new[]{rootAssembly,typeof(SharedProjectionEpochCoordinator).Assembly,typeof(ProjectionRequest).Assembly,typeof(DomainProjectionDispatcher).Assembly,typeof(FolderCreated).Assembly,typeof(OperationalStatusProjectionHandler).Assembly})
 Console.WriteLine("MODULE "+JsonSerializer.Serialize(new{name=assembly.GetName().Name,path=assembly.Location,mvid=assembly.ManifestModule.ModuleVersionId,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location)))}));

var storeType = rootAssembly.GetType("Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestStore")!;
var fixture = rootAssembly.GetType("Hexalith.Folders.Tests.Projections.OperationalStatus.NamedOperationalStatusRebuildFixture")!;
var rebuild = fixture.GetMethod("RebuildAsync")!;
foreach (bool evolve in new[] { false, true }) {
 object store = Activator.CreateInstance(storeType)!;
 var services = new ServiceCollection();
 services.AddSingleton((IReadModelStore)store); services.AddSingleton((IReadModelBatchStore)store);
 services.AddSingleton((IReadModelBatchStagingStore)store); services.AddSingleton((IReadModelPendingBatchRecoveryStore)store);
 services.AddSingleton(new SharedProjectionEpochCoordinator((IReadModelStore)store,(IReadModelBatchStore)store));
 services.AddSingleton<OperationalStatusProjectionWriter>();
 CountingProxy? counter=null;
 services.AddSingleton<IAsyncDomainProjectionHandler>(services => {
  var real=new OperationalStatusProjectionHandler(services.GetRequiredService<OperationalStatusProjectionWriter>());
  var proxy=DispatchProxy.Create<IAsyncDomainSharedProjectionStreamingRebuildHandler,CountingProxy>();
  counter=(CountingProxy)(object)proxy;counter.Inner=real;return proxy;
 });
 if(evolve) services.AddSingleton(new EventPayloadEvolutionRegistry(new[]{typeof(FolderCreated)},[]));
 using var provider = services.BuildServiceProvider();
 var options = new ProjectionDispatchOptions { MaxRebuildEventCount = 40000 };
 var binding = new DomainProjectionIdentityOptions { AppId="folders",ServiceVersion="test" };
 string catalog = ProjectionRouteCatalogFingerprint.Compute("folders","test",[new("folders","operational-status")],[new("folders","operational-status")]);
 var now=DateTimeOffset.UtcNow;
 ProjectionEventDto Source(string folder,long position) => new(typeof(FolderCreated).FullName!, JsonSerializer.SerializeToUtf8Bytes(new FolderCreated("tenant-a","tenant-a",folder,"Capacity metadata",null,null,[],FolderLifecycleState.Active,FolderRepositoryBindingState.Unbound,"capacity-principal","capacity-correlation","capacity-task","capacity-create-key","capacity-fingerprint",now)),"json",1,now,"capacity-correlation","capacity-message-1",GlobalPosition:position){MetadataVersion=1};
 async Task<ProjectionDispatchOutcome> Deliver(string folder,ProjectionEventDto source) {
  var request = new ProjectionDispatchRequest(new("tenant-a","folders",folder,[source]),["operational-status"],"diagnostic-"+Guid.NewGuid().ToString("N"),catalog);
  request = JsonSerializer.Deserialize<ProjectionDispatchRequest>(JsonSerializer.SerializeToUtf8Bytes(request,JsonSerializerOptions.Web),JsonSerializerOptions.Web)!;
  int before=counter?.FoldCalls ?? 0;
  var outcome=(await DomainProjectionDispatcher.DispatchAsync(provider,request,options,binding,CancellationToken.None)).Outcomes.Single();
  Console.WriteLine("FOLD_CALLBACK "+evolve+" "+folder+" delta="+((counter?.FoldCalls ?? 0)-before));
  return outcome;
 }
 async Task Rebuild(string owner,string operation,ProjectionEventDto source, (string FolderId,IReadOnlyList<OperationalStatusEvent> Events)[]? others=null){
  var task=(Task<ReadModelBatchResult>)rebuild.Invoke(null,[store,"tenant-a",owner,operation,new[]{OperationalStatusEventReader.ReadStored(source)},source.GlobalPosition,CancellationToken.None,null,true,null,others])!;
  Console.WriteLine("REBUILD "+evolve+" "+owner+" "+JsonSerializer.Serialize(await task));
 }
 var original=Source("folder-a",2);var other=Source("folder-b",3);
 Console.WriteLine("PRIMER "+evolve+" "+JsonSerializer.Serialize(await Deliver("primer",Source("primer",1))));
 await Rebuild("folder-a","original",original,[("folder-b",new[]{OperationalStatusEventReader.ReadStored(other)})]);
 var coordinator=provider.GetRequiredService<SharedProjectionEpochCoordinator>();var scope=OperationalStatusGeneration.Scope("tenant-a");
 var canonical=JsonSerializer.SerializeToUtf8Bytes(new ProjectionRequest("tenant-a","folders","folder-a",[original]),JsonSerializerOptions.Web);
 var delivery=new SharedProjectionDelivery("folder-a",2,canonical,JsonSerializer.SerializeToUtf8Bytes(2L),OwnerSequenceNumber:1);
 async Task Show(string phase){
  var checkpoint=await coordinator.GetCheckpointAsync(scope,"folder-a");
  var epoch=(await ((IReadModelStore)store).GetAsync<JsonObject>("statestore","shared-epoch:"+scope.ComputeHash())).Value;
  var lease=await coordinator.RegisterWriterAsync(scope,OperationalStatusGeneration.WriterId);
  Console.WriteLine("DIRECT_JOURNAL "+phase+" "+evolve+" "+JsonSerializer.Serialize(new {canonicalSha256=Convert.ToHexString(SHA256.HashData(canonical)),checkpoint,epoch,journal=await coordinator.JournalAsync(scope,lease,delivery)}));
 }
 await Show("before");
 Console.WriteLine("BEFORE_INHERIT "+evolve+" "+JsonSerializer.Serialize(await Deliver("folder-a",original)));
 await Rebuild("folder-b","independent",other);
 await Show("after");
 var inheritedOutcome = await Deliver("folder-a",original);
 Console.WriteLine("AFTER_INHERIT "+evolve+" "+JsonSerializer.Serialize(inheritedOutcome));
 if (inheritedOutcome.Status != ProjectionDispatchStatus.AlreadyCompleted) throw new InvalidOperationException("Unchanged inherited owner replay must remain AlreadyCompleted.");
}
