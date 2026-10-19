using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LexFlow.Web.Common.Security;

namespace LexFlow.Api.IntegrationTests;

/// <summary>Cliente do protocolo JSON/LongPolling do SignalR, sem dependência de um SDK de cliente.</summary>
internal sealed class SignalRNotificationProbe(HttpClient client) : IAsyncDisposable
{
    private const string Hub = "/api/v1/notifications/hub";
    private string _transport = string.Empty;

    public async Task ConnectAsync(SeededMember member, CancellationToken cancellationToken)
    {
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, member.UserId.ToString());
        client.DefaultRequestHeaders.Add(TenantResolutionMiddleware.HeaderName, member.OrganizationId.ToString());
        using var negotiated = await client.PostAsync(Hub + "/negotiate?negotiateVersion=1", null, cancellationToken);
        negotiated.EnsureSuccessStatusCode();
        var body = await negotiated.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        _transport = Hub + "?id=" + Uri.EscapeDataString(body.GetProperty("connectionToken").GetString()!);
        using var initialPoll = await client.GetAsync(_transport, cancellationToken);
        initialPoll.EnsureSuccessStatusCode();
        await SendAsync("{\"protocol\":\"json\",\"version\":1}", cancellationToken);
        using var handshake = await client.GetAsync(_transport, cancellationToken);
        handshake.EnsureSuccessStatusCode();
        (await handshake.Content.ReadAsStringAsync(cancellationToken)).ShouldContain("{}");
        await SendAsync("{\"type\":4,\"invocationId\":\"watch\",\"target\":\"Watch\",\"arguments\":[]}", cancellationToken);
    }

    public async Task<JsonElement> NextAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        while (true)
        {
            using var response = await client.GetAsync(_transport, timeout.Token);
            response.EnsureSuccessStatusCode();
            foreach (var frame in (await response.Content.ReadAsStringAsync(timeout.Token)).Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
            {
                using var json = JsonDocument.Parse(frame);
                var type = json.RootElement.GetProperty("type").GetInt32();
                type.ShouldNotBe(3, frame);
                type.ShouldNotBe(7, frame);
                if (type == 2) { return json.RootElement.GetProperty("item").Clone(); }
            }
        }
    }

    private async Task SendAsync(string frame, CancellationToken cancellationToken)
    {
        using var content = new StringContent(frame + '\u001e', Encoding.UTF8, "text/plain");
        using var response = await client.PostAsync(_transport, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async ValueTask DisposeAsync()
    {
        if (_transport.Length != 0)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var response = await client.DeleteAsync(_transport, timeout.Token);
        }
        client.Dispose();
    }
}
