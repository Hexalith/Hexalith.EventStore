namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Reads only the exact finite owner-private digest capture produced by the independently started consumer.</summary>
internal static class HistoricalSecurityCapture
{
    /// <summary>Bounded regular-file read; no command payload, proof or application secret is captured.</summary>
    internal const string ReadScript = "import pathlib,sys,stat,json; p=pathlib.Path(sys.argv[1]); s=p.lstat(); assert stat.S_ISREG(s.st_mode) and s.st_size<=262144 and stat.S_IMODE(s.st_mode)==0o600; rows=[json.loads(x) for x in p.read_text().splitlines()]; assert len(rows)==10 and all(x['owner']==sys.argv[2] and x['pid']==int(sys.argv[3]) for x in rows); print(json.dumps(rows,separators=(',',':')))";
}
