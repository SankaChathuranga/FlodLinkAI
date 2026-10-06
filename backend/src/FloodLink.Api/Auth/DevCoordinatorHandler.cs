using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FloodLink.Api.Auth;

/// <summary>
/// Development-only authentication scheme.
///
/// Auto-authenticates every request as a "Coordinator" role principal. This
/// exists so Member D's [Authorize(Roles = "Coordinator")] endpoints can be
/// demoed end-to-end BEFORE the team wires real JWT auth (the Week 2 JWT task).
/// It is registered ONLY when the app runs in the Development environment.
///
/// Production behaviour is unchanged: with no scheme registered, [Authorize]
/// correctly rejects anonymous requests once JWT lands.
/// </summary>
public sealed class DevCoordinatorHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public DevCoordinatorHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            claims: new[]
            {
                new Claim(ClaimTypes.Name, "dev-coordinator"),
                new Claim(ClaimTypes.Role, "Coordinator")
            },
            authenticationType: "Dev");

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Dev");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}