# 架构说明

[English](architecture.md)

## 1. 范围

Stopamap.Markdown 是实时 Markdown 编辑器的平台无关内核：

```text
Markdown 原文
    │
    ▼
MarkdownLiveDocument.Create
    │  统一换行符
    │  块级解析
    │  行起点索引
    ▼
MdBlock[] + 行偏移
    │
    ▼
下游渲染器
    │  测量文本
    │  绘制样式片段
    │  把光标 / 鼠标 / 选区映射回源码偏移
    ▼
源码编辑请求
```

本库负责解析和坐标；渲染器负责像素和交互。

## 2. 数据结构

`MarkdownModel.cs` 定义公开结构：

| 类型 | 作用 |
| --- | --- |
| `MdBlock` | 一个顶层文档块及其源码行号 |
| `MdListItem` | 列表项，含缩进、任务状态、行内片段 |
| `MdInline` | 一段带样式的文字，可带链接或图片地址 |
| `MdOutlineItem` | 大纲项，含标题、级别、行号、字符下标 |

`MdBlockType` 当前包含：

```text
Heading, Paragraph, List, Quote, CodeBlock, Table,
HorizontalRule, Image, Empty
```

`Empty` 是刻意保留的：空行也是编辑器里的可见内容，直接丢弃会让光标和回车行为出错。

## 3. 解析流程

`MarkdownParser.Parse` 分两遍：

1. **定义预扫描。** 收集 `[id]: url` 参考式链接和 `[^id]: 内容` 脚注定义。定义行不进入正文。
2. **块级扫描。** 按行判断 YAML Front Matter、围栏代码块、标题、分隔线、表格、引用、列表、图片、段落。每个块用 `SourceLine` 记住起始行。
3. **行内解析。** 标题、段落、表格单元格、引用和列表文字交给 `ParseInlines`，支持嵌套强调、链接、图片、硬换行、HTML 实体和三连星号 `***文字***`。

`ExtractOutline` 是独立的轻量扫描，故意不复用块解析器，让用户输入时大纲更新更便宜。**修改代码围栏规则时，`Parse` 和 `ExtractOutline` 必须一起改。**

## 4. 坐标系统

### 4.1 统一换行

`MarkdownLiveDocument.Create` 只做一次 CRLF/CR → LF：

```csharp
text = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
```

库对外暴露的所有下标都以这个字符串为准。

### 4.2 行索引

`_lineStarts` 保存每行首字符下标，最后一个哨兵等于 `Text.Length`。

```text
Text:        A \n \n B
Index:       0  1  2  3
_lineStarts: 0  2  3  4
```

`LineStart(line)` / `LineEnd(line)` 会夹取越界值；`LineOfOffset(offset)` 使用二分查找。

### 4.3 块区间

```csharp
(start, end) = document.RangeOf(block);
```

- `start` 是块第一行的行首。
- `end` 是下一块第一行的行首；最后一块为 `Text.Length`。
- 区间为左闭右开 `[start, end)`。
- 区间包含块尾换行；可编辑内容用 `ContentEnd(block)`。

### 4.4 连续空行

`BlankRunAt(offset)` 返回光标所在连续空行的区间。编辑器用它删除空段落，避免把上下两段粘成一行。

## 5. 接入渲染器

每次文本变化时，渲染器通常这样做：

```csharp
var document = MarkdownLiveDocument.Create(editorText);
foreach (var block in document.Blocks)
{
    var range = document.RangeOf(block);
    var contentEnd = document.ContentEnd(block);
    RenderBlock(block, range, contentEnd);
}
```

推荐遵守：

- 原文变化后不要继续使用旧的 `MarkdownLiveDocument`；
- 不要在渲染器里再做一次 CRLF 归一化；
- 鼠标位置必须通过同一套行索引换算成源码偏移；
- 编辑块时只替换 `[start, contentEnd)` 或精确选区；
- `MdBlockType.Empty` 要当成真实可放置光标的一行。

## 6. 扩展语法

新增块类型：

1. 在 `MdBlockType` 加枚举；
2. 在 `MdBlock` 加字段；
3. 在 `MarkdownParser.Parse` 产出该块；
4. 如果影响标题，同步修改 `ExtractOutline`；
5. 下游渲染器增加分支；
6. 增加解析与 LiveDocument 测试，包含 CRLF 变体；
7. 更新 `CHANGELOG.md`，公开 API 变化时更新 `README`。

新增行内样式：

1. 在 `MdInline` 加标记或数据字段；
2. 在 `ParseSegment` 中按嵌套顺序解析；
3. 下游渲染器支持该样式；
4. 覆盖“粗体 + 链接”“代码 + 强调”等组合测试。

## 7. 测试策略

- `MarkdownParserTests`：块类型、行内样式、链接、表格、大纲、任务列表。
- `MarkdownLiveDocumentTests`：换行归一化、行起点、连续区间、内容结尾、空行区间。
- CI 在 Windows、Linux、macOS 上运行 `dotnet test`。

修复 Bug 时，先补一个当前会失败的测试，再修到通过。

## 8. 已知限制

- 不是完整 CommonMark 实现；
- HTML 由下游渲染器按纯文本显示；
- 行内解析采用“编辑器够用”策略，歧义语法以 Stopamap 的行为为准；
- 核心不做语法高亮、排版、图片解码和输入法处理；
- `1.0.0` 前 API 可能随 minor 版本调整。
