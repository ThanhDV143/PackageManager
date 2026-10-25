using Microsoft.EntityFrameworkCore;
using PackageManager.Server.Data;
using PackageManager.Server.Services;
using PackageManager.Shared;

namespace PackageManager.Server.Endpoints;

/// <summary>Files sitting in the packages folder that no package version points to (e.g. copied in by hand).</summary>
public static class UnmanagedEndpoints
{
    public static void MapUnmanagedEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/unmanaged").RequireAuthorization();

        g.MapGet("/count", async (AppDbContext db, FileStore files, CancellationToken ct) =>
            (await FindAsync(db, files, ct)).Count);

        g.MapGet("/", async (AppDbContext db, FileStore files, PackageService svc, TimeProvider clock, CancellationToken ct) =>
        {
            var found = await FindAsync(db, files, ct);
            var lookup = await svc.LookupAsync(ct);
            var groups = found
                .Select(f => (File: f, Parsed: FileNameParser.Parse(f.Name)))
                .GroupBy(x => FileNameParser.Normalize(x.Parsed.Name))
                .Select(grp =>
                {
                    string name = grp.First().Parsed.Name;
                    var match = PackageService.Match(name, lookup);
                    return new UnmanagedGroupDto
                    {
                        Name = name,
                        MatchPackageId = match?.PackageId,
                        MatchDisplayName = match?.DisplayName,
                        Files = grp
                            .OrderByDescending(x => x.Parsed.Version, VersionComparer.Instance)
                            .Select(x => new UnmanagedFileDto
                            {
                                RelativePath = files.ToRelative(x.File.FullName),
                                FileName = x.File.Name,
                                GuessedVersion = x.Parsed.Version,
                                Size = x.File.Length,
                                ModifiedAt = x.File.LastWriteTimeUtc,
                            }).ToList(),
                    };
                })
                // Groups that match an existing package first, groups without a guessable version last.
                .OrderBy(grp => grp.MatchPackageId == null)
                .ThenBy(grp => grp.Files.Any(f => f.GuessedVersion == null))
                .ThenBy(grp => grp.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return new UnmanagedScanDto
            {
                Folder = Path.GetFileName(files.Root.TrimEnd(Path.DirectorySeparatorChar)) + "/",
                ScannedAt = clock.GetUtcNow().UtcDateTime,
                TotalFiles = found.Count,
                Groups = groups,
            };
        });

        g.MapPost("/assign", async (AssignFilesRequest req, AppDbContext db, FileStore files, PackageService svc, HttpContext ctx, CancellationToken ct) =>
        {
            if (await svc.FindAsync(req.PackageId, ct) is not { } p) return Results.NotFound(ctx.Error(new Text("Không tìm thấy package.", "Package not found.")));
            if (req.Files.Count == 0) return Results.BadRequest(ctx.Error(new Text("Chưa chọn file nào.", "No files selected.")));

            var taken = p.Versions.Select(v => v.Version).ToHashSet();
            foreach (var item in req.Files)
            {
                string version = item.Version?.Trim() ?? "";
                if (!PackageService.IsValidVersion(version))
                {
                    string name = Path.GetFileName(item.RelativePath);
                    return Results.BadRequest(ctx.Error(new Text($"Nhập phiên bản hợp lệ cho {name}.", $"Enter a valid version for {name}.")));
                }
                if (!taken.Add(version))
                    return Results.Conflict(ctx.Error(new Text($"{p.DisplayName} đã có phiên bản {version}.", $"{p.DisplayName} already has version {version}.")));

                string? full = files.Resolve(item.RelativePath);
                if (full == null || !File.Exists(full) || !FileStore.IsAllowed(full))
                    return Results.BadRequest(ctx.Error(new Text($"Không tìm thấy file {item.RelativePath}.", $"File {item.RelativePath} not found.")));
                string rel = files.ToRelative(full);
                if (await db.PackageVersions.AnyAsync(v => v.RelativePath == rel, ct))
                    return Results.Conflict(ctx.Error(new Text($"{rel} đã thuộc một package khác.", $"{rel} already belongs to another package.")));

                var info = new FileInfo(full);
                p.Versions.Add(new PackageVersion
                {
                    Version = version,
                    FileName = info.Name,
                    RelativePath = rel,
                    Size = info.Length,
                    UploadedAt = info.LastWriteTimeUtc,
                });
            }
            var assigned = p.Versions.Select(v => v.RelativePath).ToList();
            db.IgnoredFiles.RemoveRange(await db.IgnoredFiles.Where(f => assigned.Contains(f.RelativePath)).ToListAsync(ct));
            p.UpdatedAt = svc.Now;
            await db.SaveChangesAsync(ct);
            return Results.Ok(PackageService.ToDetail(p));
        });

        g.MapPost("/ignore", async (IgnoreFilesRequest req, AppDbContext db, FileStore files, CancellationToken ct) =>
        {
            var existing = await db.IgnoredFiles.Select(f => f.RelativePath).ToListAsync(ct);
            foreach (string path in req.RelativePaths.Distinct())
            {
                if (files.Resolve(path) is not { } full) continue;
                string rel = files.ToRelative(full);
                if (!existing.Contains(rel)) db.IgnoredFiles.Add(new IgnoredFile { RelativePath = rel });
            }
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static async Task<List<FileInfo>> FindAsync(AppDbContext db, FileStore files, CancellationToken ct)
    {
        var known = (await db.PackageVersions.Select(v => v.RelativePath).ToListAsync(ct))
            .Concat(await db.IgnoredFiles.Select(f => f.RelativePath).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return files.EnumerateFiles().Where(f => !known.Contains(files.ToRelative(f.FullName))).ToList();
    }
}
