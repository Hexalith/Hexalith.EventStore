using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Commands;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Client.Handlers;
var table = DomainProcessorStateRehydrator.DiscoverApplyMethods(typeof(ProbeState));
void Probe(string label, string json, JsonDocumentOptions options = default)
{
    using var doc = JsonDocument.Parse(json, options);
    try { Console.WriteLine($"{label}: Total={DomainProcessorStateRehydrator.RehydrateState<ProbeState>(doc.RootElement, table)?.Total}"); }
    catch (Exception e) { Console.WriteLine($"{label}: {e.GetType().Name}: {e.Message}"); }
}
Probe("uppercase payload", """[{"eventTypeName":"ProbeEvent","Payload":"!","amount":1}]""");
Probe("uppercase payload without gate control", """[{"eventTypeName":"ProbeEvent","body":"!","amount":1}]""");
Probe("trailing comma", """{"Total":7,}""", new JsonDocumentOptions { AllowTrailingCommas=true });
Probe("comment", """{"Total":7/* hi */}""", new JsonDocumentOptions { CommentHandling=JsonCommentHandling.Skip });
foreach (string json in new[]{ "\"\u0059WJj\"", "\"\\u0059WJj\"", "\"YQ==\\n\"", "\" YQ==\\u0009\"", "\"\\u002f w==\"", "\"\\u0009\"" }) {
    using var doc = JsonDocument.Parse(json);
    try { Console.WriteLine($"base64 {json}: measured={LegacyCommandReplayJsonAdmission.DecodedBase64Length(doc.RootElement,CancellationToken.None)},actual={doc.RootElement.GetBytesFromBase64().LongLength}"); }
    catch(Exception e) {Console.WriteLine($"base64 {json}: {e.GetType().Name}");}
}

var metadata = new EventMetadata("message", "aggregate", "probe", "tenant", "domain", 1, 1,
    DateTimeOffset.UnixEpoch, "correlation", "cause", "user", "1", "ProbeEvent", 1, "json");
var envelope = new EventEnvelope(metadata, "{\"amount\":1}"u8.ToArray(), null);
var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
string wrapper = JsonSerializer.Serialize(new DomainServiceCurrentState(null, new[]{ envelope }, 0, 1), web);
Probe("capitalized wrapper payload", wrapper.Replace("\"payload\":", "\"Payload\":"));
string escapedKey = string.Concat(Enumerable.Repeat("\\u006b", 100_000));
string rawKey = new('k', 100_000);
Probe("large raw ignored metadata name", wrapper.Replace("\"metadataVersion\":", "\"" + rawKey + "\":0,\"metadataVersion\":"));
string escapedWrapper = wrapper.Replace("\"metadataVersion\":", "\"" + escapedKey + "\":0,\"metadataVersion\":");
using (var document = JsonDocument.Parse(escapedWrapper)) {
    var eventJson = document.RootElement.GetProperty("events")[0];
    Console.WriteLine($"escaped-name default-Web event size: {JsonSerializer.SerializeToUtf8Bytes(eventJson, web).Length}");
}
Probe("large escaped ignored metadata name", escapedWrapper);
int compared=0;
var random = new Random(94013);
for(int n=0; n<20000; n++) {
    string input;
    if(n%2==0) {
        var bytes = new byte[random.Next(0,100)]; random.NextBytes(bytes); input=Convert.ToBase64String(bytes);
        int spaces=random.Next(0,12);
        for(int i=0;i<spaces;i++) input=input.Insert(random.Next(input.Length+1), new[]{" ","\t","\n","\r"}[random.Next(4)]);
    } else {
        const string alphabet="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/= \t\r\n\f\u00a0";
        input=new string(Enumerable.Range(0,random.Next(0,60)).Select(_=>alphabet[random.Next(alphabet.Length)]).ToArray());
    }
    using var document = JsonDocument.Parse(JsonSerializer.Serialize(input));
    byte[]? expected=null; Type? expectedError=null;
    try {expected=document.RootElement.GetBytesFromBase64();} catch(Exception e) {expectedError=e.GetType();}
    try {
        int measured=(int)LegacyCommandReplayJsonAdmission.DecodedBase64Length(document.RootElement, CancellationToken.None);
        var actual=new byte[measured]; LegacyCommandReplayJsonAdmission.DecodeBase64(document.RootElement, actual, CancellationToken.None);
        if(expectedError != null || expected == null || !expected.SequenceEqual(actual)) throw new Exception($"Base64 differential mismatch {JsonSerializer.Serialize(input)}");
    } catch(Exception e) {if(e.GetType()!=expectedError) throw;}
    compared++;
}
Console.WriteLine($"base64 framework differential cases: {compared}, mismatches: 0");
