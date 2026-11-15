// ============================================================================
//  Markdown/MarkdownLiveDocument.cs —— 实时渲染的“唯一坐标系”
// ============================================================================
//
//  这个文件是 2.84 之后对“实时渲染核心”的重写部分。
//  它不负责画控件，只负责三件最容易出错、也最应该只做一次的事：
//
//    1. 把原文统一成 LF。Windows 的 TextBox、文件、剪贴板都可能带 CRLF，
//       而预览、选区、源码替换全部按字符下标计算；只要两套坐标系混用，
//       每经过一行就会差 1 个字符，累积后就会出现：
//         · 一次回车看起来多出空行
//         · 代码围栏被吞、正文被卷进代码块
//         · 方向键跨块后乱码、焦点掉到窗口
//       所以整个预览只认这里的 Text，别处不得再自行 Replace。
//
//    2. 缓存每一行的起始下标。以前每次查找块、定位光标、换算空行都要
//       从文件头 IndexOf 一遍，既慢又容易把“行号”和“字符下标”混在一起。
//       这里一次算好 LineStarts，后续所有操作都用 O(1) 的行起点。
//
//    3. 给每个 MdBlock 一个明确的源码范围：
//         BlockStart = 这一块第一行的行首
//         BlockEnd   = 下一块第一行的行首（最后一块就是全文长度）
//       注意：这个范围包含块尾用于分隔下一块的换行符。
//       真正“可编辑的文字”到 ContentEnd 为止（去掉尾部连续换行）。
//       预览里的 Enter、Delete、方向键都只在这个范围里做替换，
//       不再用“块长度 + 猜几个 \n”这种容易叠空行的算法。
//
//  样式与具体控件留在下游渲染适配器里，本库只负责无误地定位源码范围。
//  这里只换“怎么找到要改的那几个字符”，不改任何字号/颜色/间距，
//  所以已经调好的显示效果不会被这次重写推翻。
// ============================================================================

using System;
using System.Collections.Generic;

namespace Stopamap.Markdown;

/// <summary>
/// 一份用于实时渲染的 Markdown 快照。
/// 不可变：每次原文变化都重新 Create 一份，避免编辑过程中旧下标继续被使用。
/// </summary>
public sealed class MarkdownLiveDocument
{
    /// <summary>已统一成 LF 的全文。所有下标都以这个字符串为准。</summary>
    public string Text { get; }

    /// <summary>解析出来的块列表。顺序与渲染顺序一致。</summary>
    public IReadOnlyList<MdBlock> Blocks { get; }

    /// <summary>每一行第一个字符在 Text 里的下标；最后一项是 Text.Length，方便求行尾。</summary>
    private readonly int[] _lineStarts;

    private MarkdownLiveDocument(string text, IReadOnlyList<MdBlock> blocks, int[] lineStarts)
    {
        Text = text;
        Blocks = blocks;
        _lineStarts = lineStarts;
    }

    /// <summary>
    /// 从任意换行风格的 Markdown 建一份快照。
    /// 这是全工程唯一允许做 CRLF/CR → LF 的地方之一（另一个是 MainWindow 保存前）。
    /// </summary>
    public static MarkdownLiveDocument Create(string? markdown)
    {
        string text = (markdown ?? "")
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        var blocks = MarkdownParser.Parse(text);
        return new MarkdownLiveDocument(text, blocks, BuildLineStarts(text));
    }

