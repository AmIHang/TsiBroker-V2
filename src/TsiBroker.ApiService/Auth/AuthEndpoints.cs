using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace TsiBroker.ApiService.Auth;

public record LoginRequest(string Username, string Password);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginRequest request, HttpContext context, IOptions<AdminUserOptions> adminUser) =>
        {
            var admin = adminUser.Value;
            if (string.IsNullOrEmpty(admin.Password)
                || !string.Equals(request.Username, admin.Username, StringComparison.Ordinal)
                || !FixedTimeEquals(request.Password, admin.Password))
            {
                return Results.Unauthorized();
            }

            var claims = new[] { new Claim(ClaimTypes.Name, admin.Username) };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            return Results.Ok(new { username = admin.Username });
        });

        group.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok();
        }).RequireAuthorization();

        group.MapGet("/me", (ClaimsPrincipal user) =>
            Results.Ok(new { username = user.Identity!.Name })
        ).RequireAuthorization();
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
