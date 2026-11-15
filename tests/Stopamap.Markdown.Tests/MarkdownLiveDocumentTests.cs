using System.Linq;
using Stopamap.Markdown;
using Xunit;

namespace Stopamap.Markdown.Tests;

public sealed class MarkdownLiveDocumentTests
{
    [Fact]
    public void NormalizesCRLFAndCreatesStableLineIndex()
    {
        var document = MarkdownLiveDocument.Create("A\r\n\r\nB");

        Assert.Equal("A\n\nB", document.Text);
        Assert.Equal(0, document.LineStart(0));
        Assert.Equal(2, document.LineStart(1));
        Assert.Equal(3, document.LineStart(2));
        Assert.Equal(2, document.LineOfOffset(3));
        Assert.Equal(4, document.LineEnd(2));
    }

    [Fact]
    public void BlockRangesAreContiguousAndContentEndTrimsNewlines()
    {
        var document = MarkdownLiveDocument.Create("A\n\nB");
        var first = document.Blocks[0];
        var empty = document.Blocks[1];
        var last = document.Blocks[2];

        Assert.Equal((0, 2), document.RangeOf(first));
        Assert.Equal(1, document.ContentEnd(first));
        Assert.Equal((2, 3), document.RangeOf(empty));
        Assert.Equal((3, 4), document.RangeOf(last));
        Assert.Equal(document.Text.Length, document.RangeOf(last).End);
    }

    [Fact]
    public void BlankRunAtFindsConsecutiveBlankLines()
    {
        var document = MarkdownLiveDocument.Create("A\n\n\nB");

        Assert.Equal((2, 4), document.BlankRunAt(2));
        Assert.Null(document.BlankRunAt(0));
    }

    [Fact]
    public void FootnoteSyntheticRuleDoesNotOverlapTheFootnoteBlock()
    {
        var document = MarkdownLiveDocument.Create("A\n\n[^1]: note\n");
        var footnote = document.Blocks.Single(b => b.IsFootnote);
        var rule = document.Blocks.Single(b => b.Type == MdBlockType.HorizontalRule);

        Assert.Equal(2, rule.SourceLine);
        Assert.Equal(document.LineStart(2), document.RangeOf(rule).Start);
        Assert.Equal(document.LineStart(2), document.RangeOf(footnote).Start);

        int previousStart = -1;
        foreach (var block in document.Blocks)
        {
            var start = document.RangeOf(block).Start;
            Assert.True(start >= previousStart, "block ranges should not move backwards");
            previousStart = start;
        }
    }

    [Fact]
    public void NextContentStartSkipsEmptyBlocks()
    {
        var document = MarkdownLiveDocument.Create("A\n\nB");

        Assert.Equal(3, document.NextContentStart(document.Blocks[0]));
        Assert.Equal(3, document.NextContentStart(document.Blocks[1]));
    }
}
