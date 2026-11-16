#!/usr/bin/env python3
"""Explicit fixed Story 5.3 validator; saved output is diagnostic, never authority."""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from phase_4_story_5_3 import main

if __name__ == "__main__":
    raise SystemExit(main())
