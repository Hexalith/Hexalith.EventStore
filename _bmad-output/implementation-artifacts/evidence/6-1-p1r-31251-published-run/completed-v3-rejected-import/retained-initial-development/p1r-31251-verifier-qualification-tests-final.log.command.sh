python3 - <<'PY'
from pathlib import Path
p=Path('tools/p1r_isolation.py');s=p.read_text().replace('subprocess.check_output(["dotnet", "--version"], env=environment).decode().strip()', 'subprocess.check_output(["dotnet", "--version"], env=environment, cwd="/").decode().strip()');p.write_text(s)
PY
python3 -m unittest discover -s tools/tests -p 'test_p1r_published_qualification.py' > /tmp/p1r-31251-verifier-qualification-tests-final.log 2>&1