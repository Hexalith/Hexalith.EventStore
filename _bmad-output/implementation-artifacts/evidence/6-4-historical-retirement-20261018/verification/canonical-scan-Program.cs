using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
string root=args[0];
string assemblyPath=args[3];
AssemblyDependencyResolver resolver=new(assemblyPath);
AssemblyLoadContext.Default.Resolving += (context,name) => resolver.ResolveAssemblyToPath(name) is string path ? context.LoadFromAssemblyPath(path) : null;
var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
Type type=assembly.GetType("Hexalith.EventStore.Server.Tests.Security.SecretsProtectionTests")!;
var scan=type.GetMethod("ScanTrackedFile",BindingFlags.Static|BindingFlags.NonPublic)!;
var paths=File.ReadAllText(args[1]).Split('\0',StringSplitOptions.RemoveEmptyEntries);
List<object> rows=[];
foreach(string path in paths) {
 try { string[] violations=((IEnumerable<string>)scan.Invoke(null,[path,root])!).ToArray();
 if(violations.Length>0) rows.Add(new {path,violations});
 } catch(Exception ex) { rows.Add(new {path,error=ex.ToString()}); }
}
File.WriteAllText(args[2],JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true})+"\n");
Console.WriteLine($"filesWithViolations={rows.Count}");
