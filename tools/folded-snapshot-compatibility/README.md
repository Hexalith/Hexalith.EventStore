# Story 6.2 package compatibility check

Run `bash tools/folded-snapshot-compatibility/run.sh` from the repository root.
The check compiles and runs an old consumer against the published 3.119.0
packages, then packs the current source under a unique temporary version. A
new packaged consumer loads the old binary against those current packages and
checks positional constructor/deconstruction calls, observes the invoker's
additive default delegation and the old processor call under an enrolled
capture profile. It parses the old result with the current bounded parser and
round-trips a new result through the bounded writer and parser. The new package
reads old request and version-0 snapshot files. The published old package reads
new capture request, optional wire result, and version-1 folded snapshot files.

The script uses a temporary directory and leaves the working tree untouched.
Set `FOLDED_SNAPSHOT_OLD_VERSION` to test another published pre-6.2 version.
