# Architecture

[中文](architecture.zh-CN.md)

## 1. Scope

Stopamap.Markdown is the platform-independent core of a live Markdown editor:

```text
source string
    │
    ▼
MarkdownLiveDocument.Create
    │  LF normalization
    │  block parsing
    │  line-start index
    ▼
MdBlock[] + line offsets
    │
    ▼
downstream renderer
    │  measure text
    │  paint styled runs
    │  map caret / pointer / selection back to source offsets
    ▼
source edit request
```

The core owns parsing and coordinates. A renderer owns pixels and interaction.

## 2. Data model

`MarkdownModel.cs` defines three public structures:

| Type | Purpose |
| --- | --- |
| `MdBlock` | One top-level document block and its source line |
| `MdListItem` | One list row, including indent, task state, and inline runs |
| `MdInline` | A styled text run with optional URL or image URL |
| `MdOutlineItem` | Heading text, level, line number, and source character index |

`MdBlockType` currently includes:

```text
Heading, Paragraph, List, Quote, CodeBlock, Table,
HorizontalRule, Image, Empty
```

`Empty` is intentional. Blank lines are visible editor content, so dropping them breaks caret movement and newline editing.

## 3. Parsing pipeline

`MarkdownParser.Parse` works in two passes:

1. **Definition pre-pass.** Scan for `[id]: url` reference definitions and `[^id]: note` footnote definitions. Definitions are removed from the visible body and stored in dictionaries.
2. **Block pass.** Walk lines and dispatch on the current syntax: YAML front matter, code fences, headings, rules, tables, quotes, lists, images, paragraphs. Each block records its starting line in `SourceLine`.
3. **Inline pass.** Paragraph, heading, table-cell, quote, and list text is parsed by `ParseInlines`. It supports nested emphasis, links, images, hard breaks, HTML entities, and the triple-emphasis form `***text***`.

`ExtractOutline` is a separate lightweight scan. It deliberately does not reuse the block parser so the outline can stay cheap while typing. **If you change fence rules, update both `Parse` and `ExtractOutline`.**

## 4. Coordinate model

### 4.1 Normalization

`MarkdownLiveDocument.Create` converts CRLF and CR to LF exactly once:

```csharp
text = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
```

Every offset exposed by the library refers to this normalized string.

### 4.2 Line index

`_lineStarts` stores the first character of each line plus a sentinel equal to `Text.Length`.

```text
Text:        A \n \n B
Index:       0  1  2  3
_lineStarts: 0  2  3  4
```

`LineStart(line)` and `LineEnd(line)` clamp out-of-range values. `LineOfOffset(offset)` uses binary search.

### 4.3 Block ranges

```csharp
(start, end) = document.RangeOf(block);
```

- `start` is the beginning of the block's first line.
- `end` is the beginning of the next block's first line, or `Text.Length` for the last block.
- The range is half-open `[start, end)`.
- The range includes trailing separators; use `ContentEnd(block)` for the editable payload.

### 4.4 Blank runs

`BlankRunAt(offset)` returns the contiguous run of blank lines around the offset. The editor uses it to delete an empty paragraph without joining the surrounding paragraphs.

## 5. Renderer integration

A renderer typically does this on every text change:

```csharp
var document = MarkdownLiveDocument.Create(editorText);
foreach (var block in document.Blocks)
{
    var range = document.RangeOf(block);
    var contentEnd = document.ContentEnd(block);
    RenderBlock(block, range, contentEnd);
}
```

Recommended invariants for the renderer:

- Never keep a stale `MarkdownLiveDocument` after the source changes.
- Never recalculate CRLF normalization locally.
- Convert pointer positions to source offsets through the same line index.
- When editing a block, replace only `[start, contentEnd)` or the exact selected range.
- Treat `MdBlockType.Empty` as a real line with a caret stop.

## 6. Extending syntax

To add a new block type:

1. Add a value to `MdBlockType`.
2. Add any fields to `MdBlock`.
3. Emit the block in `MarkdownParser.Parse`.
4. Update `ExtractOutline` only if the syntax affects headings.
5. Add a renderer case downstream.
6. Add parser and live-document tests, including a CRLF variant.
7. Update `CHANGELOG.md` and, if public API changed, `README.md`.

To add an inline style:

1. Add a flag or data field to `MdInline`.
2. Parse it in `ParseSegment`, preserving nesting order.
3. Add it to the downstream renderer.
4. Test combinations such as bold + link and code + emphasis.

## 7. Testing strategy

- `MarkdownParserTests`: block types, inline styles, links, tables, outline, list tasks.
- `MarkdownLiveDocumentTests`: LF normalization, line starts, contiguous ranges, content end, blank runs.
- CI runs `dotnet test` on Windows, Linux, and macOS.

A bug fix should add a test that fails before the fix and passes after it.

## 8. Known limitations

- Not a complete CommonMark implementation.
- HTML is rendered as literal text by the downstream renderer.
- Inline parsing is pragmatic; ambiguous Markdown follows the editor's behavior, not a full spec.
- The core does not perform syntax highlighting, layout, image decoding, or IME handling.
- API is pre-1.0 and may change with a minor version until `1.0.0`.
