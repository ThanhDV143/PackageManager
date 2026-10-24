using System.Net;
using System.Text.RegularExpressions;
using Ganss.Xss;
using Markdig;

namespace PackageManager.Server.Services;

/// <summary>Turns untrusted HTML/Markdown (Asset Store, READMEs) into HTML that is safe to render.</summary>
public partial class HtmlContent
{
    private readonly HtmlSanitizer _sanitizer;
    private readonly MarkdownPipeline _markdown = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public HtmlContent()
    {
        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("mailto");
        _sanitizer.AllowedAttributes.Add("class");
        _sanitizer.AllowedCssProperties.Clear(); // keep the theme's colors and sizes
        _sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Dom.IElement el && el.LocalName == "a")
            {
                el.SetAttribute("target", "_blank");
                el.SetAttribute("rel", "noopener noreferrer");
            }
        };
        _sanitizer.AllowedAttributes.Add("target");
        _sanitizer.AllowedAttributes.Add("rel");
    }

    public string? Sanitize(string? html) =>
        string.IsNullOrWhiteSpace(html) ? null : _sanitizer.Sanitize(FixProtocolRelative(html));

    public string? Markdown(string? markdown) =>
        string.IsNullOrWhiteSpace(markdown) ? null : Sanitize(Markdig.Markdown.ToHtml(markdown, _markdown));

    /// <summary>Plain text for search and short descriptions.</summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        string text = TagRegex().Replace(html, " ");
        return WhitespaceRegex().Replace(WebUtility.HtmlDecode(text), " ").Trim();
    }

    private static string FixProtocolRelative(string html) => ProtocolRelativeRegex().Replace(html, "$1https://");

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"((?:src|href)\s*=\s*[""'])//")]
    private static partial Regex ProtocolRelativeRegex();
}
