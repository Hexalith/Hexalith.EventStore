
- source_spec: `spec-6-1-p1r-31210-published-qualification.md`
  summary: Make the pre-existing incomplete checkout witness validator retain error receipts without accessing an absent comparison object.
  evidence: EventStore HEAD21b34c01 tools/p1r_check_witnesses.py computes coordinate_identifier from observation[comparison] before its not-incomplete guards; the original source-timeout checkout receipt raises KeyError at this unchanged path. Complete checkout receipts validate, and all original error evidence remains retained.

Resolved during the adjacent call-site repair: coordinate_identifier now requires not incomplete before reading comparison. The original failed audit/checkout remain retained. The temporary entry was removed from the ledger; all pre-existing ledger bytes were preserved.
