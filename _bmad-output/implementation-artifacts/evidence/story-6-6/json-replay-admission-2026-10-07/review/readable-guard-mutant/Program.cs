using System.Reflection;
using System.Text.Json;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Events;
long Number(LegacyCommandReplayInput owner,string name)=>(long)typeof(LegacyCommandReplayInput).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
const int typedBytes=50*1024*1024;
const int jsonPadding=8*1024*1024;
byte[] payload=new byte[typedBytes];payload.AsSpan().Fill((byte)' ');"{\"amount\":1}"u8.CopyTo(payload);
var metadata=new EventMetadata("message","aggregate","mutation","tenant","domain",1,1,DateTimeOffset.UnixEpoch,"correlation","cause","user","1","Event",1,"json");
var typed=new EventEnvelope(metadata,payload,null);
using JsonDocument json=JsonDocument.Parse("{\"eventTypeName\":\"Event\",\"amount\":1,\"ignored\":\""+new string('a',jsonPadding)+"\"}");
var measured=LegacyCommandReplayJsonAdmission.Measure(json.RootElement,default,eventRoot:true);
using var owner=new LegacyCommandReplayInput(default);
long allReadable=typedBytes+2*measured.ReadableBytes;
Console.WriteLine($"Fixture readable={allReadable}, cap={64L*1024*1024}; JSON entry readable={measured.ReadableBytes}, document={measured.DocumentBytes}");
try {
var captured=owner.CaptureEvents(new object[]{typed,json.RootElement,json.RootElement});
Console.WriteLine($"Guard assertion FAILED: admitted {captured.Count} entries, readable-counter={Number(owner,"_readableBytes")}, accounted={Number(owner,"_accountedBytes")}");
Environment.ExitCode=1;
} catch(InvalidOperationException error) when(error.Message.StartsWith("LegacyArrayLimit:")) {
long wouldAccount=Number(owner,"_accountedBytes")+measured.DocumentBytes+2*measured.ReadableBytes;
if(wouldAccount>=256L*1024*1024)throw new Exception("Fixture crosses accounted cap too");
Console.WriteLine($"Guard assertion PASSED: {error.Message}; readable-counter={Number(owner,"_readableBytes")}, accounted-if-all-admitted={wouldAccount}, cap={256L*1024*1024}");
}
