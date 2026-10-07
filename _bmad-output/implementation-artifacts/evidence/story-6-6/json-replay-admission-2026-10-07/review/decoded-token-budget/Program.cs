using System.Reflection;
using System.Text.Json;
using Hexalith.EventStore.Client.Handlers;
byte[] payload = new byte[2 * 16 * 1024 * 1024 + 1];
payload[0] = (byte)'[';
for (int i=1; i<payload.Length-1; i+=2) { payload[i]=(byte)'0'; payload[i+1]=(byte)','; }
payload[^1]=(byte)']';
payload[^2]=(byte)'0';
string outer="[{\"eventTypeName\":\"Event\",\"payload\":\""+Convert.ToBase64String(payload)+"\"}]";
using JsonDocument source=JsonDocument.Parse(outer);
var measure=LegacyCommandReplayJsonAdmission.Measure(source.RootElement,default);
using var owner=new LegacyCommandReplayInput(default);
var admitted=owner.CaptureJson(source.RootElement,true);
byte[] decoded=owner.DecodePayload(admitted[0].GetProperty("payload"));
using JsonDocument decodedDoc=JsonDocument.Parse(decoded);
object table=typeof(JsonDocument).GetField("_parsedData",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(decodedDoc)!;
byte[] buffer=(byte[])table.GetType().GetField("_data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(table)!;
Console.WriteLine($"decoded={decoded.Length}; admitted_charge={measure.DocumentBytes+8192+2*measure.ReadableBytes}; decoded_token_capacity={buffer.Length}; combined={measure.DocumentBytes+8192+2*measure.ReadableBytes+buffer.Length}; limit={256L*1024*1024}");
