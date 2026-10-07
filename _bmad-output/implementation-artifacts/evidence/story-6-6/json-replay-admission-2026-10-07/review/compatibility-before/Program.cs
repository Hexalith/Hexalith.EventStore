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
