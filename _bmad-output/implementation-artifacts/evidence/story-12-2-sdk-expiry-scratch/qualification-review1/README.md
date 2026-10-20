Current EventStore SDK source qualification, pending parent review. The owner spec stays in-progress.

Read qualification.json and commands.json for exact scope, commands, counts, source/binary binding and limits.
Full Client: 1,736 passed, two existing optional skips. Full DomainService: 1,005 passed, zero skips.
Live: 36 outer cases and four child worker invocations passed with zero skips.
The two pending owner-scratch process cases use exited owning workers and distinct recovery PIDs.

The process extension changes test helpers only; binary-group-differences.json and pre-process-extension-test-source
preserve the exact earlier live group. Production sources and affected Client/DomainService/Testing assemblies did not drift; Server/Gateway rebuild outputs are recorded as distinct binary groups.
The PDB checks cover every scoped project C# source with zero mismatches or missing documents.
Diagnostic folders retain the actual initial failures and repeats; diagnostic-failures.json records their assertions.
Generic abandoned streamed-metadata terminal guards, forced Redis transaction limitations, existing optional skips,
and all unclaimed release/consumer/story gates remain explicit in qualification.json.

SHA256SUMS hashes every packet artifact except itself. Packet-local * -text preserves raw bytes across checkout.
The historical qualification-20261020 packet passes its original checksum manifest unchanged.
