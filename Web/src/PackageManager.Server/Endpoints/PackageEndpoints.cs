using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using PackageManager.Server.Data;
using PackageManager.Server.Services;
using PackageManager.Shared;

namespace PackageManager.Server.Endpoints;

public static class PackageEndpoints
{
    public static void MapPackageEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/packages");

        g.MapGet("/", (PackageService svc, string? sort, int? page, int? pageSize, CancellationToken ct) =>
            svc.ListAsync(sort, page ?? 1, pageSize ?? 24, ct));

        g.MapGet("/lookup", (PackageService svc, CancellationToken ct) => svc.LookupAsync(ct)).RequireAuthorization();

        g.MapGet("/{packageId}", async (string packageId, PackageService svc, CancellationToken ct) =>
            await svc.FindAsync(packageId, ct) is { } p ? Results.Ok(PackageService.ToDetail(p)) : Results.NotFound());

        g.MapGet("/{packageId}/download", async (string packageId, PackageService svc, FileStore files, CancellationToken ct) =>
            await svc.FindAsync(packageId, ct) is { } p && PackageService.Latest(p.Versions) is { } v
                ? FileResult(files, v)
                : Results.NotFound());

        g.MapPost("/", async (PackageEditDto dto, PackageService svc, AppDbContext db, CancellationToken ct) =>
        {
            var p = new Package { CreatedAt = svc.Now };
            if (await svc.ApplyAsync(p, dto, ct) is { } error) return Results.BadRequest(new ApiError { Message = error });
            db.Packages.Add(p);
            await db.SaveChangesAsync(ct);
            return Results.Ok(PackageService.ToDetail(p));
        }).RequireAuthorization();

        g.MapPut("/{packageId}", async (string packageId, PackageEditDto dto, PackageService svc, AppDbContext db, CancellationToken ct) =>
        {
            if (await svc.FindAsync(packageId, ct) is not { } p) return Results.NotFound();
            if (await svc.ApplyAsync(p, dto, ct) is { } error) return Results.BadRequest(new ApiError { Message = error });
            await db.SaveChangesAsync(ct);
            return Results.Ok(PackageService.ToDetail(p));
        }).RequireAuthorization();

        g.MapDelete("/{packageId}", async (string packageId, bool? deleteFiles, PackageService svc, AppDbContext db, FileStore files, CancellationToken ct) =>
        {
            if (await svc.FindAsync(packageId, ct) is not { } p) return Results.NotFound();
            var paths = p.Versions.Select(v => files.Resolve(v.RelativePath)).OfType<string>().ToList();
            db.Packages.Remove(p);
            await db.SaveChangesAsync(ct);
            // Without deleteFiles the files stay on disk and show up again under "File chưa quản lý".
            if (deleteFiles == true) paths.ForEach(FileStore.TryDelete);
            return Results.NoContent();
        }).RequireAuthorization();

        g.MapPost("/{packageId}/refresh", async (string packageId, PackageService svc, AssetStoreClient store, AppDbContext db, CancellationToken ct) =>
        {
            if (await svc.FindAsync(packageId, ct) is not { } p) return Results.NotFound();
            if (string.IsNullOrEmpty(p.AssetStoreUrl)) return Results.BadRequest(new ApiError { Message = "Package chưa có link Asset Store." });
            PackageEditDto fresh;
            try { fresh = await store.FetchAsync(p.AssetStoreUrl, ct); }
            catch (AssetStoreException ex) { return Results.BadRequest(new ApiError { Message = ex.Message }); }

            // Keep the admin's own ID and name; refresh everything read from the store.
            fresh.PackageId = p.PackageId;
            fresh.DisplayName = p.DisplayName;
            if (await svc.ApplyAsync(p, fresh, ct) is { } error) return Results.BadRequest(new ApiError { Message = error });
            await db.SaveChangesAsync(ct);
            return Results.Ok(PackageService.ToDetail(p));
        }).RequireAuthorization();

        g.MapPut("/{packageId}/versions/upload", UploadAsync).RequireAuthorization();

        var versions = app.MapGroup("/api/versions");

