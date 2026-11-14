using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Api.Data;
using Xunit;

namespace ServiceDesk.Api.Tests;

public sealed class AssetImportTests
{
    private const string Header = "assetTag,hostname,bitLockerState,tpmState,lastCheckedAtUtc\n";

    // A batch with an invalid row must leave the asset table untouched.
    [Fact]
    public async Task InvalidImportIsAtomicAndCorrectedAssetsCanBeUsedOnTickets()
    {
        const string invalidCsv = Header + """
            WS-021,LAB-WS-01,protected,ready,2026-11-01T13:30:00Z
            ws-021,LAB-WS-02,protected,ready,2026-11-01T13:35:00Z
            WS-022,LAB-WS-03,wrong,ready,not-a-date
            """;
        const string correctedCsv = Header + """
             ws-021 ," LAB-WS-01 ",PROTECTED,READY,2026-11-01T13:30:00Z
            WS-023,LAB-WS-02,protected,ready,2026-11-01T13:35:00Z
            WS-022,LAB-WS-03,off,ready,2026-11-01T13:40:00Z
            """;

        await using var app = new TestApp();
        await app.PrepareDatabaseAsync();
        using var technician = await app.CreateSignedInClientAsync();

        using var invalid = await UploadAsync(technician, invalidCsv);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        using var invalidJson = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
        Assert.Equal(0, invalidJson.RootElement.GetProperty("createdCount").GetInt32());
        var errors = invalidJson.RootElement.GetProperty("errors").EnumerateArray()
            .Select(error => (error.GetProperty("row").GetInt32(), error.GetProperty("field").GetString()!));
        Assert.Equal(new[] { (3, "assetTag"), (4, "bitLockerState"), (4, "lastCheckedAtUtc") }, errors);
        Assert.Equal(0, await CountImportedAssetsAsync(app));

        using var corrected = await UploadAsync(technician, correctedCsv);
        Assert.Equal(HttpStatusCode.Created, corrected.StatusCode);
        using var correctedJson = JsonDocument.Parse(await corrected.Content.ReadAsStringAsync());
        Assert.Equal(3, correctedJson.RootElement.GetProperty("createdCount").GetInt32());
        var firstAsset = correctedJson.RootElement.GetProperty("assets")[0];
        Assert.Equal("WS-021", firstAsset.GetProperty("assetTag").GetString());

        using var created = await TestApp.SendJsonAsync(technician, HttpMethod.Post, "/api/tickets", new
        {
            AssetId = firstAsset.GetProperty("id").GetInt32(),
            Summary = "Confirm imported workstation",
            Priority = "P2",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var detail = JsonDocument.Parse(await technician.GetStringAsync(created.Headers.Location!));
        var asset = detail.RootElement.GetProperty("asset");
        Assert.Equal("WS-021", asset.GetProperty("assetTag").GetString());
        Assert.Equal("LAB-WS-01", asset.GetProperty("hostname").GetString());
        Assert.Equal("protected", asset.GetProperty("bitLockerState").GetString());
        Assert.Equal("ready", asset.GetProperty("tpmState").GetString());
        Assert.Equal("2026-11-01T13:30:00Z", asset.GetProperty("lastCheckedAtUtc").GetString());

        using var repeat = await UploadAsync(technician, correctedCsv);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, repeat.StatusCode);
        using var repeatJson = JsonDocument.Parse(await repeat.Content.ReadAsStringAsync());
        Assert.Equal(0, repeatJson.RootElement.GetProperty("createdCount").GetInt32());
        Assert.Equal(3, repeatJson.RootElement.GetProperty("errors").GetArrayLength());
        Assert.Equal(3, await CountImportedAssetsAsync(app));

        using var viewer = await app.CreateSignedInClientAsync(viewer: true);
        using var forbidden = await UploadAsync(viewer, correctedCsv);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task InvalidCsvStructureAndUploadLimitsAreRejected()
    {
        await using var app = new TestApp();
        await app.PrepareDatabaseAsync();
        using var technician = await app.CreateSignedInClientAsync();

        using var wrongHeader = await UploadAsync(technician, "hostname,assetTag\nLAB-WS-01,WS-021");
        Assert.Equal(HttpStatusCode.BadRequest, wrongHeader.StatusCode);

        using var malformed = await UploadAsync(technician, Header + "WS-021,\"LAB-WS-01,protected,ready\n");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);

        using var shortRecord = await UploadAsync(technician, Header + "WS-021,LAB-WS-01,protected,ready\n");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, shortRecord.StatusCode);
        using var shortJson = JsonDocument.Parse(await shortRecord.Content.ReadAsStringAsync());
        var error = Assert.Single(shortJson.RootElement.GetProperty("errors").EnumerateArray());
        Assert.Equal(2, error.GetProperty("row").GetInt32());
        Assert.Equal("record", error.GetProperty("field").GetString());

        using var oversized = await UploadAsync(technician, new string('x', 1_048_577));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);

        var excessiveRecords = Header + string.Concat(Enumerable.Range(1000, 1001).Select(
            index => $"WS-{index},LAB-WS,protected,ready,2026-11-01T13:30:00Z\n"));
        using var tooMany = await UploadAsync(technician, excessiveRecords);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooMany.StatusCode);
        Assert.Equal(0, await CountImportedAssetsAsync(app));
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string csv)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(csv, Encoding.UTF8, "text/csv"), "file", "assets.csv");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/assets/import") { Content = form };
        request.Headers.Add("X-CSRF-TOKEN", await TestApp.GetCsrfTokenAsync(client));
        return await client.SendAsync(request);
    }

    private static async Task<int> CountImportedAssetsAsync(TestApp app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        return await db.Assets.CountAsync(asset => asset.Id > 12);
    }
}
