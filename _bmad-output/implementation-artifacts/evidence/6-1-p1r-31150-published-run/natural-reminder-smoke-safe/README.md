# Natural Reminder smoke

All three selected cases used actual candidate packages and Scheduler delivery. Positive restart and tenant cases measured sequence12 before stopping the writer, then natural sequence13 after restart through a read-only actor call. Stale-generation measured natural sequence13 before its revision change. Every first completed observation preceded all manual callback injection. Counts were 42/42, 41/41 and 42/42; cleanup 7/7, no executor errors.

These smoke invocations are development evidence, separately bound from the complete fresh final run.