        versions.MapGet("/{id:int}/download", async (int id, AppDbContext db, FileStore files, CancellationToken ct) =>
            await db.PackageVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct) is { } v ? FileResult(files, v) : Results.NotFound());

        versions.MapDelete("/{id:int}", async (int id, bool? deleteFile, AppDbContext db, FileStore files, CancellationToken ct) =>
        {
            if (await db.PackageVersions.FirstOrDefaultAsync(v => v.Id == id, ct) is not { } v) return Results.NoContent();
            string? path = files.Resolve(v.RelativePath);
            db.PackageVersions.Remove(v);
            await db.SaveChangesAsync(ct);
            if (deleteFile != false && path != null) FileStore.TryDelete(path);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapPost("/api/uploads/resolve", async (UploadResolveRequest req, PackageService svc, AppDbContext db, CancellationToken ct) =>
        {
            var lookup = await svc.LookupAsync(ct);
            var existing = await db.PackageVersions.AsNoTracking()
                .Select(v => new { v.Package.PackageId, v.Version }).ToListAsync(ct);
            return req.FileNames.Take(200).Select(name =>
            {
                var parsed = FileNameParser.Parse(name);
                var match = PackageService.Match(parsed.Name, lookup);
                return new UploadResolveItem
                {
                    FileName = name,
                    GuessedName = parsed.Name,
                    GuessedVersion = parsed.Version,
                    MatchPackageId = match?.PackageId,
                    MatchDisplayName = match?.DisplayName,
                    VersionExists = match != null && existing.Any(e =>
                        string.Equals(e.PackageId, match.PackageId, StringComparison.OrdinalIgnoreCase) && e.Version == parsed.Version),
                };
            }).ToList();
        }).RequireAuthorization();
    }

    /// <summary>
    /// Raw-body upload (application/octet-stream) so a file of several hundred MB streams straight to disk.
    /// Release notes travel URI-encoded in the X-Release-Notes header.
    /// </summary>
    private static async Task<IResult> UploadAsync(
        string packageId, string version, string fileName, string? unityVersion,
        HttpContext ctx, PackageService svc, AppDbContext db, FileStore files, CancellationToken ct)
    {
        if (ctx.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = null;

        version = version.Trim();
        if (!PackageService.IsValidVersion(version)) return Results.BadRequest(new ApiError { Message = "Phiên bản không hợp lệ." });
        if (!FileStore.IsAllowed(fileName)) return Results.BadRequest(new ApiError { Message = "Chỉ nhận file .unitypackage hoặc file nén." });
        if (await svc.FindAsync(packageId, ct) is not { } p) return Results.NotFound(new ApiError { Message = "Không tìm thấy package đích." });
        if (p.Versions.Any(v => v.Version == version))
            return Results.Conflict(new ApiError { Message = $"{p.DisplayName} đã có phiên bản {version}." });

        string? notes = ctx.Request.Headers["X-Release-Notes"].FirstOrDefault() is { Length: > 0 } raw ? Uri.UnescapeDataString(raw) : null;

        FileInfo saved;
        try
        {
            saved = await files.SaveAsync(fileName, ctx.Request.Body, ct);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or BadHttpRequestException)
        {
            return Results.BadRequest(new ApiError { Message = "Mất kết nối khi đang tải lên. File chưa được lưu." });
        }

        var entity = new PackageVersion
        {
            Package = p,
            Version = version,
            FileName = saved.Name,
            RelativePath = files.ToRelative(saved.FullName),
            Size = saved.Length,
            UnityVersion = string.IsNullOrWhiteSpace(unityVersion) ? null : unityVersion.Trim()[..Math.Min(unityVersion.Trim().Length, 50)],
            ReleaseNotes = notes is { Length: > 4000 } ? notes[..4000] : notes,
            UploadedAt = svc.Now,
        };
        db.PackageVersions.Add(entity);
        p.UpdatedAt = svc.Now;
        await db.SaveChangesAsync(CancellationToken.None);
        return Results.Ok(PackageService.ToVersion(entity, PackageService.Latest(p.Versions) == entity));
    }

    private static IResult FileResult(FileStore files, PackageVersion v) =>
        files.Resolve(v.RelativePath) is { } path && File.Exists(path)
            ? Results.File(path, "application/octet-stream", v.FileName, enableRangeProcessing: true)
            : Results.NotFound(new ApiError { Message = "File không còn trên ổ đĩa." });
}
