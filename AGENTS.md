# AI assistant instructions

Before working in this repository, read
[`hexalith-llm-instructions.md`](references/Hexalith.AI.Tools/hexalith-llm-instructions.md)
(in the `references/Hexalith.AI.Tools` submodule) and follow it.

## Agent Skills

- For this section, the repository root is the directory containing this instruction file, regardless of the current working directory. An agent skill is a `SKILL.md` manifest and its supporting files when treated as an available skill.
- Never discover (register or select as available), load, or execute an agent skill when either its discovery path or resolved canonical path is inside the repository root's `references/` directory. Incidental filename matches during ordinary source searches are not skill discovery.
- If tooling exposes a skill from `references/`, or a user names one explicitly, do not open it as a skill. Continue without it and use an allowed alternative if one is available.
- Use only skills located outside the root `references/` tree, such as root-workspace or user-environment skills.
- This restriction applies only to agent skills. Continue to read non-skill instructions, documentation, and source files in `references/` when required by the task or repository instructions.

## Git Submodules

- Initialize root-declared submodules only, using the `references/...` paths declared in the root `.gitmodules` file.
- Avoid recursive submodule commands unless they are explicitly scoped so that nested submodules are not initialized.
- If nested submodules are initialized accidentally, deinitialize them before continuing.
