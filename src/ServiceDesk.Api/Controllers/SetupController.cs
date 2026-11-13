using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Api.Data;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/setup")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class SetupController(
    FirstRunSetup setup,
    UserManager<IdentityUser> users,
    RoleManager<IdentityRole> roles,
    ServiceDeskDbContext db) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(new { setup.Required });

    [HttpPost("technician")]
    public async Task<IActionResult> CreateTechnician(SetupRequest request, CancellationToken cancellationToken)
    {
        await setup.Gate.WaitAsync(cancellationToken);
        try
        {
            if (!setup.Required)
            {
                return Problem(statusCode: StatusCodes.Status409Conflict, detail: "Initial setup is already complete or unavailable.");
            }

            if (!setup.Accepts(request.SetupCode))
            {
                return Problem(statusCode: StatusCodes.Status403Forbidden, detail: "Enter the setup code displayed in the server terminal.");
            }

            var userName = request.UserName?.Trim();
            if (string.IsNullOrEmpty(userName) || userName.Length > 100 ||
                string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 128)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest,
                    detail: "Enter a username of 1–100 characters and a password of up to 128 characters.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            const string technician = "Technician";
            if (!await roles.RoleExistsAsync(technician))
            {
                RequireSuccess(await roles.CreateAsync(new IdentityRole(technician)));
            }

            var user = new IdentityUser(userName);
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest,
                    detail: string.Join(" ", result.Errors.Select(error => error.Description)));
            }

            RequireSuccess(await users.AddToRoleAsync(user, technician));
            await transaction.CommitAsync(CancellationToken.None);
            setup.Complete();
            return StatusCode(StatusCodes.Status201Created, new { created = true });
        }
        finally
        {
            setup.Gate.Release();
        }
    }

    private static void RequireSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }

    public sealed record SetupRequest(string? UserName, string? Password, string? SetupCode);
}
