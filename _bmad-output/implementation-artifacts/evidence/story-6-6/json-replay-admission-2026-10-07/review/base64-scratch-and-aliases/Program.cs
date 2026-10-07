using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Client.Handlers;
void Capture(string json, JsonDocumentOptions options = default) {
using JsonDocument source = JsonDocument.Parse(json,options);
using var owner = new LegacyCommandReplayInput(default);
try { var captured = owner.CaptureJson(source.RootElement, true); Console.WriteLine("ACCEPT: "+json[..Math.Min(90,json.Length)]); }
catch(Exception ex) {Console.WriteLine(ex.GetType().Name+": "+ex.Message+" input="+json[..Math.Min(90,json.Length)]); }
}
Capture("[{\"eventTypeName\":\"Event\",\"amount\":1,\"Payload\":\"!\"}]");
Capture("[{\"eventTypeName\":\"Event\",\"payload\":{\"amount\":1}},]",new JsonDocumentOptions {AllowTrailingCommas=true});
Capture("[/*comment*/{\"eventTypeName\":\"Event\",\"payload\":{\"amount\":1}}]",new JsonDocumentOptions {CommentHandling=JsonCommentHandling.Skip});
const int whitespaceCount=8*1024*1024;
string whitespaceJson="[{\"eventTypeName\":\"Event\",\"payload\":\""+string.Concat(Enumerable.Repeat("\\n",whitespaceCount))+"\"}]";
using JsonDocument original=JsonDocument.Parse(whitespaceJson);
using var whitespaceOwner=new LegacyCommandReplayInput(default);
var measureWhitespace=LegacyCommandReplayJsonAdmission.Measure(original.RootElement,default);
var whitespaceAdmitted=whitespaceOwner.CaptureJson(original.RootElement,true);
long beforeAllocation=GC.GetAllocatedBytesForCurrentThread();
byte[] whitespaceDecoded=whitespaceOwner.DecodePayload(whitespaceAdmitted[0].GetProperty("payload"));
long afterAllocation=GC.GetAllocatedBytesForCurrentThread();
Console.WriteLine($"Whitespace decode: readable={measureWhitespace.ReadableBytes}, documents={measureWhitespace.DocumentBytes}, decode allocations={afterAllocation-beforeAllocation}, decoded={whitespaceDecoded.Length}");
var metadata = new Hexalith.EventStore.Contracts.Events.EventMetadata("message","aggregate","aggregateType","tenant","domain",1,0,DateTimeOffset.UnixEpoch,"correlation","causation","user","1","First",1,"json");
string metadataJson=JsonSerializer.Serialize(metadata,new JsonSerializerOptions(JsonSerializerDefaults.Web));
metadataJson=metadataJson.Replace("\"eventTypeName\":\"First\"","\"eventTypeName\":\"First\",\"EventTypeName\":\"Second\"",StringComparison.Ordinal);
Capture("{\"currentSequence\":1,\"events\":[{\"metadata\":"+metadataJson+",\"payload\":\"e30=\"}],\"snapshotState\":null}");
