using System.Linq;
using NUnit.Framework;

namespace ThanhDV.PackageManager.Tests
{
    public class HtmlToRichTextTests
    {
        private const string Code = "#C0DE00";
        private const string Link = "#0000FF";

        private static string[] Blocks(string html) =>
            HtmlToRichText.Convert(html, Code, Link).Select(b => b.ToString()).ToArray();

        [Test]
        public void Paragraphs_AreSeparateBlocks() =>
            CollectionAssert.AreEqual(new[] { "Paragraph: One", "Paragraph: Two" }, Blocks("<p>One</p>\n<p>Two</p>"));

        [Test]
        public void InlineFormatting_BecomesRichTextTags() =>
            CollectionAssert.AreEqual(new[] { "Paragraph: <b>bold</b> and <i>it</i>" }, Blocks("<p><strong>bold</strong> and <em>it</em></p>"));

        [Test]
        public void Headings_KeepTheirLevel() =>
            CollectionAssert.AreEqual(new[] { "Heading1: Title", "Heading2: Sub", "Heading3: Small", "Paragraph: Text" },
                Blocks("<h1>Title</h1><h2>Sub</h2><h4>Small</h4><p>Text</p>"));

        [Test]
        public void UnorderedList_IsOneBlockWithBullets() =>
            CollectionAssert.AreEqual(new[] { "List: • a\n• b" }, Blocks("<ul>\n<li>a</li>\n<li>b</li>\n</ul>"));

        [Test]
        public void OrderedList_IsNumbered() =>
            CollectionAssert.AreEqual(new[] { "List: 1. a\n2. b" }, Blocks("<ol><li>a</li><li>b</li></ol>"));

        [Test]
        public void LooseListItems_StayOnTheirBulletLine() =>
            CollectionAssert.AreEqual(new[] { "List: • a\n• b" }, Blocks("<ul>\n<li><p>a</p></li>\n<li><p>b</p></li>\n</ul>"));

        [Test]
        public void NestedList_IsIndented() =>
            CollectionAssert.AreEqual(new[] { "List: • a\n    • b" }, Blocks("<ul><li>a<ul><li>b</li></ul></li></ul>"));

        [Test]
        public void Entities_AreDecoded_AndLiteralTagsCannotBecomeRichText() =>
            CollectionAssert.AreEqual(new[] { "Paragraph: List<​T> & more" }, Blocks("<p>List&lt;T&gt; &amp; more</p>"));

        [Test]
        public void Whitespace_IsCollapsedOutsidePre() =>
            CollectionAssert.AreEqual(new[] { "Paragraph: a b" }, Blocks("<p>  a \n\n  b </p>"));

        [Test]
        public void Pre_IsACodeBlock_KeepingLineBreaks() =>
            CollectionAssert.AreEqual(new[] { "Paragraph: Use:", "Code: line1\n\n  line2", "Paragraph: Done" },
                Blocks("<p>Use:</p><pre><code>line1\n\n  line2\n</code></pre><p>Done</p>"));

        [Test]
        public void InlineCodeAndLinks_UseTheGivenColors() =>
            CollectionAssert.AreEqual(new[] { $"Paragraph: Call <color={Code}>Run()</color>, see <color={Link}><u>docs</u></color>" },
                Blocks("<p>Call <code>Run()</code>, see <a href=\"https://x\" target=\"_blank\">docs</a><img src=\"https://x/a.png\" alt=\"a\"></p>"));

        [Test]
        public void Table_RowsBecomeLines_HeaderBold() =>
            CollectionAssert.AreEqual(new[] { "Table: <b>A | B</b>\n1 | 2" },
                Blocks("<table>\n<tr>\n<th>A</th>\n<th>B</th>\n</tr>\n<tr><td>1</td><td>2</td></tr></table>"));

        [Test]
        public void HorizontalRule_IsItsOwnBlock() =>
            CollectionAssert.AreEqual(new[] { "Paragraph: a", "Rule: ", "Paragraph: b" }, Blocks("<p>a</p><hr><p>b</p>"));

        [Test]
        public void Emoji_AreDropped_ArrowsKept() =>
            CollectionAssert.AreEqual(new[] { "Paragraph: <b>Feel</b> is juicy. Player → Settings" },
                Blocks("<p>\U0001F525 <b>Feel</b> is juicy. ⚡️ Player → Settings</p>"));

        [Test]
        public void Empty_ReturnsNoBlocks() => CollectionAssert.IsEmpty(HtmlToRichText.Convert(null, Code, Link));
    }
}
