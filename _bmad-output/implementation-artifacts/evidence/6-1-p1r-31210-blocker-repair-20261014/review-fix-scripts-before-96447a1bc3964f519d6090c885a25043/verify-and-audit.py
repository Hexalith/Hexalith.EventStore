#!/usr/bin/env python3
"""Keep repair measurements and independent audit in the same frozen private toolchain."""
from pathlib import Path
import subprocess
import sys
root=Path(__file__).resolve().parent
verification=subprocess.run([sys.executable,str(root/'verify-repair.py')],check=False)
audit=subprocess.run([sys.executable,str(root/'independent-repair-audit.py')],check=False)
raise SystemExit(0 if verification.returncode==audit.returncode==0 else 2)
