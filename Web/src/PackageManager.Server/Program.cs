using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using PackageManager.Server;
using PackageManager.Server.Data;
using PackageManager.Server.Endpoints;
using PackageManager.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection("Admin"));
builder.Services.Configure<VerdaccioOptions>(builder.Configuration.GetSection("Verdaccio"));

string dbPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath,
    builder.Configuration.GetSection("Storage").Get<StorageOptions>()?.DatabasePath ?? new StorageOptions().DatabasePath));
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

// Keep cookie-encryption keys next to the database so updating the container does not sign the admin out.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetDirectoryName(dbPath)!, "keys")));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<FileStore>();
builder.Services.AddSingleton<HtmlContent>();
builder.Services.AddScoped<PackageService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<VerdaccioClient>(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHttpClient<AssetStoreClient>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36");
    c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
})
// With the cookies it sets on the first visit, the Asset Store answers later requests with a page
// that no longer embeds the product data; pooled handlers would keep those cookies.
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false });

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "pm_auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
        // API only: answer 401/403 instead of redirecting to a login page.
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(AuthEndpoints.LoginRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(5) }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

if (app.Environment.IsDevelopment())
    app.UseWebAssemblyDebugging();

// Keep the old site's direct links (…/unitypkgs/<file>) working. Runs before routing so the SPA fallback cannot claim them.
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings.Clear();
foreach (string ext in FileStore.AllowedExtensions) contentTypes.Mappings[ext] = "application/octet-stream";
app.UseStaticFiles(new StaticFileOptions
{
    RequestPath = "/unitypkgs",
    FileProvider = new PhysicalFileProvider(app.Services.GetRequiredService<FileStore>().Root),
    ContentTypeProvider = contentTypes,
});

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapAuthEndpoints();
app.MapPackageEndpoints();
app.MapUnmanagedEndpoints();
app.MapExternalEndpoints();
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
// Package IDs contain dots ("com.moremountains.feel"), which the default fallback treats as file names.
app.MapFallbackToFile("packages/{*path}", "index.html");
app.MapFallbackToFile("upm/{*path}", "index.html");

app.Run();
