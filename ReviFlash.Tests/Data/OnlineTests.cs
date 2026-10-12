using System.Net;
using ReviFlash.Data.Online;

namespace ReviFlash.Tests.Data;

public class OnlineTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Reply(HttpStatusCode status, string body = "{}") =>
        new(status) { Content = new StringContent(body) };

    [Fact]
    public async Task Sign_out_revokes_only_this_devices_session()
    {
        var handler = new FakeHandler(_ => Reply(HttpStatusCode.NoContent, ""));
        using var client = new SupabaseConnection("token", handler);

        await client.SignOutAsync();

        Assert.Equal("scope=local", Assert.Single(handler.Requests).RequestUri!.Query.TrimStart('?'));
        Assert.EndsWith("/auth/v1/logout", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Theory]
    [InlineData(400, true)]
    [InlineData(401, true)]
    [InlineData(408, false)]
    [InlineData(429, false)]
    [InlineData(500, false)]
    [InlineData(503, false)]
    [InlineData(0, false)]
    public void Only_a_client_error_rejects_a_session(int status, bool rejected)
    {
        Assert.Equal(rejected, SupabaseConnection.IsRejection(status));
    }

    [Fact]
    public async Task A_refresh_that_cannot_reach_the_server_is_not_a_rejection()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("offline"));
        using var client = new SupabaseConnection("token", handler);

        var result = await client.RefreshSessionAsync("refresh");

        Assert.False(result.Success);
        Assert.False(result.Rejected);
    }

    [Fact]
    public async Task A_refresh_the_server_refuses_is_a_rejection()
    {
        var handler = new FakeHandler(_ => Reply(HttpStatusCode.BadRequest, """{"error_description":"Invalid Refresh Token"}"""));
        using var client = new SupabaseConnection("token", handler);

        var result = await client.RefreshSessionAsync("refresh");

        Assert.True(result.Rejected);
    }

    [Fact]
    public async Task Listing_cloud_decks_offline_throws()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("offline"));
        using var client = new SupabaseConnection("token", handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetUserCloudDecksAsync("user"));
    }

    [Fact]
    public async Task Listing_cloud_decks_throws_on_a_server_error_instead_of_returning_an_empty_list()
    {
        var handler = new FakeHandler(_ => Reply(HttpStatusCode.InternalServerError));
        using var client = new SupabaseConnection("token", handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetUserCloudDecksAsync("user"));
    }

    [Fact]
    public async Task Listing_cloud_decks_returns_an_empty_account_as_empty()
    {
        var handler = new FakeHandler(_ => Reply(HttpStatusCode.OK, "[]"));
        using var client = new SupabaseConnection("token", handler);

        Assert.Empty(await client.GetUserCloudDecksAsync("user"));
    }
}
