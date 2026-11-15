# Stopamap.Markdown demo

A **source-mapped** Markdown paragraph with *italic*, ***bold italic***,
`inline code`, ~~strikethrough~~, <u>underline</u>, <mark>highlight</mark>,
H<sub>2</sub>O, x<sup>2</sup>, and a [**bold link**](https://example.com).

> A quote with **strong** text.

- [x] Parse a task list
- [ ] Keep a blank line visible
  - Nested item

```csharp
var document = MarkdownLiveDocument.Create(markdown);
var range = document.RangeOf(document.Blocks[0]);
```

| Layer | Responsibility |
| --- | --- |
| Parser | Source → blocks |
| Live document | Offset → line and block |

![Stopamap](../assets/stopamap.jpg)

---

[^1]: Footnotes are parsed and resolved by the downstream renderer.
