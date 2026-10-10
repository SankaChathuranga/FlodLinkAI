using System.Security.Claims;

namespace FloodLink.Api.Auth;

/// <summary>Helpers for reading the signed-in user from the request principal.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The numeric user id from the token's <c>sub</c> / NameIdentifier claim, or null when the
    /// request is anonymous or the claim is missing or not a number.
    /// </summary>
    public static int? GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return int.TryParse(value, out var id) ? id : null;
    }
}
