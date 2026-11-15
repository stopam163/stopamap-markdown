# Stopamap.Markdown

A small, dependency-free, source-mapped Markdown engine for **live editors**.

[中文说明](README.zh-CN.md) · [Architecture](docs/architecture.md) · [Contributing](CONTRIBUTING.md) · [MIT License](LICENSE)

## Why this exists

Most Markdown parsers answer one question: *what does this document contain?*

A live editor has to answer a harder one: **where in the original source does each rendered line and block come from?** A one-character offset error becomes a misplaced caret, a duplicated newline, a swallowed code fence, or a selection that jumps across blocks.

Stopamap.Markdown keeps that coordinate system honest:

- normalize all line endings once;
- parse a stable block tree;
- index every line start;
- expose `[start, end)` ranges for every block;
- avoid hidden state between parsing and rendering.

It is extracted from the Markdown live-rendering core of Stopamap, a desktop mind-map and Markdown workspace.

## What is included

| Area | Included |
| --- | --- |
| Block parser | headings, paragraphs, lists, tasks, quotes, fenced code, tables, rules, images, YAML front matter |
| Inline parser | bold, italic, bold-italic, strikethrough, code, underline, highlight, sup/sub, links, images, hard breaks |
| References | reference-style links, shortcut references, footnotes |
| Source map | LF normalization, line starts, block ranges, content ranges, blank runs |
| Utilities | outline extraction, word/character/line count |

The repaint layer is intentionally **not** in this repository. Avalonia controls, themes, image storage, and application services stay in the downstream app. The core is platform-independent and can be consumed by Avalonia, WPF, MAUI, a web backend, or a CLI.

## Quick start

Requirements: .NET 9 SDK.

```bash
git clone https://github.com/stopam163/stopamap-markdown.git
cd stopamap-markdown
dotnet build
dotnet test
```

Add a project reference:

```xml
<ProjectReference Include="..\stopamap-markdown\src\Stopamap.Markdown\Stopamap.Markdown.csproj" />
```

Use the engine:

```csharp
using Stopamap.Markdown;

var document = MarkdownLiveDocument.Create("""
# Hello

A **source-mapped** paragraph.

| Name | Value |
| --- | --- |
| alpha | 1 |
""");

foreach (var block in document.Blocks)
{
    var range = document.RangeOf(block);
    Console.WriteLine($"{block.Type}: [{range.Start}, {range.End})");
}
```

Parse without the live-document wrapper:

```csharp
var blocks = MarkdownParser.Parse(markdown);
var outline = MarkdownParser.ExtractOutline(markdown);
var count = MarkdownParser.Count(markdown);
```

Run the demo:

```bash
dotnet run --project samples/Stopamap.Markdown.Demo -- samples/hello.md
```

## Core invariants

1. **One line-ending convention.** `MarkdownLiveDocument.Text` is always LF-normalized. All offsets refer to this string.
2. **One source of truth for lines.** Every line start is cached in a compact array; `LineOfOffset` is O(log n).
3. **Block ranges are half-open.** `RangeOf(block)` returns `[start, end)`, including the newline that separates it from the next block.
4. **Content ranges exclude trailing blank lines.** `ContentEnd(block)` is the index just after the last editable character.
5. **Empty lines are blocks.** A visible blank line is not dropped; it is represented as `MdBlockType.Empty`.

These invariants are covered by tests in `tests/Stopamap.Markdown.Tests`.

## Project layout

```text
src/Stopamap.Markdown/       # public library
  MarkdownModel.cs           # block and inline data model
  MarkdownParser.cs          # block + inline parser and outline/count helpers
  MarkdownLiveDocument.cs    # line index and source ranges
samples/Stopamap.Markdown.Demo/
tests/Stopamap.Markdown.Tests/
docs/architecture.md         # design and extension guide
docs/maintainer-guide.md     # release, branch, review and triage workflow
```

## Compatibility and limitations

- This is a pragmatic parser, not a full CommonMark implementation.
- HTML blocks and arbitrary HTML are treated as plain text instead of being executed.
- The renderer is responsible for escaping, styling, image loading, and interaction.
- Performance is tuned for interactive documents, not multi-megabyte generated files.
- The library currently targets .NET 9. Multi-targeting can be added once the API stabilizes.

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request. Bug reports should include a minimal Markdown input, expected output, actual output, and the platform used.

Please also read the [maintainer guide](docs/maintainer-guide.md) for the branch model and release process.

## License

MIT. See [LICENSE](LICENSE).
