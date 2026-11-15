// ============================================================================
//  Markdown/MarkdownModel.cs —— Markdown 解析结果的“数据结构”
// ============================================================================
//  解析 Markdown 分两步（业界通用做法）：
//    第一步：块级解析（block）—— 把全文切成“标题 / 段落 / 列表 / 代码块 / 表格 / 引用…”
//    第二步：行内解析（inline）—— 把段落里的 **加粗**、`代码`、[链接](url) 拆成小片段
//
//  这个文件只定义数据结构，具体解析在 MarkdownParser.cs，渲染交给下游 UI/HTML 适配器。
// ============================================================================

namespace Stopamap.Markdown;

/// <summary>
/// 块级元素类型。解析器只产出这些类型，渲染器只处理这些类型；
/// 两者之间没有隐藏状态，新增一种语法时先在这里加类型，再补解析器和下游渲染分支。
/// </summary>
public enum MdBlockType
{
    /// <summary>标题，级别由 <see cref="MdBlock.Level"/> 表示。</summary>
    Heading,
    /// <summary>普通段落，正文放在 <see cref="MdBlock.Inlines"/>。</summary>
    Paragraph,
    /// <summary>无序、有序或任务列表，明细放在 <see cref="MdBlock.Items"/>。</summary>
    List,
    /// <summary>引用块，每行一组行内片段放在 <see cref="MdBlock.QuoteLines"/>。</summary>
    Quote,
    /// <summary>围栏代码块，正文放在 <see cref="MdBlock.Code"/>。</summary>
    CodeBlock,
    /// <summary>表格，表头和数据行分别放在 TableHeaders / TableRows。</summary>
    Table,
    /// <summary>分隔线：<c>---</c>、<c>***</c> 或 <c>___</c>。</summary>
    HorizontalRule,
    /// <summary>独占一行的图片，便于渲染器输出大图。</summary>
    Image,
    /// <summary>空行。它是可见的编辑内容，不能被静默丢弃。</summary>
    Empty
}

/// <summary>块级元素。</summary>
public sealed class MdBlock
{
    /// <summary>块类型。</summary>
    public MdBlockType Type { get; set; }

    /// <summary>标题级别 1–6（仅 Heading 用）。</summary>
    public int Level { get; set; }

    /// <summary>段落的行内片段（Paragraph / Heading 用）。</summary>
    public List<MdInline> Inlines { get; } = new();

    /// <summary>列表项（List 用）。</summary>
    public List<MdListItem> Items { get; } = new();

    /// <summary>有序列表的起始序号（无序列表为 0）。</summary>
    public int StartNumber { get; set; }

    /// <summary>引用块里的多段文字（Quote 用）。</summary>
    public List<List<MdInline>> QuoteLines { get; } = new();

    /// <summary>代码块的语言标记（```csharp 里的 csharp）。</summary>
    public string Language { get; set; } = "";

    /// <summary>
    /// 代码块开头的围栏符号：反引号 "```" 或波浪号 "~~~"。
    /// Markdown 允许两种围栏混用，而且围栏里的内容可以出现另一种符号：
    /// 例如 ~~~none 里面写 ``` 内容 ```，三个反引号只是正文，不会结束代码块。
    /// 提交编辑时如果把 ~~~ 强行改写成 ```，内层那行 ``` 就会把块提前闭合，
    /// 后面的正文会被解析成新的代码块，表现出来就是“点一下全乱了”。
    /// 所以必须原样记住、原样写回。
    /// </summary>
    public string Fence { get; set; } = "```";

    /// <summary>代码块原文。</summary>
    public string Code { get; set; } = "";

    /// <summary>表格：表头 + 每一行（每行是一个字符串列表）。</summary>
    public List<string> TableHeaders { get; } = new();

    /// <summary>表格数据行，每行保留原始单元格字符串，便于写回源码。</summary>
    public List<List<string>> TableRows { get; } = new();

    /// <summary>
    /// 表格单元格解析后的行内片段，和上面的字符串列表一一对应。
    /// 字符串列表保留原始 Markdown（编辑表格时要写回源码），这里保留已经拆好的
    /// 粗体/斜体/代码/链接/上下标等片段，预览渲染时直接使用，避免把 `**` 原样画出来。
    /// </summary>
    public List<List<MdInline>> TableHeaderInlines { get; } = new();

    /// <summary>表格数据行对应的行内片段，与 <see cref="TableRows"/> 一一对应。</summary>
    public List<List<List<MdInline>>> TableRowInlines { get; } = new();

    /// <summary>这张块在原文里的起始行号（0 开始），用于“点大纲跳转”。</summary>
    public int SourceLine { get; set; }

    /// <summary>这一段是不是文末自动追加的脚注说明（渲染时可弱化字号）。</summary>
    public bool IsFootnote { get; set; }
}

/// <summary>列表项。</summary>
public sealed class MdListItem
{
    /// <summary>缩进层级：0 = 顶层，1 = 子层……</summary>
    public int Indent { get; set; }

    /// <summary>是否是任务列表项（- [ ] / - [x]）。</summary>
    public bool IsTask { get; set; }

    /// <summary>任务是否已完成。</summary>
    public bool TaskDone { get; set; }

    /// <summary>有序列表里这一项的序号（无序为 0）。</summary>
    public int Number { get; set; }

    /// <summary>这一项的行内片段。</summary>
    public List<MdInline> Inlines { get; } = new();
}

/// <summary>行内片段：一段样式相同的文字。</summary>
public sealed class MdInline
{
    /// <summary>文字内容，不含 Markdown 标记。</summary>
    public string Text { get; set; } = "";

    /// <summary>是否加粗。</summary>
    public bool Bold { get; set; }

    /// <summary>是否斜体。</summary>
    public bool Italic { get; set; }

    /// <summary>是否删除线。</summary>
    public bool Strike { get; set; }

    /// <summary>是否行内代码。</summary>
    public bool Code { get; set; }

    // 下面四种是用户第 13 轮点出来的“HTML 行内样式”。
    // 以前解析器只认 ** / * / ~~ / `，`<u>`、`<mark>`、`<sup>`、`<sub>`
    // 会原样留在文字里，所以预览看起来“没渲染”。现在拆成独立标记，
    // 渲染层按标记画下划线、底色、上下标。

    /// <summary>是否下划线。</summary>
    public bool Underline { get; set; }

    /// <summary>是否高亮。</summary>
    public bool Highlight { get; set; }

    /// <summary>是否上标。</summary>
    public bool Superscript { get; set; }

    /// <summary>是否下标。</summary>
    public bool Subscript { get; set; }

    /// <summary>链接地址（普通文本为 null）。</summary>
    public string? Url { get; set; }

    /// <summary>图片地址（Image 片段用）。</summary>
    public string? ImageUrl { get; set; }

    /// <summary>行内换行（Markdown 的两个空格 + 换行，或 &lt;br&gt;）。</summary>
    public bool LineBreak { get; set; }
}

/// <summary>大纲里的一条（用于左侧栏）。</summary>
public sealed class MdOutlineItem
{
    /// <summary>标题级别，1–6。</summary>
    public int Level { get; set; }

    /// <summary>标题纯文本。</summary>
    public string Text { get; set; } = "";

    /// <summary>在原文中的字符下标（TextBlock 跳转用）。</summary>
    public int CharIndex { get; set; }

    /// <summary>行号（1 开始，显示给人看）。</summary>
    public int LineNumber { get; set; }

    /// <summary>返回用于显示的大纲文本。</summary>
    public override string ToString() => $"{new string(' ', (Level - 1) * 2)}{Text}";
}
