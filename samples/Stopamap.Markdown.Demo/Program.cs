using System;
using System.IO;
using System.Linq;
using System.Text;
using Stopamap.Markdown;

Console.OutputEncoding = Encoding.UTF8;

string markdown = args.Length > 0
    ? File.ReadAllText(args[0])
    : """
      # Stopamap.Markdown

      A **source-mapped** Markdown engine for *live* editors.

      - `MarkdownParser` turns source into blocks and inline runs.
      - `MarkdownLiveDocument` keeps line and block ranges in sync.

      | Layer | Responsibility |
      | --- | --- |
      | Parser | Source → structured blocks |
      | Live document | Source offset → line/block |

      > The renderer is intentionally not included.
      """;

var document = MarkdownLiveDocument.Create(markdown);
var count = MarkdownParser.Count(markdown);

Console.WriteLine("Stopamap.Markdown demo");
Console.WriteLine($"chars={count.Chars} words={count.Words} lines={count.Lines} blocks={document.Blocks.Count}");
Console.WriteLine();
Console.WriteLine("Blocks:");
for (int i = 0; i < document.Blocks.Count; i++)
{
    var block = document.Blocks[i];
    var range = document.RangeOf(block);
    Console.WriteLine(
        $"  {i,2}  {block.Type,-14} line={block.SourceLine,2} range=[{range.Start,4},{range.End,4})");
}

Console.WriteLine();
Console.WriteLine("Outline:");
foreach (var item in MarkdownParser.ExtractOutline(markdown))
    Console.WriteLine($"  {new string(' ', (item.Level - 1) * 2)}{item.Text} (char {item.CharIndex})");

Console.WriteLine();
Console.WriteLine("Inline runs in first paragraph:");
var paragraph = document.Blocks.FirstOrDefault(b => b.Type == MdBlockType.Paragraph);
if (paragraph is null)
{
    Console.WriteLine("  (none)");
}
else
{
    foreach (var inline in paragraph.Inlines)
        Console.WriteLine($"  \"{inline.Text}\" bold={inline.Bold} italic={inline.Italic} code={inline.Code}");
}
