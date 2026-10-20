import datetime,json,subprocess,sys
from pathlib import Path
p=Path(sys.argv[1]);assert not p.exists()
def stamp():return datetime.datetime.now(datetime.timezone.utc).isoformat()
r={"argv":sys.argv[2:],"cwd":str(Path.cwd()),"started_utc":stamp(),"output_log":str(p.with_suffix(".log").resolve())}
p.write_text(json.dumps(r,indent=2)+"\n")
with Path(r["output_log"]).open("wb") as h:code=subprocess.run(r["argv"],stdout=h,stderr=subprocess.STDOUT).returncode
r.update(exit_code=code,finished_utc=stamp());p.write_text(json.dumps(r,indent=2)+"\n");print(json.dumps(r));raise SystemExit(code)
