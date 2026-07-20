using System.Net;
using System.Net.Http;
using PushoverHotkeys.Models;
using PushoverHotkeys.Services;
using Xunit;

namespace PushoverHotkeys.Tests;

public sealed class PushoverClientTests
{
    private const string Token = "aBcDeFgHiJkLmNoPqRsTuVwXyZ0123";
    private const string FirstKey = "zYxWvUtSrQpOnMlKjIhGfEdCbA9876";
    private const string SecondKey = "oPqRsTuVwXyZ0123aBcDeFgHiJkLmN";

    [Fact]
    public async Task SendAsync_UsesOneFormRequestWithConfiguredMessagePriorityAndSound()
    {
        string? requestBody = null;
        var handler = new StubHandler(async request =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(HttpStatusCode.OK, "{\"status\":1}");
        });
        var client = new PushoverClient(new HttpClient(handler));

        var result = await client.SendAsync(Token,
        [
            new Recipient { UserKey = FirstKey },
            new Recipient { UserKey = SecondKey }
        ], "Проверьте сервер", (int)PushoverPriority.High, "siren");

        Assert.True(result.IsSuccess);
        Assert.NotNull(requestBody);
        Assert.True(requestBody.Contains("token=" + Token, StringComparison.Ordinal));
        Assert.True(requestBody.Contains("message=%D0%9F%D1%80%D0%BE%D0%B2%D0%B5%D1%80%D1%8C%D1%82%D0%B5+%D1%81%D0%B5%D1%80%D0%B2%D0%B5%D1%80", StringComparison.Ordinal));
        Assert.True(requestBody.Contains("priority=1", StringComparison.Ordinal));
        Assert.True(requestBody.Contains("sound=siren", StringComparison.Ordinal));
        Assert.True(requestBody.Contains("user=" + FirstKey + "%2C" + SecondKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendAsync_ReportsPushoverErrors()
    {
        var handler = new StubHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, "{\"errors\":[\"user identifier is invalid\"],\"status\":0}")));
        var client = new PushoverClient(new HttpClient(handler));

        var result = await client.SendAsync(Token, [new Recipient { UserKey = FirstKey }], "GM", 0, PushoverSounds.DefaultId);

        Assert.False(result.IsSuccess);
        Assert.True(result.ErrorMessage.Contains("user identifier is invalid", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendAsync_ReportsTimeoutWithoutRetrying()
    {
        var handler = new StubHandler(_ => Task.FromException<HttpResponseMessage>(new TaskCanceledException()));
        var client = new PushoverClient(new HttpClient(handler));

        var result = await client.SendAsync(Token, [new Recipient { UserKey = FirstKey }], "GM", 0, PushoverSounds.DefaultId);

        Assert.False(result.IsSuccess);
        Assert.True(result.ErrorMessage.Contains("10 секунд", StringComparison.Ordinal));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) => new(statusCode)
    {
        Content = new StringContent(json)
    };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => responseFactory(request);
    }
}
