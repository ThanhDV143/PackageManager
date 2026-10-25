using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using PackageManager.Shared;

namespace PackageManager.Server.Endpoints;

public static class AuthEndpoints
{
    public const string LoginRateLimit = "login";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth");

        g.MapGet("/me", (ClaimsPrincipal user) => new AuthStateDto
        {
            IsAdmin = user.Identity?.IsAuthenticated == true,
            UserName = user.Identity?.Name,
        });

        g.MapPost("/login", async (LoginRequest req, IOptions<AdminOptions> options, HttpContext ctx) =>
        {
            var admin = options.Value;
            if (string.IsNullOrEmpty(admin.Password))
                return Results.BadRequest(ctx.Error(new Text(
                    "Chưa cấu hình mật khẩu admin (biến môi trường Admin__Password).",
                    "Admin password is not set (environment variable Admin__Password).")));

            // Evaluate both comparisons so timing does not reveal which one failed.
            bool userOk = SecretEquals(req.UserName ?? "", admin.UserName);
            bool passOk = SecretEquals(req.Password ?? "", admin.Password);
            if (!(userOk & passOk))
                return Results.BadRequest(ctx.Error(new Text("Sai tên đăng nhập hoặc mật khẩu.", "Wrong user name or password.")));

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, admin.UserName)], CookieAuthenticationDefaults.AuthenticationScheme);
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true });
            return Results.Ok(new AuthStateDto { IsAdmin = true, UserName = admin.UserName });
        }).RequireRateLimiting(LoginRateLimit);

        g.MapPost("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok(new AuthStateDto());
        });
    }

    private static bool SecretEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(a)), SHA256.HashData(Encoding.UTF8.GetBytes(b)));
}
