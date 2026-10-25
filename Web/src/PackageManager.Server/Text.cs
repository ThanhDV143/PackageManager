using PackageManager.Shared;

namespace PackageManager.Server;

/// <summary>
/// A user-facing message in both languages. Vietnamese by default (the web UI asks for it explicitly);
/// English when the caller's Accept-Language puts English first (the Unity tool).
/// </summary>
public readonly record struct Text(string Vi, string En)
{
    public string For(HttpRequest request) => PrefersEnglish(request) ? En : Vi;

    public static bool PrefersEnglish(HttpRequest request)
    {
        // Stable sort: among equal qualities the header's own order wins.
        var top = request.GetTypedHeaders().AcceptLanguage.OrderByDescending(l => l.Quality ?? 1).FirstOrDefault();
        return top?.Value.Value?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true;
    }
}

public static class TextExtensions
{
    public static ApiError Error(this HttpContext ctx, Text text) => new() { Message = text.For(ctx.Request) };
}
