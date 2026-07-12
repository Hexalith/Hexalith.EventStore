# Verification-Gap Review Prompt

Run this prompt in a separate session with the repository working directory set to `/home/administrator/projects/hexalith/eventstore`:

> Invoke the `bmad-review-verification-gap` skill on this diff:
>
> Baseline: `7eb975e0ea59bde713f514f609f090b1c52c2cba`.
>
> Obtain the complete diff verbatim with:
>
> ```bash
> git diff --no-ext-diff --unified=80 \
>   7eb975e0ea59bde713f514f609f090b1c52c2cba -- \
>   .github/copilot-instructions.md \
>   AGENTS.md \
>   CLAUDE.md \
>   _bmad-output/implementation-artifacts/spec-gh-29195132839-fix-tier-1-doc-contracts.md
> ```
>
> At review start, the intended implementation diff contains exactly those four tracked modified files. Review only; do not edit files. Return findings about changed behavior that could regress without reliable verification, with precise evidence and concrete required actions.

Paste the separate session's findings back into the original `bmad-quick-dev` session so review classification can continue.
