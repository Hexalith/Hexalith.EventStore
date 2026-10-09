# Interrupted privacy discovery trial

Invocation `627a9059adee4dcfa35d15afaabd9346` was interrupted by exact PID 10307/start ticks10811529 after a read-only audit found overly broad Docker inspection output capture. Exit2 and the partial snapshot case are retained. Both cleanup receipts passed7/7, no owned resources remained and shared containers were preserved.

Original invocation artifacts remain private outside the repository with directory0700/file0600. This newly derived directory excludes all unfiltered Docker command output and reduces runtime image observations to preservation fields. Original hashes are recorded without sensitive bytes. Derived receipt/outer hashes can differ from their original bindings and confer no qualification. Final fresh observation and execution select safe Docker fields before capture.
