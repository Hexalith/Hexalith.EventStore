"""Disposable nested process tree; never a Dapr acceptance substitute."""
import os
from pathlib import Path
import subprocess
import sys
import time
runtime = Path(os.environ['LIFECYCLE_CONTROL_DIR'])
child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(300)'])
observer = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(300)'])
while not (runtime / 'crash-trigger').exists():
    time.sleep(0.1)
os._exit(7)
