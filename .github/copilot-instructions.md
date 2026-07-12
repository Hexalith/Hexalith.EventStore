# AI assistant instructions

Before working in this repository, read
[`hexalith-llm-instructions.md`](../references/Hexalith.AI.Tools/hexalith-llm-instructions.md)
(in the `references/Hexalith.AI.Tools` submodule) and follow it.

## Commit Messages

When generating a commit message, follow the repository's
`@commitlint/config-conventional` contract directly:

- Format the header as `<type>[optional scope][!]: <description>`.
- Start the description with a lowercase letter and omit a trailing period.
  Use imperative mood as a repository authoring convention.
- Keep the entire header at 100 characters or fewer; prefer a concise header
  near 50 characters.
- Choose the type by release impact: `feat` for a minor release, `fix` or
  `perf` for a patch release, and `docs`, `test`, `refactor`, `build`, `ci`,
  `chore`, `revert`, or `style` for changes that do not release product
  behavior.
- Use `!` or a `BREAKING CHANGE:` footer for a major release.

Commitlint mechanically enforces the header format, allowed types, description
case, trailing punctuation, and length. Imperative mood and choosing the type
that accurately reflects release impact remain author and reviewer
responsibilities.

## Git Submodules

- Initialize root-declared submodules only, using the `references/...` paths declared in the root `.gitmodules` file.
- Avoid recursive submodule commands unless they are explicitly scoped so that nested submodules are not initialized.
- If nested submodules are initialized accidentally, deinitialize them before continuing.
