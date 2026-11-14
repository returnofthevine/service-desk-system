using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ServiceDesk.Api.Tests;

public sealed class TicketPaginationTests
{
    // Verify that moving between pages preserves the filter, total, and ordering.
    [Fact]
    public async Task FilteredPagesKeepTheStableOrderAndMatchingCount()
    {
        await using var app = new TestApp();
        await app.PrepareDatabaseAsync();
        using var client = await app.CreateSignedInClientAsync();

        await AssertPageAsync(client, 1, [1, 7]);
        await AssertPageAsync(client, 2, [19]);
        await AssertPageAsync(client, 3, []);
        await AssertPageAsync(client, int.MaxValue, []);

        var legacyQueue = await client.GetFromJsonAsync<JsonElement>(
            "/api/tickets?status=open&priority=P2");
        Assert.Equal(new[] { 1, 7, 19 },
            legacyQueue.EnumerateArray().Select(item => item.GetProperty("id").GetInt32()));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("priority=P9")]
    [InlineData("status=pending")]
    public async Task InvalidPageArgumentsReturnBadRequest(string query)
    {
        await using var app = new TestApp();
        await app.PrepareDatabaseAsync();
        using var client = await app.CreateSignedInClientAsync();

        using var response = await client.GetAsync($"/api/v2/tickets?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task AssertPageAsync(HttpClient client, int pageNumber, int[] expectedIds)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v2/tickets?status=OPEN&priority=p2&page={pageNumber}&pageSize=2");

        Assert.Equal(pageNumber, page.GetProperty("page").GetInt32());
        Assert.Equal(2, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page.GetProperty("totalPages").GetInt32());
        Assert.Equal(expectedIds, page.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt32()));
    }
}
