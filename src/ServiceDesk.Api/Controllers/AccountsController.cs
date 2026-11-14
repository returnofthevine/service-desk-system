using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Api.Data;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Authorize(Roles = "Technician")]
[Route("api/accounts")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AccountsController(
    UserManager<IdentityUser> users,
    RoleManager<IdentityRole> roles,
    ServiceDeskDbContext db) : ControllerBase
{
    // Each route fixes its own role. Callers cannot submit an arbitrary role name.
    private const string Employee = "Employee";
    private const string Technician = "Technician";

    [HttpGet("employees")]
    public Task<IActionResult> GetEmployees() => GetAccounts(Employee);

    [HttpGet("technicians")]
    public Task<IActionResult> GetTechnicians() => GetAccounts(Technician);

    [HttpPost("employees")]
    public Task<IActionResult> CreateEmployee(CreateAccountRequest request, CancellationToken cancellationToken) =>
        CreateAccount(request, Employee, cancellationToken);

    [HttpPost("technicians")]
    public Task<IActionResult> CreateTechnician(CreateAccountRequest request, CancellationToken cancellationToken) =>
        CreateAccount(request, Technician, cancellationToken);

    private async Task<IActionResult> GetAccounts(string role)
    {
        var accounts = await users.GetUsersInRoleAsync(role);
        return Ok(accounts.OrderBy(user => user.UserName, StringComparer.OrdinalIgnoreCase)
            .Select(user => new Account(user.Id, user.UserName!)));
    }

    private async Task<IActionResult> CreateAccount(CreateAccountRequest request, string role, CancellationToken cancellationToken)
    {
        var userName = request.UserName?.Trim();
        if (string.IsNullOrEmpty(userName) || userName.Length > 100 ||
            string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 128)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                detail: "Enter a username of 1–100 characters and a password of up to 128 characters.");
        }

        // Account creation and role assignment must succeed together or the transaction rolls them back.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await roles.RoleExistsAsync(role))
        {
            var roleResult = await roles.CreateAsync(new IdentityRole(role));
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException($"Could not create the {role} role.");
            }
        }

        var user = new IdentityUser(userName);
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            var statusCode = created.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.DuplicateUserName))
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;
            return Problem(statusCode: statusCode,
                detail: string.Join(" ", created.Errors.Select(error => error.Description)));
        }

        var added = await users.AddToRoleAsync(user, role);
        if (!added.Succeeded)
        {
            throw new InvalidOperationException($"Could not assign the {role} role.");
        }

        await transaction.CommitAsync(CancellationToken.None);
        return StatusCode(StatusCodes.Status201Created, new Account(user.Id, user.UserName!));
    }

    public sealed record CreateAccountRequest(string? UserName, string? Password);

    private sealed record Account(string Id, string UserName);
}
