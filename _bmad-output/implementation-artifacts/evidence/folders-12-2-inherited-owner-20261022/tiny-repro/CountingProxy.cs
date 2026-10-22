using System.Reflection;
using Hexalith.EventStore.DomainService;

public class CountingProxy : DispatchProxy
{
 public IAsyncDomainSharedProjectionStreamingRebuildHandler Inner { get; set; } = null!;
 public int FoldCalls { get; private set; }
 protected override object? Invoke(MethodInfo? targetMethod,object?[]? args)
 {
  if(targetMethod!.Name=="FoldCatchUpAsync") FoldCalls++;
  return targetMethod.Invoke(Inner,args);
 }
}
