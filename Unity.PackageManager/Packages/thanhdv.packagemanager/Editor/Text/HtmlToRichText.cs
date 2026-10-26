using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ThanhDV.PackageManager
{
    internal enum RichKind
    {
        Paragraph,
        Heading1,
        Heading2,
        Heading3,
        List,
        Code,
        Table,
        Rule,
    }

    /// <summary>One block of a description: the view gives each kind its own Label and spacing.</summary>
    internal readonly struct RichBlock
    {
        public readonly RichKind Kind;
        /// <summary>UI Toolkit rich text (empty for <see cref="RichKind.Rule"/>).</summary>
        public readonly string Text;

        public RichBlock(RichKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public override string ToString() => $"{Kind}: {Text}";
    }

    /// <summary>
    /// Turns the server's sanitized HTML (Asset Store descriptions, READMEs) into blocks of UI Toolkit rich text.
    /// The input is already well-formed, so a tag tokenizer is enough. Images are dropped, tables become " | " rows
    /// with the first row bold. Colors come in as hex because rich text cannot use USS variables.
    /// </summary>
    internal static class HtmlToRichText
    {
        private static readonly Regex Token = new Regex(
            @"<!--.*?-->|<(?<close>/?)(?<tag>[a-zA-Z][a-zA-Z0-9]*)\b[^>]*>|(?<text>[^<]+)|(?<text><)",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);
        // The editor font has no emoji (Asset Store descriptions are full of them): they would show as empty boxes.
        // ✓ (U+2713) is kept: the editor font has it.
        private static readonly Regex Emoji = new Regex(@"[\uD800-\uDFFF☀-✒✔-➿⬀-⯿️‍]", RegexOptions.Compiled);
        private static readonly Regex SpaceBeforeNewline = new Regex(@"[ \t]+\n", RegexOptions.Compiled);
        private static readonly Regex BlankLines = new Regex(@"\n{2,}", RegexOptions.Compiled);

        public static List<RichBlock> Convert(string html, string codeColor, string linkColor)
        {
            var w = new Writer();
            if (string.IsNullOrWhiteSpace(html)) return w.Blocks;

            // -1 = <ul>, otherwise the next <ol> number.
            var lists = new Stack<int>();
            int pre = 0, cell = 0;

            foreach (Match m in Token.Matches(html))
            {
                if (m.Groups["text"].Success)
                {
                    w.Text(m.Groups["text"].Value, pre > 0);
                    continue;
                }
                if (!m.Groups["tag"].Success) continue; // comment

                string tag = m.Groups["tag"].Value.ToLowerInvariant();
                bool close = m.Groups["close"].Value == "/";
                switch (tag)
                {
                    case "h1": w.Heading(close, RichKind.Heading1); break;
                    case "h2": w.Heading(close, RichKind.Heading2); break;
                    case "h3": case "h4": case "h5": case "h6": w.Heading(close, RichKind.Heading3); break;
                    case "p": case "div": case "blockquote": case "section": case "article":
                    case "header": case "footer": case "figure": case "details": case "summary": case "dl":
                        // Inside a list (Markdig's loose lists wrap items in <p>) the item stays on its line.
                        if (lists.Count == 0) w.Flush(RichKind.Paragraph);
                        break;
                    case "br": w.Raw("\n"); break;
                    case "hr": w.Rule(lists.Count > 0 ? RichKind.List : RichKind.Paragraph); break;
                    case "pre":
                        w.Flush(close ? (lists.Count > 0 ? RichKind.List : RichKind.Paragraph) : RichKind.Code);
                        pre = Math.Max(0, pre + (close ? -1 : 1));
                        break;
                    case "table":
                        w.Flush(close ? RichKind.Paragraph : RichKind.Table);
                        break;
                    case "ul": case "ol":
                        if (close)
                        {
                            if (lists.Count > 0) lists.Pop();
                            if (lists.Count == 0) w.Flush(RichKind.Paragraph); else w.Line();
                        }
                        else
                        {
                            if (lists.Count == 0) w.Flush(RichKind.List); else w.Line();
                            lists.Push(tag == "ul" ? -1 : 1);
                        }
                        break;
                    case "li":
                        if (close) break;
                        string marker = "• ";
                        if (lists.Count > 0 && lists.Peek() > 0)
                        {
                            int n = lists.Pop();
                            marker = n + ". ";
                            lists.Push(n + 1);
                        }
                        w.Line();
                        w.Raw(new string(' ', 4 * Math.Max(0, lists.Count - 1)) + marker);
                        break;
                    case "dt": case "dd": case "tr":
                        w.Line();
                        cell = 0;
                        break;
                    case "td": case "th":
                        if (!close && cell++ > 0) w.CellSeparator();
                        break;
                    case "b": case "strong": w.Raw(close ? "</b>" : "<b>"); break;
                    case "i": case "em": w.Raw(close ? "</i>" : "<i>"); break;
                    case "u": case "ins": w.Raw(close ? "</u>" : "<u>"); break;
                    case "s": case "del": case "strike": w.Raw(close ? "</s>" : "<s>"); break;
                    case "code":
                        if (pre == 0) w.Raw(close ? "</color>" : $"<color={codeColor}>");
                        break;
                    case "a": w.Raw(close ? "</u></color>" : $"<color={linkColor}><u>"); break;
                }
            }
            w.Flush(RichKind.Paragraph);
            return w.Blocks;
        }

        private sealed class Writer
        {
            private readonly StringBuilder _sb = new StringBuilder();
            private RichKind _kind = RichKind.Paragraph;

            public readonly List<RichBlock> Blocks = new List<RichBlock>();

            public void Raw(string s) => _sb.Append(s);

            public void Text(string raw, bool preformatted)
            {
                string text = Emoji.Replace(WebUtility.HtmlDecode(raw), "");
                if (preformatted) text = text.Replace("\r\n", "\n");
                else
                {
                    text = Whitespace.Replace(text, " ");
                    if (_sb.Length == 0 || Last == '\n' || Last == ' ') text = text.TrimStart();
                }
                // A zero-width space after "<" keeps literal text such as "List<T>" from being read as a rich text tag.
                _sb.Append(text.Replace("<", "<​"));
            }

            public void Heading(bool close, RichKind kind) => Flush(close ? RichKind.Paragraph : kind);

            public void Rule(RichKind after)
            {
                Flush(after);
                Blocks.Add(new RichBlock(RichKind.Rule, ""));
            }

            /// <summary>Ends the current block; what follows goes into a block of <paramref name="next"/>.</summary>
            public void Flush(RichKind next)
            {
                string text = _sb.ToString();
                _sb.Length = 0;
                text = SpaceBeforeNewline.Replace(text, "\n");
                // Only code keeps its blank lines; elsewhere they come from whitespace between tags.
                if (_kind != RichKind.Code) text = BlankLines.Replace(text, "\n");
                text = _kind == RichKind.Code ? text.Trim('\n') : text.Trim();
                if (StripTags(text).Trim().Length > 0)
                {
                    if (_kind == RichKind.Table) text = BoldFirstLine(text);
                    Blocks.Add(new RichBlock(_kind, text));
                }
                _kind = next;
            }

            public void Line()
            {
                TrimTrailingSpaces();
                if (_sb.Length > 0 && Last != '\n') _sb.Append('\n');
            }

            public void CellSeparator()
            {
                TrimTrailingSpaces();
                _sb.Append(" | ");
            }

            private char Last => _sb[_sb.Length - 1];

            private void TrimTrailingSpaces()
            {
                while (_sb.Length > 0 && Last == ' ') _sb.Length--;
            }

            private static string BoldFirstLine(string text)
            {
                int end = text.IndexOf('\n');
                return end < 0 ? $"<b>{text}</b>" : $"<b>{text.Substring(0, end)}</b>{text.Substring(end)}";
            }

            private static string StripTags(string text) => Regex.Replace(text, @"</?(b|i|u|s|color)(=[^>]*)?>", "");
        }
    }
}
