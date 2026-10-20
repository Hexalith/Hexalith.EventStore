#!/usr/bin/env python3
"""Run one explicit P1R verification command with private Docker and process namespaces."""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from p1r_isolation import main


if __name__ == "__main__":
    raise SystemExit(main())
