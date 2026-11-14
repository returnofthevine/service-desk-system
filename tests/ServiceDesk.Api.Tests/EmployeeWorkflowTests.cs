using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ServiceDesk.Api.Tests;

public sealed class EmployeeWorkflowTests
{
    private const string Accounts = "/api/accounts/employees";
    private const string EmployeeTickets = "/api/employee/tickets";
    private const string Password = "Test!7-Employee";

    [Fact]
    public async Task EmployeeAndTechnicianShareATicketWhilePrivateDataStaysPrivate()
    {
        await using var app = new TestApp();
        await app.PrepareDatabaseAsync();
        // Independent cookie jars model two staff roles and two different employee accounts.
        using var technician = await app.CreateSignedInClientAsync();
        using var viewer = await app.CreateSignedInClientAsync(viewer: true);
        await CreateAccount(technician, "employee-one");
        await CreateAccount(technician, "employee-two");
        using var employee = await app.CreateSignedInClientAsync("employee-one", Password);
        using var coworker = await app.CreateSignedInClientAsync("employee-two", Password);

        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync(Accounts)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(Accounts)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/v2/tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/tickets/options")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await employee.GetAsync($"{EmployeeTickets}/1")).StatusCode);

        var created = await Write(employee, HttpMethod.Post, EmployeeTickets, new
        {
            summary = "  Cannot open payroll  ",
            description = "  The payroll page displays an account access error.  ",
            category = "account-access",
            workstationDetails = "  FIN loaner  ",
        }, HttpStatusCode.Created);
        var id = created.GetProperty("id").GetInt32();
        var employeePath = $"{EmployeeTickets}/{id}";
        var technicianPath = $"/api/tickets/{id}";
        Assert.Equal("Cannot open payroll", created.GetProperty("summary").GetString());
        Assert.Equal("FIN loaner", created.GetProperty("workstationDetails").GetString());
        Assert.Equal("open", created.GetProperty("status").GetString());

        Assert.Single((await employee.GetFromJsonAsync<JsonElement>(EmployeeTickets)).EnumerateArray());
        Assert.Empty((await coworker.GetFromJsonAsync<JsonElement>(EmployeeTickets)).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await coworker.GetAsync(employeePath)).StatusCode);
        await Write(coworker, HttpMethod.Post, $"{employeePath}/replies", new { body = "Attempt to reply" }, HttpStatusCode.NotFound);

