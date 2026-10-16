using System.Security.Claims;

namespace BookStore.Api.Controllers;

public static class ControllerExtensions
{
    public static string GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
}