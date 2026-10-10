# P1R 3.119.0 published requalification evidence

The selected inputs and pending owner decisions are in
`observation-a8318299841d4bb2a0a6bb7d96918064/`. That observation
binds the published archives and signatures, the exact tag and Builds
identities, Dapr 1.18.2, and the tracked PostgreSQL v1 component.

The final complete execution is
`execution-64d7c7a84abc4f71b89e26e7793813d7/`. Its `execution.json`
lists all 17 canonical lanes, both adopted additions, the fresh-database
restore, and owned cleanup. Executor errors are empty. Reminder recovery
passed 131/131, restore passed 12/12, and cleanup passed 7/7. Failed
historical directions, the 3.119.0 stale-fence denial gap, the logical
alias/evolution gap, and four startup-failure seed checks remain nonpassing.
The startup-failure seed passed 37/37 in the separate fresh targeted
`development-smoke-startup-b5ea938865b84163bf293d138ffef234/`.

The independently prepared and validated packet is
`packet-7e1d2c98585e4c1c995831af2998b558/`. `prepare` and `validate`
both exited 0. Validation reports `valid=true`,
`technically_qualified=false`, `decisions_complete=false`, and
`p1r_usable=false`. The recovery disposition remains mutation freeze and
forward recovery because no capable rollback was selected.

Earlier observations, development smokes, failed execution
`execution-b331134522214eacbbcc0744b8ae113a/`, complete but
nonpassing execution `execution-19b767f156174fb2ae1b19d28ae819a6/`,
and failed prepare packets remain at their unique paths. No earlier
receipt was reused in the final packet. Rendered PostgreSQL credentials
were kept in invocation-private scratch; retained configuration evidence
contains the tracked template and the rendered file hash.
