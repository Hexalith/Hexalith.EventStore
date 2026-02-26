[← Back to Hexalith.EventStore](../README.md)

# Page Template & Documentation Conventions

This page defines the standard structure, naming conventions, and formatting rules for all Hexalith.EventStore documentation pages.

## Page Structure

Every documentation page MUST follow this structure:

```markdown
[← Back to Hexalith.EventStore](../../README.md)

# Page Title

One-paragraph summary of what this page covers and who it's for.

> **Prerequisites:** [Prerequisite 1](link), [Prerequisite 2](link)
>
> (Maximum 2 prerequisites per NFR10. Omit if none required.)

## Main Content Sections

(Page-specific content)

## Next Steps

- **Next:** [Logical next page](link) — one-sentence description
- **Related:** [Related page 1](link), [Related page 2](link)
```

## Page Structure Rules

- **Back-link:** Every page starts with a relative link back to `README.md`
- **H1 title:** Exactly one H1 heading per page — this is the page title
- **Summary:** One paragraph immediately after the title describing the page content and audience
- **Prerequisites:** Optional callout using blockquote syntax, maximum 2 prerequisites (NFR10)
- **Content sections:** Use H2 for major sections, H3 for subsections, H4 for sub-subsections
- **Next Steps footer:** Every page ends with "Next:" and "Related:" links to guide the reader

## File Naming Convention

All documentation files MUST follow these naming rules:

- **Lowercase letters only** — no uppercase characters
- **Hyphen-separated words** (kebab-case) — no underscores or spaces
- **Descriptive, unabbreviated names** — prefer clarity over brevity
- **`.md` extension** for all documentation files

| Good | Bad | Why |
|------|-----|-----|
| `configuration-reference.md` | `config-ref.md` | No abbreviations |
| `architecture-overview.md` | `Architecture-Overview.md` | Lowercase only |
| `getting-started.md` | `getting_started.md` | Hyphens, not underscores |

## Cross-Linking Convention

All internal links MUST use **relative paths only**:

- **Same folder:** `[link text](file.md)`
- **Child folder:** `[link text](subfolder/file.md)`
- **Parent folder:** `[link text](../folder/file.md)`
- **Never** use absolute URLs for internal documentation links

This ensures links work correctly when browsing on GitHub and in any future documentation site.

## Assets Convention

All media files are centralized under `docs/assets/`:

- **Images:** `docs/assets/images/` — screenshots, logos, illustrations
- **Diagrams:** `docs/assets/diagrams/` — Mermaid source files, architecture diagrams
- **GIF demos:** `docs/assets/` — animated demonstrations (e.g., `quickstart-demo.gif`)

Reference assets using relative paths from your documentation page:

```markdown
![Architecture diagram](../assets/diagrams/architecture.png)
![Screenshot](../assets/images/example.png)
```

## No YAML Frontmatter

Pages MUST NOT use YAML frontmatter. GitHub renders YAML frontmatter as visible text in the page, which degrades the reading experience.

## Markdown Formatting Rules

| Pattern | Rule |
|---------|------|
| Heading hierarchy | H1 = page title (one per page), H2 = major sections, H3 = subsections. Never skip levels. |
| Code blocks | Always specify language: ` ```csharp `, ` ```bash `, ` ```yaml `. Never bare fences. |
| Terminal commands | Use `bash` language tag. Prefix commands with `$`. |
| Callouts | Use GitHub blockquote syntax: `> **Note:**`, `> **Warning:**`, `> **Tip:**` |
| Tables | Use for structured comparisons. Keep cells concise. Always include header row. |
| Lists | Ordered for sequential steps. Unordered for non-sequential items. |
| Line length | No hard wrap in markdown source. |

## Next Steps

- **Next:** [Getting Started](getting-started/) — begin setting up your development environment
- **Related:** [README](../README.md), [Contributing](../CONTRIBUTING.md)