        // Find the exact request in the staff queue, then exercise the version check after triage.
        var queue = await technician.GetFromJsonAsync<JsonElement>("/api/v2/tickets?category=account-access&assigneeId=unassigned");
        var row = Assert.Single(queue.GetProperty("items").EnumerateArray());
        Assert.Equal(id, row.GetProperty("id").GetInt32());
        Assert.Equal("employee-one", row.GetProperty("requesterName").GetString());
        var detail = await technician.GetFromJsonAsync<JsonElement>(technicianPath);
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("asset").ValueKind);
        var version = detail.GetProperty("version").GetInt32();
        var options = await technician.GetFromJsonAsync<JsonElement>("/api/tickets/options");
        var technicianId = Assert.Single(options.GetProperty("technicians").EnumerateArray()).GetProperty("id").GetString();

        var triage = new { expectedVersion = version, priority = "P2", assignedTechnicianId = technicianId, assetId = 1 };
        detail = await Write(technician, HttpMethod.Patch, $"{technicianPath}/triage", triage);
        Assert.Equal("P2", detail.GetProperty("priority").GetString());
        Assert.Equal(technicianId, detail.GetProperty("assignedTechnicianId").GetString());
        Assert.Equal("WS-001", detail.GetProperty("asset").GetProperty("assetTag").GetString());
        Assert.NotEmpty(detail.GetProperty("triageHistory").EnumerateArray());
        await Write(technician, HttpMethod.Patch, $"{technicianPath}/triage", triage, HttpStatusCode.Conflict);

        await Write(technician, HttpMethod.Post, $"{technicianPath}/replies", new { body = "Internal escalation", isInternal = true });
        await Write(technician, HttpMethod.Post, $"{technicianPath}/replies", new { body = "Please retry payroll.", isInternal = false });
        await Write(employee, HttpMethod.Post, $"{employeePath}/replies", new { body = "Payroll opens now." });
        detail = await technician.GetFromJsonAsync<JsonElement>(technicianPath);
        Assert.Equal(3, detail.GetProperty("replies").GetArrayLength());
        Assert.Contains(detail.GetProperty("replies").EnumerateArray(), reply => reply.GetProperty("isInternal").GetBoolean());

        detail = await Write(technician, HttpMethod.Patch, $"{technicianPath}/status", new
        {
            expectedStatus = "open",
            status = "in-progress",
            expectedVersion = detail.GetProperty("version").GetInt32(),
        });
        await Write(technician, HttpMethod.Patch, $"{technicianPath}/status", new
        {
            expectedStatus = "in-progress",
            status = "resolved",
            expectedVersion = detail.GetProperty("version").GetInt32(),
            note = "Payroll access restored.",
        });

        // Re-read through the employee API to verify its projection omits internal notes and metadata.
        var employeeView = await employee.GetFromJsonAsync<JsonElement>(employeePath);
        Assert.Equal("resolved", employeeView.GetProperty("status").GetString());
        Assert.Equal(new[] { "Please retry payroll.", "Payroll opens now." },
            employeeView.GetProperty("replies").EnumerateArray().Select(reply => reply.GetProperty("body").GetString()));
        Assert.Equal("Payroll access restored.", employeeView.GetProperty("statusHistory")[1].GetProperty("resolutionNote").GetString());
        Assert.False(employeeView.TryGetProperty("triageHistory", out _));
        Assert.False(employeeView.TryGetProperty("asset", out _));
    }

    [Fact]
    public async Task EmployeeProvisioningRejectsDuplicateNamesAndMissingCsrf()
    {
        await using var app = new TestApp();
        await app.PrepareDatabaseAsync();
        using var technician = await app.CreateSignedInClientAsync();
        using var anonymous = app.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Accounts)).StatusCode);

        using var withoutCsrf = await technician.PostAsJsonAsync(Accounts, new { userName = "new-user", password = Password });
        Assert.Equal(HttpStatusCode.BadRequest, withoutCsrf.StatusCode);
        await CreateAccount(technician, "new-user");
        await Write(technician, HttpMethod.Post, Accounts, new { userName = "NEW-USER", password = Password }, HttpStatusCode.Conflict);

        var directory = await technician.GetFromJsonAsync<JsonElement>(Accounts);
        var employee = Assert.Single(directory.EnumerateArray());
        Assert.Equal("new-user", employee.GetProperty("userName").GetString());
        Assert.False(employee.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task TechnicianCanProvisionASecondTechnicianForAssignment()
    {
        await using var app = new TestApp();
        await app.PrepareDatabaseAsync();
        using var technician = await app.CreateSignedInClientAsync();
        var created = await Write(technician, HttpMethod.Post, "/api/accounts/technicians",
            new { userName = "next-technician", password = Password }, HttpStatusCode.Created);
        using var newTechnician = await app.CreateSignedInClientAsync("next-technician", Password);
        var identity = await newTechnician.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("Technician", Assert.Single(identity.GetProperty("roles").EnumerateArray()).GetString());

        var ticket = await technician.GetFromJsonAsync<JsonElement>("/api/tickets/1");
        var id = created.GetProperty("id").GetString();
        var triaged = await Write(technician, HttpMethod.Patch, "/api/tickets/1/triage", new
        {
            expectedVersion = ticket.GetProperty("version").GetInt32(), priority = "P2",
            assignedTechnicianId = id, assetId = 1,
        });
        Assert.Equal(id, triaged.GetProperty("assignedTechnicianId").GetString());
    }

    private static Task<JsonElement> CreateAccount(HttpClient technician, string userName) =>
        Write(technician, HttpMethod.Post, Accounts, new { userName, password = Password }, HttpStatusCode.Created);

    private static async Task<JsonElement> Write<T>(HttpClient client, HttpMethod method, string path, T request,
        HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await TestApp.SendJsonAsync(client, method, path, request);
        Assert.Equal(expected, response.StatusCode);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<JsonElement>() : default;
    }
}