    private static int[] BuildLineStarts(string text)
    {
        // 先数行数，避免 List 扩容；空文档也要有一个 0。
        int count = 1;
        for (int i = 0; i < text.Length; i++)
            if (text[i] == '\n') count++;

        var starts = new int[count + 1];
        int line = 1;
        starts[0] = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                starts[line++] = i + 1;
        }
        starts[count] = text.Length;
        return starts;
    }

    /// <summary>取第 line 行（0 开始）的行首下标；越界时夹到全文范围。</summary>
    public int LineStart(int line)
    {
        if (line <= 0) return 0;
        if (line >= _lineStarts.Length) return Text.Length;
        return _lineStarts[line];
    }

    /// <summary>第 line 行结束后的第一个字符下标（也就是下一行行首）。</summary>
    public int LineEnd(int line)
    {
        if (line < 0) return 0;
        if (line + 1 >= _lineStarts.Length) return Text.Length;
        return _lineStarts[line + 1];
    }

    /// <summary>块在源码里的完整区间 [Start, End)，包含块尾分隔下一块的换行。</summary>
    public (int Start, int End) RangeOf(MdBlock block)
    {
        int start = LineStart(block.SourceLine);
        int index = IndexOf(block);
        int nextLine = index >= 0 && index + 1 < Blocks.Count
            ? Blocks[index + 1].SourceLine
            : -1;
        int end = nextLine < 0 ? Text.Length : LineStart(nextLine);
        return (start, Math.Max(start, end));
    }

    /// <summary>
    /// 块内容真正结束的位置：去掉块尾所有空行/换行。
    /// 例：`第一行\n\n` 的块区间到第二行行首，ContentEnd 落在“行”字之后。
    /// </summary>
    public int ContentEnd(MdBlock block)
    {
        var (start, end) = RangeOf(block);
        int cut = end;
        while (cut > start && Text[cut - 1] == '\n') cut--;
        return cut;
    }

    /// <summary>找块在列表里的下标；找不到返回 -1。</summary>
    public int IndexOf(MdBlock block)
    {
        for (int i = 0; i < Blocks.Count; i++)
            if (ReferenceEquals(Blocks[i], block)) return i;
        return -1;
    }

    /// <summary>
    /// 从某个块往后找“下一块真正有内容的块”的行首。
    /// 中间的 Empty 空行全部跳过，返回的是下一段正文的第一行。
    /// 没有下一段时返回 Text.Length。
    /// </summary>
    public int NextContentStart(MdBlock block)
    {
        int index = IndexOf(block);
        if (index < 0) return Text.Length;

        for (int i = index + 1; i < Blocks.Count; i++)
        {
            if (Blocks[i].Type != MdBlockType.Empty)
                return LineStart(Blocks[i].SourceLine);
        }
        return Text.Length;
    }

    /// <summary>
    /// 光标所在空行对应的“整段连续空行”区间 [From, To)。
    ///
    /// 例：`A\n\n\nB`（A 后面有两条空行）
    ///   A        line0 行首 0
    ///   空行 1   line1 行首 2
    ///   空行 2   line2 行首 3
    ///   B        line3 行首 4
    /// 返回 [2,4)。删除这两个换行后变成 `A\nB`：
    /// 上一行 A 自己的换行保留，不会把 A 和 B 粘成一行。
    ///
    /// 如果光标不在空行上，返回 null。
    /// </summary>
    public (int From, int To)? BlankRunAt(int offset)
    {
        offset = Math.Clamp(offset, 0, Text.Length);
        int line = LineOfOffset(offset);
        if (!IsBlankLine(line)) return null;

        int first = line;
        while (first - 1 >= 0 && IsBlankLine(first - 1)) first--;
        int last = line;
        // _lineStarts 的最后一个哨兵是 Text.Length，所以 last 最多到“最后一个可判断行”。
        while (last + 1 < _lineStarts.Length - 1 && IsBlankLine(last + 1)) last++;

        int from = LineStart(first);
        int to = LineEnd(last);
        return (from, Math.Max(from, to));
    }

    /// <summary>offset 落在第几行（0 开始）。</summary>
    public int LineOfOffset(int offset)
    {
        offset = Math.Clamp(offset, 0, Text.Length);
        int lo = 0, hi = _lineStarts.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (_lineStarts[mid] <= offset) lo = mid;
            else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>第 line 行是不是空行（整行只有换行，或就是文末空行）。</summary>
    private bool IsBlankLine(int line)
    {
        if (line < 0 || line >= _lineStarts.Length - 1) return false;
        int start = _lineStarts[line];
        int end = _lineStarts[line + 1];
        // 行内容不包含结尾换行；start == end 说明这是文末哨兵行，不算空行。
        if (start >= end) return false;
        for (int i = start; i < end; i++)
        {
            char c = Text[i];
            if (c != '\n' && c != '\r') return false;
        }
        return true;
    }
}
