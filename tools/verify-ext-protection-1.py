#!/usr/bin/env python3
"""EventStore-owned qualification entry point; Source mode never executes Live."""
from ext_protection_1 import main

if __name__ == '__main__':
    raise SystemExit(main())
