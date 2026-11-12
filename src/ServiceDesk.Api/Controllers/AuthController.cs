using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AuthController(
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("csrf")]
    public IActionResult GetCsrfToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { tokens.RequestToken });
    }

    [AllowAnonymous]
    [HttpPost("sign-in")]
    public async Task<IActionResult> StartSession(SignInRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Enter your username and password.");
        }

        var result = await signInManager.PasswordSignInAsync(
            request.UserName.Trim(), request.Password, isPersistent: false, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid username or password.");
        }

        return Ok(new { SignedIn = true });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetSession()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        return Ok(new { user.UserName, Roles = roles });
    }

    [Authorize]
    [HttpPost("sign-out")]
    public async Task<IActionResult> EndSession()
    {
        await signInManager.SignOutAsync();
        return Ok(new { SignedIn = false });
    }

    public sealed record SignInRequest(string? UserName, string? Password);
}
