# Interrupted scheduler coverage trial

Invocation `8776db77a0ab459fb2ac36b9848623ba` used the frozen intermediate source closure at `3759b37b490e45476af04301544d2190d2ca0161ccc03533d7c1beffa5daf0ef`. It completed all nine Release consumer builds, five lanes and part of snapshot-tail. A read-only pre-review audit found that manual Reminder callback injection did not independently prove natural scheduler completion. The exact invocation PID 45003/start ticks 10776186 received SIGINT. Exit status was 2, the partial case was retained, and both cleanup receipts passed all seven checks with no remaining owned resources and unchanged shared containers.

This trial is sealed historical development evidence and cannot qualify P1R. The corrected final invocation independently observes read-only actor sequence 13 before any callback injection.
