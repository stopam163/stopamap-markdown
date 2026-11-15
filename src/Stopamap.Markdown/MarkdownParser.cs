// ============================================================================
//  Markdown/MarkdownParser.cs —— Markdown 原文 → 结构化块
// ============================================================================
//  这个解析器不追求 100% 符合 CommonMark 规范（那需要上千行），
//  而是覆盖“实际写文章会用到”的语法，并保证不会把内容解析丢：
//    标题 # ~ ######
//    段落（含行内样式）
//    无序/有序/任务列表（支持缩进嵌套）
//    引用 >（支持多行）
//    代码块 ``` 或 ~~~（支持语言标记，两种围栏可混用）
//    表格 | a | b |
//    分隔线 --- / *** / ___
//    独占一行的图片 ![alt](url)
//  不认识的语法（比如 <div> HTML 片段）会退化显示成纯文本，不会消失。
//
//  解析分两遍：
//    第一遍扫描全文，收集参考式链接 [id]: url 和脚注 [^id]: 内容；
//    第二遍按行切块，块内再交给 ParseInlines 拆行内样式。
//
//  注意：ExtractOutline 是另一套“只看标题”的轻量扫描，它有自己的围栏状态机。
//  改代码块围栏规则（``` / ~~~）时，Parse 和 ExtractOutline 必须一起改，
//  否则会出现“正文渲染正常、左侧大纲却少了后半截”。
// ============================================================================

using System.Text.RegularExpressions;
using Stopamap.Markdown;

namespace Stopamap.Markdown;

