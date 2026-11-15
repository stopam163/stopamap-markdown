# Stopamap.Markdown

面向**实时编辑器**的轻量 Markdown 解析与源码坐标引擎，零第三方运行时依赖。

[English](README.md) · [架构说明](docs/architecture.zh-CN.md) · [贡献指南](CONTRIBUTING.zh-CN.md) · [MIT 许可证](LICENSE)

## 为什么做这个项目

普通 Markdown 解析器只需要回答：*这篇文档里有什么？*

实时编辑器还要回答更难的问题：**渲染出来的每一行、每一个块，对应原文的哪一段字符？** 只要偏移错一位，就会出现光标漂移、回车多一行、代码围栏被吞、选区跨块乱跳。

Stopamap.Markdown 把“源码坐标”作为核心能力：

- 统一换行符，整个预览只使用一套坐标；
- 生成稳定的块树；
- 预先缓存每一行的起始下标；
- 每个块都能取到 `[start, end)` 源码区间；
- 解析层和渲染层之间不藏隐式状态。

本库从 Stopamap 桌面版的 Markdown 实时渲染内核中抽取，独立于 Avalonia、主题和文件存储。

## 包含什么

| 能力 | 内容 |
| --- | --- |
| 块级解析 | 标题、段落、列表、任务列表、引用、围栏代码块、表格、分隔线、图片、YAML Front Matter |
| 行内解析 | 粗体、斜体、粗斜体、删除线、行内代码、下划线、高亮、上下标、链接、图片、硬换行 |
| 引用与脚注 | 参考式链接、简写参考式链接、脚注 |
| 源码坐标 | LF 统一、行起点、块区间、内容区间、连续空行区间 |
| 辅助能力 | 大纲提取、字数/字符数/行数统计 |

**不包含** Avalonia 控件、主题、图片存储和桌面应用服务。核心库与平台无关，可以被 Avalonia、WPF、MAUI、Web 后端或命令行工具复用。

## 快速开始

环境要求：.NET 9 SDK。

```bash
git clone https://github.com/stopam163/stopamap-markdown.git
cd stopamap-markdown
dotnet build
dotnet test
```

在另一个项目中引用：

```xml
<ProjectReference Include="..\stopamap-markdown\src\Stopamap.Markdown\Stopamap.Markdown.csproj" />
```

使用示例：

```csharp
using Stopamap.Markdown;

var document = MarkdownLiveDocument.Create("""
# 你好

这是一段 **带源码坐标** 的正文。

| 名称 | 值 |
| --- | --- |
| alpha | 1 |
""");

foreach (var block in document.Blocks)
{
    var range = document.RangeOf(block);
    Console.WriteLine($"{block.Type}: [{range.Start}, {range.End})");
}
```

如果只需要解析结果：

```csharp
var blocks = MarkdownParser.Parse(markdown);
var outline = MarkdownParser.ExtractOutline(markdown);
var count = MarkdownParser.Count(markdown);
```

运行演示：

```bash
dotnet run --project samples/Stopamap.Markdown.Demo -- samples/hello.md
```

## 核心约定

1. **只保留一套换行约定。** `MarkdownLiveDocument.Text` 永远是 LF；所有偏移都相对于这个字符串。
2. **行起点只算一次。** 行起点数组常驻内存，`LineOfOffset` 使用二分查找。
3. **块区间是左闭右开。** `RangeOf(block)` 返回 `[start, end)`，包含块尾用于分隔下一块的换行。
4. **内容区间不含尾部空行。** `ContentEnd(block)` 是可编辑内容的最后一个字符之后。
5. **空行也是块。** 可见空行不会被吞掉，而是保留为 `MdBlockType.Empty`。

这些约定都有测试覆盖，见 `tests/Stopamap.Markdown.Tests`。

## 目录结构

```text
src/Stopamap.Markdown/       # 公开库
  MarkdownModel.cs           # 块与行内数据结构
  MarkdownParser.cs          # 块解析、行内解析、大纲与统计
  MarkdownLiveDocument.cs    # 行索引与源码区间
samples/Stopamap.Markdown.Demo/
tests/Stopamap.Markdown.Tests/
docs/architecture.zh-CN.md   # 架构与扩展指南
docs/maintainer-guide.zh-CN.md # 分支、发布、评审与 Issue 管理
```

## 兼容性与限制

- 这是“够用优先”的解析器，不是完整 CommonMark 实现。
- HTML 块和任意 HTML 会退化为纯文本，不会被执行。
- 转义、样式、图片加载和交互由下游渲染器负责。
- 性能针对交互式文档优化，不面向超大自动生成文件。
- 当前目标框架为 .NET 9；API 稳定后再考虑多目标框架。

## 参与贡献

提交 PR 前请阅读 [CONTRIBUTING.zh-CN.md](CONTRIBUTING.zh-CN.md)。报告 Bug 时请附最小 Markdown 输入、期望结果、实际结果和运行平台。

分支模型、发布流程和常见问题见 [维护者指南](docs/maintainer-guide.zh-CN.md)。

## 许可证

MIT，见 [LICENSE](LICENSE)。
