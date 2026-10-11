# Story 6.2 package compatibility check

Run `bash tools/folded-snapshot-compatibility/run.sh` from the repository root.
The check compiles and runs an old consumer against the published 3.119.0
packages, then packs the current source under a unique temporary version. A
new packaged consumer loads the old binary against those current packages and
checks positional constructor/deconstruction calls, the invoker's additive
default fallback, old processor handling, and old/new optional wire fields.
It also reads an old version-0 snapshot with the new package and reads a new
version-1 folded object with the old published package.

The script uses a temporary directory and leaves the working tree untouched.
Set `FOLDED_SNAPSHOT_OLD_VERSION` to test another published pre-6.2 version.
