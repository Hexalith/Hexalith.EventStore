#!/usr/bin/env bash
set -euo pipefail
python3 -m unittest discover -s tools/tests -p test_catalog_package_preparation.py
python3 tools/prepare-catalog-packages.py
