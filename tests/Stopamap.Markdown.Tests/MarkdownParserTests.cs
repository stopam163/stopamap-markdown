using System.Linq;
using Stopamap.Markdown;
using Xunit;

namespace Stopamap.Markdown.Tests;

public sealed class MarkdownParserTests
{
    [Fact]
    public void ParsesHeadingParagraphListAndBlankLines()
    {
        var blocks = MarkdownParser.Parse("# Title\n\nHello **world** and `code`.\n\n- one\n- two\n");

        Assert.Equal(5, blocks.Count);
        Assert.Equal(MdBlockType.Heading, blocks[0].Type);
        Assert.Equal(1, blocks[0].Level);
        Assert.Equal(MdBlockType.Empty, blocks[1].Type);
        Assert.Equal(MdBlockType.Paragraph, blocks[2].Type);
        Assert.Equal(MdBlockType.Empty, blocks[3].Type);
        Assert.Equal(MdBlockType.List, blocks[4].Type);
        Assert.Equal(2, blocks[4].Items.Count);
        Assert.True(blocks[4].Items[0].Inlines.Single(i => i.Text == "one").Text == "one");
    }

    [Fact]
    public void ParsesTripleEmphasisWithoutLeakingAsterisks()
    {
        var inlines = MarkdownParser.ParseInlines("***both***");

        Assert.Single(inlines);
        Assert.Equal("both", inlines[0].Text);
        Assert.True(inlines[0].Bold);
        Assert.True(inlines[0].Italic);
    }

    [Fact]
    public void ParsesBoldLinkLabelWithoutShowingMarkdownMarkers()
    {
        var inlines = MarkdownParser.ParseInlines("[**Bold link**](https://example.com)");

        Assert.Single(inlines);
        Assert.Equal("Bold link", inlines[0].Text);
        Assert.True(inlines[0].Bold);
        Assert.Equal("https://example.com", inlines[0].Url);
    }

    [Fact]
    public void ParsesTableCellsIntoInlineRuns()
    {
        var blocks = MarkdownParser.Parse(
            "| Name | Value |\n" +
            "| --- | --- |\n" +
            "| **bold** | `code` |\n");

        Assert.Single(blocks);
        var table = blocks[0];
        Assert.Equal(MdBlockType.Table, table.Type);
        Assert.Equal(2, table.TableHeaders.Count);
        Assert.Equal("Name", table.TableHeaderInlines[0][0].Text);
        Assert.Single(table.TableRows);
        Assert.True(table.TableRowInlines[0][0][0].Bold);
        Assert.True(table.TableRowInlines[0][1][0].Code);
    }

    [Fact]
    public void ParsesNestedListsAndTaskItems()
    {
        var blocks = MarkdownParser.Parse(
            "- [x] done\n" +
            "  - child\n" +
            "- [ ] todo\n");

        Assert.Single(blocks);
        Assert.Equal(3, blocks[0].Items.Count);
        Assert.True(blocks[0].Items[0].IsTask);
        Assert.True(blocks[0].Items[0].TaskDone);
        Assert.Equal(1, blocks[0].Items[1].Indent);
        Assert.True(blocks[0].Items[2].IsTask);
        Assert.False(blocks[0].Items[2].TaskDone);
    }

    [Fact]
    public void ExtractsOutlineWithLevelsAndOffsets()
    {
        var outline = MarkdownParser.ExtractOutline("# One\n\n## Two\n");

        Assert.Equal(2, outline.Count);
        Assert.Equal(1, outline[0].Level);
        Assert.Equal("One", outline[0].Text);
        Assert.Equal(0, outline[0].CharIndex);
        Assert.Equal(2, outline[1].Level);
        Assert.Equal("Two", outline[1].Text);
        Assert.Equal(7, outline[1].CharIndex);
    }
}