/// <summary>
/// 轻量 Markdown 解析器。对外只暴露四个入口：
/// Parse（正文切块）、ExtractOutline（左栏大纲）、Count（字数/行数）、
/// 以及内部复用的 ParseInlines（行内样式）。
/// 所有方法都不修改传入的 markdown 字符串。
/// </summary>
public static class MarkdownParser
{
    private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.*?)\s*#*\s*$", RegexOptions.Compiled);
    // 分隔线：CommonMark 允许符号之间有空格（用户样例里的 `- - -` 就是这种写法）。
    // 以前的写法只认 `---`，结果 `- - -` 被当成列表项，后面整段表格就跟着渲染不出来。
    private static readonly Regex HrRegex = new(
        @"^\s*((?:-\s*){3,}|(?:\*\s*){3,}|(?:_\s*){3,})\s*$", RegexOptions.Compiled);
    private static readonly Regex ListRegex = new(@"^(\s*)([-*+]|\d+[.)])\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex ImageOnlyRegex = new(@"^\s*!\[([^\]]*)\]\(([^)\s]+)(?:\s+""[^""]*"")?\)\s*$", RegexOptions.Compiled);
    private static readonly Regex FenceRegex = new(@"^(```|~~~)(.*)$", RegexOptions.Compiled);

    // 参考式链接的定义行：[GOOGL]: https://example.com "标题"
    // 这行在预览里不应该显示，只提供地址给正文里的 [Google][GOOGL]。
    private static readonly Regex ReferenceDefRegex = new(
        @"^\s*\[([^\]]+)\]:\s*(\S+)(?:\s+""[^""]*"")?\s*$", RegexOptions.Compiled);

    // 脚注定义行：[^1]: 这里是脚注说明。
    // 必须先于参考式链接处理，否则 [^1] 会被当成“键为 ^1 的参考链接”，
    // 正文里显示成一条指不到正确地址的链接，脚注就永远渲染不出来。
    private static readonly Regex FootnoteDefRegex = new(
        @"^\s*\[\^([^\]]+)\]:\s*(.*)$", RegexOptions.Compiled);

    /// <summary>解析入口：返回块列表（可以直接给渲染器用）。</summary>
    public static List<MdBlock> Parse(string markdown)
    {
        var blocks = new List<MdBlock>();
        if (string.IsNullOrEmpty(markdown)) return blocks;

        string text = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = text.Split('\n');

        // 先把参考式链接的“定义行”收集成字典：`[GOOGL]: https://…`。
        // 定义行本身不参与渲染（标准 Markdown 也不会把它显示出来），
        // 正文里的 [Google][GOOGL] 靠它换成真正的链接。
        var references = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var footnotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var footnoteLines = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string rawLine = lines[lineIndex];
            // 脚注要先认，不能再落进参考式链接的字典里。
            var noteMatch = FootnoteDefRegex.Match(rawLine);
            if (noteMatch.Success)
            {
                string noteKey = noteMatch.Groups[1].Value.Trim();
                footnotes[noteKey] = noteMatch.Groups[2].Value.Trim();
                footnoteLines[noteKey] = lineIndex;
                continue;
            }

            var refMatch = ReferenceDefRegex.Match(rawLine);
            if (refMatch.Success)
                references[refMatch.Groups[1].Value.Trim()] = refMatch.Groups[2].Value.Trim();
        }

        int i = 0;

        // ---- YAML Front Matter ----
        // 文件开头两条 `---` 夹住的 key: value 是元数据，不是“分隔线 + 段落 + 分隔线”。
        // 识别出来后当成一个 yaml 代码卡片渲染，避免预览里被拆成两条横线。
        if (lines.Length >= 3 && lines[0].Trim() == "---")
        {
            int end = -1;
            for (int j = 1; j < lines.Length; j++)
            {
                if (lines[j].Trim() == "---") { end = j; break; }
                if (!System.Text.RegularExpressions.Regex.IsMatch(lines[j], @"^[A-Za-z_][A-Za-z0-9_-]*\s*:") &&
                    !string.IsNullOrWhiteSpace(lines[j]))
                {
                    end = -1;
                    break;
                }
            }
            if (end > 1)
            {
                blocks.Add(new MdBlock
                {
                    Type = MdBlockType.CodeBlock,
                    Language = "yaml",
                    Code = string.Join("\n", lines.Skip(1).Take(end - 1)),
                    SourceLine = 0
                });
                i = end + 1;
            }
        }

        while (i < lines.Length)
        {
            string line = lines[i];

            // 参考式链接的定义行只提供地址，不显示成一个普通段落。
            // 脚注定义行同理：内容统一收在文末的脚注区，不留在正文中间。
            if (FootnoteDefRegex.IsMatch(line) || ReferenceDefRegex.IsMatch(line))
            {
                i++;
                continue;
            }

            // 空行：以前直接跳过，结果预览里根本没有“空段落”这个概念。
            // 用户在预览里按回车，源码确实多了一行，但屏幕上什么都不出现，
            // 看起来就像“回车没新建行”。这里把空行也变成一个 Empty 块，
            // 由渲染器撑出固定高度，光标才能真的往下走。
            // 例外：文件末尾那个因 Split('\n') 产生的空串只是换行终止符，不是空段落。
            bool isTrailingTerminator = i == lines.Length - 1 && line.Length == 0;
            if (string.IsNullOrWhiteSpace(line))
            {
                if (!isTrailingTerminator)
                    blocks.Add(new MdBlock { Type = MdBlockType.Empty, SourceLine = i });
                i++;
                continue;
            }

            // ---- 代码块 ----
            var fence = FenceRegex.Match(line);
            if (fence.Success)
            {
                // 围栏可以是 ``` 也可以是 ~~~（Markdown.md 里两种都出现了）；
                // 结束围栏必须与开始围栏同一种符号，这样 ~~~ 块里写 ``` 不会提前结束。
                string marker = fence.Groups[1].Value;
                string language = fence.Groups[2].Value.Trim();
                int startLine = i;
                i++;
                var code = new List<string>();
                while (i < lines.Length && !lines[i].TrimStart().StartsWith(marker))
                {
                    code.Add(lines[i]);
                    i++;
                }
                i++; // 跳过结尾围栏

                blocks.Add(new MdBlock
                {
                    Type = MdBlockType.CodeBlock,
                    Language = language,
                    Fence = marker,
                    Code = string.Join("\n", code),
                    SourceLine = startLine
                });
                continue;
            }

            // ---- 标题 ----
            var heading = HeadingRegex.Match(line);
            if (heading.Success)
            {
                blocks.Add(new MdBlock
                {
                    Type = MdBlockType.Heading,
                    Level = heading.Groups[1].Length,
                    SourceLine = i
                }.WithInlines(ParseInlines(heading.Groups[2].Value, references)));
                i++;
                continue;
            }

            // ---- 分隔线 ----
            if (HrRegex.IsMatch(line))
            {
                blocks.Add(new MdBlock { Type = MdBlockType.HorizontalRule, SourceLine = i });
                i++;
                continue;
            }

            // ---- 表格（当前行 + 下一行是分隔行才算）----
            if (line.Contains('|') && i + 1 < lines.Length && IsTableSeparator(lines[i + 1]))
            {
                blocks.Add(ParseTable(lines, ref i, references));
                continue;
            }

            // ---- 引用 ----
            if (line.TrimStart().StartsWith(">"))
            {
                var block = new MdBlock { Type = MdBlockType.Quote, SourceLine = i };
                while (i < lines.Length && lines[i].TrimStart().StartsWith(">"))
                {
                    string content = lines[i].TrimStart()[1..];
                    if (content.StartsWith(" ")) content = content[1..];
                    block.QuoteLines.Add(ParseInlines(content, references));
                    i++;
                }
                blocks.Add(block);
                continue;
            }

            // ---- 列表 ----
            var listMatch = ListRegex.Match(line);
            if (listMatch.Success)
            {
                int startLine = i;
                bool ordered = char.IsDigit(listMatch.Groups[2].Value[0]);
                int startNumber = ordered
                    ? int.Parse(new string(listMatch.Groups[2].Value.TakeWhile(char.IsDigit).ToArray()))
                    : 0;

                var block = new MdBlock { Type = MdBlockType.List, StartNumber = startNumber, SourceLine = startLine };

                while (i < lines.Length)
                {
                    var m = ListRegex.Match(lines[i]);
                    if (!m.Success) break;

                    int indent = m.Groups[1].Value.Replace("\t", "  ").Length / 2;
                    string content = m.Groups[3].Value;

                    var item = new MdListItem { Indent = Math.Clamp(indent, 0, 6) };

                    // 任务列表：- [ ] 待办 / - [x] 已完成
                    if (content.StartsWith("[ ]") || content.StartsWith("[x]") || content.StartsWith("[X]"))
                    {
                        item.IsTask = true;
                        item.TaskDone = content[1] is 'x' or 'X';
                        content = content.Length > 3 ? content[3..].TrimStart() : "";
                    }

                    if (ordered)
                    {
                        var digits = new string(m.Groups[2].Value.TakeWhile(char.IsDigit).ToArray());
                        item.Number = digits.Length > 0 ? int.Parse(digits) : 0;
                    }

                    foreach (var inline in ParseInlines(content, references)) item.Inlines.Add(inline);
                    block.Items.Add(item);
                    i++;
                }

                blocks.Add(block);
                continue;
            }

            // ---- 独占一行的图片 ----
            var image = ImageOnlyRegex.Match(line);
            if (image.Success)
            {
                var block = new MdBlock { Type = MdBlockType.Image, SourceLine = i };
                block.Inlines.Add(new MdInline
                {
                    Text = image.Groups[1].Value,
                    ImageUrl = image.Groups[2].Value
                });
                blocks.Add(block);
                i++;
                continue;
            }

            // ---- 普通段落：一直收集到空行或下一个块开始 ----
            {
                int startLine = i;
                var block = new MdBlock { Type = MdBlockType.Paragraph, SourceLine = startLine };
                bool first = true;

                while (i < lines.Length)
                {
                    string current = lines[i];
                    if (string.IsNullOrWhiteSpace(current)) break;
                    if (i > startLine && IsBlockStart(current, lines, i)) break;

                    if (!first)
                        block.Inlines.Add(new MdInline { LineBreak = true });

                    foreach (var inline in ParseInlines(current, references)) block.Inlines.Add(inline);
                    first = false;
                    i++;
                }
                blocks.Add(block);
            }
        }

        // ---- 脚注区 ----
        // 正文里的 [^1] 已经在行内解析成上标标记；这里把定义按出现顺序
        // 追加到文末，预览底部就能看到“1. 说明文字”。
        // 用段落块承载，导出/大纲/搜索都还能正常工作，不会变成第二套模型。
        if (footnotes.Count > 0)
        {
            // 脚注分隔线是渲染用的合成块，源码里没有对应的一行。
            // 让它指向第一条脚注定义行，而不是全文最后一行：
            // 否则前一个空行块的 RangeOf 会延伸到脚注定义处，和新追加的脚注块重叠。
            int footnoteStartLine = footnoteLines.Count > 0
                ? footnoteLines.Values.Min()
                : Math.Max(0, lines.Length - 1);

            blocks.Add(new MdBlock
            {
                Type = MdBlockType.HorizontalRule,
                SourceLine = footnoteStartLine
            });
            foreach (var note in footnotes)
            {
                var block = new MdBlock
                {
                    Type = MdBlockType.Paragraph,
                    // 关键：脚注块必须指向它真正的定义行。
                    // 若指向全文最后一行，用户点它再按快捷键会改到不相干的源码行。
                    SourceLine = footnoteLines.TryGetValue(note.Key, out int noteLine) ? noteLine : 0,
                    IsFootnote = true
                };
                block.Inlines.Add(new MdInline
                {
                    Text = "[" + note.Key + "] ",
                    Bold = true
                });
                foreach (var inline in ParseInlines(note.Value, references))
                    block.Inlines.Add(inline);
                blocks.Add(block);
            }
        }

        return blocks;
    }

    /// <summary>判断一行是不是“新块的开始”（段落必须在这里断开）。</summary>
    private static bool IsBlockStart(string line, string[] lines, int index)
    {
        if (HeadingRegex.IsMatch(line)) return true;
        if (HrRegex.IsMatch(line)) return true;
        if (line.TrimStart().StartsWith("```")) return true;
        if (line.TrimStart().StartsWith("~~~")) return true;
        if (line.TrimStart().StartsWith(">")) return true;
        if (ListRegex.IsMatch(line)) return true;
        if (line.Contains('|') && index + 1 < lines.Length && IsTableSeparator(lines[index + 1])) return true;
        if (ImageOnlyRegex.IsMatch(line)) return true;
        if (ReferenceDefRegex.IsMatch(line)) return true;
        if (FootnoteDefRegex.IsMatch(line)) return true;
        return false;
    }

    private static bool IsTableSeparator(string line)
    {
        string t = line.Trim();
        if (!t.StartsWith("|") && !t.Contains('-')) return false;
        // 典型：| --- | :---: | ---: |
        return Regex.IsMatch(t, @"^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$");
    }

    private static MdBlock ParseTable(string[] lines, ref int index,
        Dictionary<string, string>? references)
    {
        var block = new MdBlock { Type = MdBlockType.Table, SourceLine = index };

        foreach (var cell in SplitCells(lines[index]))
        {
            string trimmed = cell.Trim();
            block.TableHeaders.Add(trimmed);
            // 单元格先解析成行内片段：**粗体**、`代码`、<u>下划线</u>、[链接](url)…
            // 字符串列表仍保留原始 Markdown，表格编辑写回源码时不会丢标记。
            block.TableHeaderInlines.Add(ParseInlines(trimmed, references));
        }
        index += 2; // 跳过表头行和分隔行

        while (index < lines.Length)
        {
            string line = lines[index];
            if (string.IsNullOrWhiteSpace(line) || !line.Contains('|')) break;

            var row = SplitCells(line).Select(c => c.Trim()).ToList();
            // 补齐/截断到表头列数，避免渲染时行列错位
            while (row.Count < block.TableHeaders.Count) row.Add("");
            if (row.Count > block.TableHeaders.Count) row = row.Take(block.TableHeaders.Count).ToList();

            block.TableRows.Add(row);
            block.TableRowInlines.Add(row.Select(cell => ParseInlines(cell, references)).ToList());
            index++;
        }
        return block;
    }

    /// <summary>把 "| a | b |" 拆成 ["a","b"]（忽略被反斜杠转义的 \|）。</summary>
    private static List<string> SplitCells(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.StartsWith("|")) trimmed = trimmed[1..];
        if (trimmed.EndsWith("|")) trimmed = trimmed[..^1];

        var cells = new List<string>();
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < trimmed.Length; i++)
        {
            char c = trimmed[i];
            if (c == '\\' && i + 1 < trimmed.Length && trimmed[i + 1] == '|')
            {
                current.Append('|');
                i++;
            }
            else if (c == '|')
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        cells.Add(current.ToString());
        return cells;
    }

    // ================================================================
    //  行内解析
    // ================================================================

    /// <summary>解析一段文字里的 **粗体**、*斜体*、`代码`、[链接](url)、~~删除线~~、图片。</summary>
    public static List<MdInline> ParseInlines(string text,
        Dictionary<string, string>? references = null)
    {
        var result = new List<MdInline>();
        ParseSegment(text, result, bold: false, italic: false, strike: false,
            underline: false, highlight: false, superscript: false, subscript: false,
            references: references);
        return Merge(result);
    }

    /// <summary>
    /// 解析链接文字，并把同一段 Url 挂到解析出来的每个行内片段上。
    /// 背景：普通链接以前直接把 label 当纯文本塞进一个 MdInline，导致
    /// [**粗体**](url) 里的星号、[`代码`](url) 里的反引号不会被解析。
    /// 现在先递归解析 label（保留外层的粗体/斜体/下划线等上下文），
    /// 再统一设置 Url，这样链接内部可以正常组合其它行内样式。
    /// </summary>
    private static void ParseLinkLabel(string label, string url, List<MdInline> output,
        bool bold, bool italic, bool strike, bool underline, bool highlight,
        bool superscript, bool subscript, Dictionary<string, string>? references)
    {
        int start = output.Count;
        ParseSegment(label, output, bold, italic, strike,
            underline, highlight, superscript, subscript, references);

        // LineBreak 不是文字节点，不该带链接地址；图片节点带 Url 时，
        // 渲染器会输出“可点击的图片”，语义上也正确。
        for (int i = start; i < output.Count; i++)
        {
            if (!output[i].LineBreak)
                output[i].Url = url;
        }
    }

    private static void ParseSegment(string text, List<MdInline> output, bool bold, bool italic, bool strike,
        bool underline, bool highlight, bool superscript, bool subscript,
        Dictionary<string, string>? references)
    {
        var buffer = new System.Text.StringBuilder();

        void Flush()
        {
            if (buffer.Length == 0) return;
            output.Add(new MdInline
            {
                Text = System.Net.WebUtility.HtmlDecode(buffer.ToString()),
                Bold = bold,
                Italic = italic,
                Strike = strike,
                Underline = underline,
                Highlight = highlight,
                Superscript = superscript,
                Subscript = subscript
            });
            buffer.Clear();
        }

        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];

            // 反斜杠转义：\* 显示成 *
            if (c == '\\' && i + 1 < text.Length)
            {
                buffer.Append(text[i + 1]);
                i += 2;
                continue;
            }

            // 图片：![alt](url)
            if (c == '!' && i + 1 < text.Length && text[i + 1] == '[')
            {
                if (TryReadLink(text, i + 1, out string alt, out string url, out int end))
                {
                    Flush();
                    output.Add(new MdInline
                    {
                        Text = alt,
                        ImageUrl = url,
                        Bold = bold,
                        Italic = italic,
                        Strike = strike,
                        Underline = underline,
                        Highlight = highlight,
                        Superscript = superscript,
                        Subscript = subscript
                    });
                    i = end;
                    continue;
                }
            }

            // 脚注引用：[^1]（后面没有圆括号，必须放在普通链接判断前面）。
            if (c == '[' && i + 1 < text.Length && text[i + 1] == '^')
            {
                int noteClose = text.IndexOf(']', i + 2);
                if (noteClose > i + 2)
                {
                    string noteId = text[(i + 2)..noteClose].Trim();
                    Flush();
                    output.Add(new MdInline
                    {
                        Text = "[" + noteId + "]",
                        Superscript = true,
                        Bold = bold,
                        Italic = italic,
                        Strike = strike,
                        Underline = underline,
                        Highlight = highlight
                    });
                    i = noteClose + 1;
                    continue;
                }
            }

            // 链接：[text](url)
            if (c == '[')
            {
                if (TryReadLink(text, i, out string label, out string url, out int end))
                {
                    Flush();
                    // 链接文字本身也可以带行内样式，例如 [**粗体链接**](url)
                    // 或 [`代码链接`](url)。旧实现把整段 label 当成纯文本，
                    // 所以 ** 和 ` 会原样显示；这里改成把 label 再走一遍行内解析，
                    // 然后给每个片段挂上同一个 Url，预览和 HTML 都能正常加粗/变代码。
                    ParseLinkLabel(label, url, output, bold, italic, strike,
                        underline, highlight, superscript, subscript, references);
                    i = end;
                    continue;
                }

                // 参考式链接：[Google][GOOGL]，地址来自后面的 [GOOGL]: https://…
                // 以前这里没实现，正文会原样显示方括号；定义行也会显示成一段源码。
                if (TryReadReference(text, i, out string refLabel, out string refId, out int refEnd))
                {
                    string lookup = refId.Length > 0 ? refId : refLabel;
                    string? refUrl = references != null ? references.GetValueOrDefault(lookup) : null;
                    if (refUrl != null)
                    {
                        Flush();
                        ParseLinkLabel(refLabel, refUrl, output, bold, italic, strike,
                            underline, highlight, superscript, subscript, references);
                        i = refEnd;
                        continue;
                    }
                }

                // 简写参考式链接：[Google]（地址同样来自 [Google]: https://…）。
                // 这种写法在 Typora / GitHub 都合法，而且没有第二个方括号，
                // 前面的 TryReadReference 会直接失败，必须在这里兜住。
                int shortClose = text.IndexOf(']', i + 1);
                if (shortClose > i + 1)
                {
                    string shortLabel = text[(i + 1)..shortClose].Trim();
                    string? shortUrl = references?.GetValueOrDefault(shortLabel);
                    if (shortUrl != null)
                    {
                        Flush();
                        ParseLinkLabel(shortLabel, shortUrl, output, bold, italic, strike,
                            underline, highlight, superscript, subscript, references);
                        i = shortClose + 1;
                        continue;
                    }
                }
            }

            // 行内代码：`code`
            if (c == '`')
            {
                int end = text.IndexOf('`', i + 1);
                if (end > i)
                {
                    Flush();
                    output.Add(new MdInline { Text = text[(i + 1)..end], Code = true });
                    i = end + 1;
                    continue;
                }
            }

            // 粗斜体：***文字*** 或 ___文字___。
            // 必须放在“加粗：**”判断之前，否则 *** 会被当成 ** + 一个落单的 *，
            // 结果导出/预览里出现 “<strong>*文字</strong>*”，星号露在外面。
            if ((c == '*' || c == '_') && i + 2 < text.Length && text[i + 1] == c && text[i + 2] == c)
            {
                string marker = new(c, 3);
                int end = text.IndexOf(marker, i + 3, StringComparison.Ordinal);
                if (end > i + 2)
                {
                    Flush();
                    ParseSegment(text[(i + 3)..end], output, true, true, strike,
                        underline, highlight, superscript, subscript, references);
                    i = end + 3;
                    continue;
                }
            }

            // 加粗：** 或 __
            if ((c == '*' || c == '_') && i + 1 < text.Length && text[i + 1] == c)
            {
                string marker = new(c, 2);
                int end = text.IndexOf(marker, i + 2, StringComparison.Ordinal);
                if (end > i + 1)
                {
                    Flush();
                    ParseSegment(text[(i + 2)..end], output, true, italic, strike,
                        underline, highlight, superscript, subscript, references);
                    i = end + 2;
                    continue;
                }
            }

            // 删除线：~~
            if (c == '~' && i + 1 < text.Length && text[i + 1] == '~')
            {
                int end = text.IndexOf("~~", i + 2, StringComparison.Ordinal);
                if (end > i + 1)
                {
                    Flush();
                    ParseSegment(text[(i + 2)..end], output, bold, italic, true,
                        underline, highlight, superscript, subscript, references);
                    i = end + 2;
                    continue;
                }
            }

            // 斜体：* 或 _
            if (c is '*' or '_')
            {
                int end = text.IndexOf(c, i + 1);
                if (end > i + 1 && end - i < 200)
                {
                    Flush();
                    ParseSegment(text[(i + 1)..end], output, bold, true, strike,
                        underline, highlight, superscript, subscript, references);
                    i = end + 1;
                    continue;
                }
            }

            // HTML 行内标签：<u>下划线</u>、<mark>高亮</mark>、<sup>上标</sup>、<sub>下标</sub>。
            // 这四个标签在 Markdown 里很常见，Typora 也会直接渲染。
            // 解析器必须把“标签”吃掉、只留下里面的文字，并给片段打上对应样式标记，
            // 否则预览里看到的是一串原始标签。
            if (c == '<')
            {
                string[] inlineTags = { "u", "mark", "sup", "sub" };
                foreach (string tag in inlineTags)
                {
                    string open = "<" + tag + ">";
                    if (!text.AsSpan(i).StartsWith(open, StringComparison.OrdinalIgnoreCase)) continue;

                    int contentStart = i + open.Length;
                    int close = text.IndexOf("</" + tag + ">", contentStart, StringComparison.OrdinalIgnoreCase);
                    if (close < 0) continue;

                    Flush();
                    ParseSegment(
                        text[contentStart..close], output, bold, italic, strike,
                        underline || tag == "u",
                        highlight || tag == "mark",
                        superscript || tag == "sup",
                        subscript || tag == "sub",
                        references);
                    i = close + tag.Length + 3;   // </tag> 的长度
                    goto nextCharacter;
                }
            }

            // 自动链接：<https://example.com>
            if (c == '<')
            {
                int end = text.IndexOf('>', i + 1);
                if (end > i + 1)
                {
                    string inside = text[(i + 1)..end];
                    if (inside.StartsWith("http://") || inside.StartsWith("https://"))
                    {
                        Flush();
                        output.Add(new MdInline { Text = inside, Url = inside, Bold = bold, Italic = italic });
                        i = end + 1;
                        continue;
                    }
                }
            }

            // <br> 换成换行
            if (c == '<' && text.AsSpan(i).StartsWith("<br"))
            {
                int end = text.IndexOf('>', i);
                if (end > i)
                {
                    Flush();
                    output.Add(new MdInline { LineBreak = true });
                    i = end + 1;
                    continue;
                }
            }

            buffer.Append(c);
            i++;
        nextCharacter:;
        }

        Flush();
    }

    /// <summary>解析 [文字](地址)，成功返回 true 并把结束位置写到 end。</summary>
    private static bool TryReadLink(string text, int start, out string label, out string url, out int end)
    {
        label = "";
        url = "";
        end = start;

        int closeBracket = text.IndexOf(']', start + 1);
        if (closeBracket < 0) return false;
        if (closeBracket + 1 >= text.Length || text[closeBracket + 1] != '(') return false;

        int closeParen = text.IndexOf(')', closeBracket + 2);
        if (closeParen < 0) return false;

        label = text[(start + 1)..closeBracket];
        string rawUrl = text[(closeBracket + 2)..closeParen].Trim();

        // 去掉 "url "title"" 里的标题部分
        int space = rawUrl.IndexOf(' ');
        if (space > 0) rawUrl = rawUrl[..space];

        url = rawUrl;
        end = closeParen + 1;
        return true;
    }

    /// <summary>读参考式链接的 `[文字][标识]`，只负责切分，地址在调用方查字典。</summary>
    private static bool TryReadReference(string text, int start, out string label, out string id, out int end)
    {
        label = "";
        id = "";
        end = start;

        int firstClose = text.IndexOf(']', start + 1);
        if (firstClose < 0) return false;
        if (firstClose + 1 >= text.Length || text[firstClose + 1] != '[') return false;

        int secondClose = text.IndexOf(']', firstClose + 2);
        if (secondClose < 0) return false;

        label = text[(start + 1)..firstClose];
        id = text[(firstClose + 2)..secondClose].Trim();
        end = secondClose + 1;
        return true;
    }

    /// <summary>把相邻同样式片段合并，减少渲染时的控件数量。</summary>
    private static List<MdInline> Merge(List<MdInline> list)
    {
        var result = new List<MdInline>();
        foreach (var item in list)
        {
            if (!item.LineBreak && item.Url == null && item.ImageUrl == null)
            {
                var last = result.LastOrDefault();
                if (last != null &&
                    !last.LineBreak && last.Url == null && last.ImageUrl == null &&
                    last.Bold == item.Bold && last.Italic == item.Italic && last.Strike == item.Strike && last.Code == item.Code &&
                    last.Underline == item.Underline && last.Highlight == item.Highlight &&
                    last.Superscript == item.Superscript && last.Subscript == item.Subscript)
                {
                    last.Text += item.Text;
                    continue;
                }
            }
            result.Add(item);
        }
        return result;
    }

    /// <summary>提取标题大纲（左侧栏 + 跳转用）。</summary>
    public static List<MdOutlineItem> ExtractOutline(string markdown)
    {
        var items = new List<MdOutlineItem>();
        if (string.IsNullOrEmpty(markdown)) return items;

        string text = markdown.Replace("\r\n", "\n");
        int lineNumber = 1;
        int charIndex = 0;
        // 当前所在的代码围栏字符（'`' 或 '~'），不在代码块里时是 '\0'。
        //
        // 这里以前只认三个反引号、而且见到就取反，于是：
        //   ① `~~~` 开的代码块完全不被当成代码块；
        //   ② 块里那行 “``` 内容 ```” 会被当成“代码块结束”，
        //      紧接着真正结束的 ``` 又被当成“开始”，状态整个反过来；
        //   ③ 后面所有标题都被判成“在代码块里”，大纲从「代码区块」之后全丢。
        // 修法：反引号、波浪号两种围栏都认，并且要求“同一种字符、至少三个、
        // 后面除了空白没有别的内容”才算结束。这样块内出现的另一种围栏不会误伤。
        char fenceChar = '\0';

        foreach (var line in text.Split('\n'))
        {
            string trimmed = line.TrimStart();

            if (fenceChar == '\0')
            {
                if (trimmed.StartsWith("```")) fenceChar = '`';
                else if (trimmed.StartsWith("~~~")) fenceChar = '~';
            }
            else
            {
                // 结束围栏：整行（去掉行尾空白）都是同一种围栏字符，且至少三个。
                string endPart = trimmed.TrimEnd();
                bool allSame = endPart.Length >= 3;
                for (int i = 0; i < endPart.Length && allSame; i++)
                {
                    if (endPart[i] != fenceChar) allSame = false;
                }
                if (allSame) fenceChar = '\0';
            }

            if (fenceChar == '\0')
            {
                var m = HeadingRegex.Match(line);
                if (m.Success)
                {
                    items.Add(new MdOutlineItem
                    {
                        Level = m.Groups[1].Length,
                        Text = m.Groups[2].Value.Trim(),
                        CharIndex = charIndex,
                        LineNumber = lineNumber
                    });
                }
            }
            charIndex += line.Length + 1; // +1 是换行符
            lineNumber++;
        }
        return items;
    }

    /// <summary>统计字数：英文按单词、中文按字符（和 Typora 的统计口径接近）。</summary>
    public static (int Words, int Chars, int Lines) Count(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return (0, 0, 0);

        int chars = 0, words = 0;
        bool inWord = false;

        foreach (char c in markdown)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
                continue;
            }
            chars++;
            if (c >= 0x4E00 && c <= 0x9FFF) // 常用汉字区
            {
                words++;
                inWord = false;
            }
            else if (char.IsLetterOrDigit(c))
            {
                if (!inWord)
                {
                    words++;
                    inWord = true;
                }
            }
            else
            {
                inWord = false;
            }
        }

        int lines = markdown.Replace("\r\n", "\n").Split('\n').Length;
        return (words, chars, lines);
    }

    /// <summary>小工具：给块填行内内容（让 Parse 里的写法更短）。</summary>
    internal static MdBlock WithInlines(this MdBlock block, List<MdInline> inlines)
    {
        block.Inlines.AddRange(inlines);
        return block;
    }
}
