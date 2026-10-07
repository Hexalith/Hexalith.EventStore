using System.Reflection;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Serialization;
long Number(LegacyCommandReplayInput owner,string name)=>(long)typeof(LegacyCommandReplayInput).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
List<byte[]> Buffers(LegacyCommandReplayInput owner)=>(List<byte[]>)typeof(LegacyCommandReplayInput).GetField("_payloads",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
void Assert(bool condition,string explanation) { if(!condition)throw new Exception(explanation); }
EventMetadata Metadata()=>new("message","aggregate","mutation","tenant","domain",1,1,DateTimeOffset.UnixEpoch,"correlation","cause","user","1","Event",1,"json");
{
const int elementCount=16*1024*1024;
byte[] bytes=new byte[elementCount*2+1]; bytes[0]=(byte)'['; bytes[^1]=(byte)']';
for(int offset=1;offset<bytes.Length-1;offset+=2) {bytes[offset]=(byte)'0';if(offset+1<bytes.Length-1)bytes[offset+1]=(byte)',';}
using JsonDocument original=JsonDocument.Parse("[{\"eventTypeName\":\"Event\",\"payload\":\""+Convert.ToBase64String(bytes)+"\"}]");
using var owner=new LegacyCommandReplayInput(default);
JsonElement captured=owner.CaptureJson(original.RootElement,true);
byte[] decoded=owner.DecodePayload(captured[0].GetProperty("payload"));
long before=Number(owner,"_accountedBytes");
long allocatedBefore=GC.GetAllocatedBytesForCurrentThread();
try {using JsonDocument parsed=owner.ParsePayload(decoded);throw new Exception("Token-dense payload was accepted");}
catch(InvalidOperationException error) when(error.Message.StartsWith("LegacyArrayLimit:")) {Console.WriteLine($"Token-table guard FIXED: admitted={before}, after={Number(owner,"_accountedBytes")}, parse-attempt allocations={GC.GetAllocatedBytesForCurrentThread()-allocatedBefore}, result={error.Message}");}
owner.Dispose();Assert(decoded.All(value=>value==0),"Decoded token probe bytes not cleared");
Assert(original.RootElement.GetArrayLength()==1,"Caller source changed");
}
{
const int whitespaceCount=8*1024*1024;
using JsonDocument original=JsonDocument.Parse("[{\"eventTypeName\":\"Event\",\"payload\":\""+string.Concat(Enumerable.Repeat("\\n",whitespaceCount))+"\"}]");
using var owner=new LegacyCommandReplayInput(default);
JsonElement admitted=owner.CaptureJson(original.RootElement,true);
long before=GC.GetAllocatedBytesForCurrentThread();
byte[] decoded=owner.DecodePayload(admitted[0].GetProperty("payload"));
long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
Assert(decoded.Length==0 && allocated<16384,"Whitespace decode allocated unescape-sized scratch");
Console.WriteLine($"Whitespace scratch FIXED: decoded={decoded.Length}, decode allocations={allocated}");
}
{
using JsonDocument names=JsonDocument.Parse("{\""+new string('k',400000)+"\":0,\"\\u0045vEnTtYpEnAmE\":1}");
var enumerator=names.RootElement.EnumerateObject();enumerator.MoveNext();JsonProperty ignored=enumerator.Current;enumerator.MoveNext();JsonProperty escaped=enumerator.Current;
_ = LegacyCommandReplayJsonAdmission.NameMatches(ignored,"eventTypeName");
long before=GC.GetAllocatedBytesForCurrentThread();
for(int index=0;index<1000;index++)Assert(!LegacyCommandReplayJsonAdmission.NameMatches(ignored,"eventTypeName"),"Ignored name matched");
long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
Assert(allocated<16384 && LegacyCommandReplayJsonAdmission.NameMatches(escaped,"eventTypeName"),"Bounded name lookup failed");
Console.WriteLine($"Property-name decoding FIXED: 1000 ignored-name lookups allocations={allocated}; escaped mixed-case alias matched");
}
foreach(string outcome in new[]{"refusal","failure","cancellation"})
{
using JsonDocument source=JsonDocument.Parse("[{\"eventTypeName\":\"Event\",\"payload\":\"eyJhbW91bnQiOjF9\"}]");
using var cancellation=new CancellationTokenSource();
var owner=new LegacyCommandReplayInput(cancellation.Token);
JsonElement captured=owner.CaptureJson(source.RootElement,true);
byte[] decoded=owner.DecodePayload(captured[0].GetProperty("payload"));
JsonDocument parsed=owner.ParsePayload(decoded);
var retained=Buffers(owner).ToArray();
try {
if(outcome=="refusal") {using JsonDocument excessive=JsonDocument.Parse("[{\"eventTypeName\":\"Event\",\"SerializationFormat\":\""+new string('<',90000)+"\"}]");_ = owner.CaptureJson(excessive.RootElement,true);}
else if(outcome=="failure") {_ = owner.ParsePayload("{invalid"u8.ToArray());}
else {cancellation.Cancel();_ = owner.ParsePayload(decoded);}
throw new Exception("Expected operation failure");
} catch(Exception error) when(error is InvalidOperationException or JsonException or OperationCanceledException) {if(outcome=="cancellation")Assert(((OperationCanceledException)error).CancellationToken==cancellation.Token,"Wrong token");}
finally {owner.Dispose();}
Assert(retained.All(buffer=>buffer.All(value=>value==0)),"Retained bytes not cleared");
try{_ = captured.GetArrayLength();throw new Exception("Private document survived");}catch(ObjectDisposedException){}
try{_ = parsed.RootElement.GetRawText();throw new Exception("Payload document survived");}catch(ObjectDisposedException){}
Assert(source.RootElement.GetArrayLength()==1,"Caller source disposed");
Console.WriteLine($"Cleanup FIXED: {outcome}, retained buffers={retained.Length}, zeroed=true, private documents disposed, caller source readable");
}
{
const int typedBytes=17*1024*1024;
const int jsonPadding=24*1024*1024;
byte[] payload=new byte[typedBytes];payload.AsSpan().Fill((byte)' ');"{\"amount\":1}"u8.CopyTo(payload);
var typed=new EventEnvelope(Metadata(),payload,null);
using JsonDocument json=JsonDocument.Parse("{\"eventTypeName\":\"Event\",\"payload\":{\"amount\":1,\"ignored\":\""+new string('a',jsonPadding)+"\"}}");
var measured=LegacyCommandReplayJsonAdmission.Measure(json.RootElement,default,eventRoot:true);
using var owner=new LegacyCommandReplayInput(default);
try {_ = owner.CaptureEvents(new object[]{typed,json.RootElement,json.RootElement});throw new Exception("Aggregate readable guard missing");}
catch(InvalidOperationException error) when(error.Message.StartsWith("LegacyArrayLimit:"))
{
long accounted=Number(owner,"_accountedBytes");long hypothetical=accounted+measured.DocumentBytes+2*measured.ReadableBytes;
Assert(hypothetical<256L*1024*1024,"Second entry also crosses accounted ceiling");
Console.WriteLine($"Aggregate readable guard VERIFIED independently: readable-so-far={Number(owner,"_readableBytes")}, next-readable={measured.ReadableBytes}, accounted-if-next-admitted={hypothetical}, cap={256L*1024*1024}");
}
}
Console.WriteLine("All final linked-source checks passed.");
