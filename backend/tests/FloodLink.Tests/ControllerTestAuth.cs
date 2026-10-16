using System.Security.Claims;
using FloodLink.Domain.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FloodLink.Tests;

/// <summary>Gives a directly constructed controller a signed-in user, as the JWT middleware would.</summary>
public static class ControllerTestAuth
{
    public static T SignInAs<T>(this T controller, int userId, string role = Roles.Volunteer) where T : ControllerBase
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role)], "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }
}
