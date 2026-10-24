using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PackageManager.Server.Data;
using PackageManager.Server.Services;
using PackageManager.Shared;

namespace PackageManager.Server.Endpoints;

/// <summary>UPM (Verdaccio), cross-source search and Asset Store lookup.</summary>
public static class ExternalEndpoints
{
    public static void MapExternalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/upm", (VerdaccioClient upm, CancellationToken ct) => upm.ListAsync(ct));

        app.MapGet("/api/upm/{name}", async (string name, VerdaccioClient upm, CancellationToken ct) =>
        {
            // Only packages published to Verdaccio; uplink caches (com.unity.* etc.) stay hidden even by direct link.
            var list = await upm.ListAsync(ct);
            if (list.Available && !list.Packages.Any(p => p.Name == name)) return Results.NotFound();

            UpmPackageDetailDto? detail;
            try { detail = await upm.GetDetailAsync(name, ct); }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
            {
                return Results.Json(new ApiError { Message = "Không kết nối được registry UPM." }, statusCode: StatusCodes.Status502BadGateway);
            }
            if (detail == null) return Results.NotFound();
            detail.KnownPackages = list.Packages.Select(p => p.Name).ToList();
            return Results.Ok(detail);
        });

        app.MapGet("/api/assetstore", async (string url, AssetStoreClient store, CancellationToken ct) =>
        {
            try { return Results.Ok(await store.FetchAsync(url, ct)); }
            catch (AssetStoreException ex) { return Results.BadRequest(new ApiError { Message = ex.Message }); }
        }).RequireAuthorization();

        app.MapGet("/api/search", SearchAsync);
    }

    private static async Task<SearchResponseDto> SearchAsync(string? q, AppDbContext db, VerdaccioClient upm, CancellationToken ct)
    {
        string query = (q ?? "").Trim();
        var response = new SearchResponseDto { Query = query };
        string[] terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0) return response;

        var scored = new List<(SearchResultDto Result, int Score)>();

        var packages = await db.Packages.AsNoTracking().Include(p => p.Versions).ToListAsync(ct);
        foreach (var p in packages)
        {
            var hit = Match(terms,
                ("tên", p.DisplayName, 4),
                ("tên package", p.PackageId, 3),
                ("publisher", p.Publisher, 2),
                ("mô tả", p.ShortDescription, 1),
                ("mô tả đầy đủ", HtmlContent.ToPlainText(p.DescriptionHtml), 0));
            if (hit == null) continue;
            var latest = PackageService.Latest(p.Versions);
            scored.Add((Result(PackageSource.Unity, p.PackageId, p.DisplayName, p.Publisher, p.ShortDescription,
                latest != null ? "v" + latest.Version : null, hit.Value.Fields, terms), hit.Value.Score));
        }

        var upmList = await upm.ListAsync(ct);
        response.UpmAvailable = upmList.Available;
        foreach (var u in upmList.Packages)
        {
            var hit = Match(terms,
                ("tên", u.DisplayName, 4),
                ("tên package", u.Name, 3),
                ("tác giả", u.Author, 2),
                ("keyword", string.Join(' ', u.Keywords), 2),
                ("mô tả", u.Description, 1));
            if (hit == null) continue;
            scored.Add((Result(PackageSource.Upm, u.Name, u.DisplayName ?? u.Name, u.Author, u.Description, u.Version, hit.Value.Fields, terms),
                hit.Value.Score));
        }

        response.Results = scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Result.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(s => s.Result).Take(100).ToList();
        return response;
    }

    /// <summary>Every term must appear in at least one field; the score is the best field weight hit.</summary>
    private static (string Fields, int Score)? Match(string[] terms, params (string Label, string? Text, int Weight)[] fields)
    {
        var matched = new List<string>();
        int score = -1;
        foreach (string term in terms)
        {
            bool any = false;
            foreach (var f in fields)
            {
                if (f.Text == null || !f.Text.Contains(term, StringComparison.CurrentCultureIgnoreCase)) continue;
                any = true;
                score = Math.Max(score, f.Weight);
                if (!matched.Contains(f.Label)) matched.Add(f.Label);
            }
            if (!any) return null;
        }
        return (string.Join(", ", matched), score);
    }

    private static SearchResultDto Result(string source, string id, string name, string? by, string? desc, string? version, string fields, string[] terms)
    {
        var dto = new SearchResultDto
        {
            Source = source, Id = id, DisplayName = name, By = by, Description = desc, Version = version, MatchedFields = fields,
        };
        foreach (string term in terms)
        {
            int at = name.IndexOf(term, StringComparison.CurrentCultureIgnoreCase);
            if (at < 0) continue;
            dto.HighlightStart = at;
            dto.HighlightLength = term.Length;
            break;
        }
        return dto;
    }
}
