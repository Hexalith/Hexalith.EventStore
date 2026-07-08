# AI assistant instructions

Before working in this repository, read
[`hexalith-llm-instructions.md`](./references/Hexalith.AI.Tools/hexalith-llm-instructions.md)
(in the `references/Hexalith.AI.Tools` submodule) and follow it.

## Git Submodules

IMPORTANT! Only initialize and update submodules declared in the root repository `.gitmodules` file.

- Initialize root-declared submodules only, using the `references/...` paths declared in the root `.gitmodules` file.
- Do not initialize, update, or recurse into nested submodules inside those root-declared submodules.
- Avoid recursive submodule commands unless they are explicitly scoped so that nested submodules are not initialized.
- If nested submodules are initialized accidentally, deinitialize them before continuing.

