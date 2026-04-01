using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CyberArkManager.Models;
using CyberArkManager.Services;
using Xunit;

namespace CyberArkManager.Tests;

public class CyberArkApiServiceTests
{
    [Fact]
    public async Task CreateAccountAsync_SendsExpectedJsonPayload()
    {
        var handler = new RecordingHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """
            {
              "id": "1",
              "name": "root@server01",
              "address": "server01",
              "userName": "root",
              "platformId": "UnixSSH",
              "safeName": "UnixSafe"
            }
            """);

        var service = CreateService(handler);

        await service.CreateAccountAsync(new AccountCreateRequest
        {
            Name = "root@server01",
            Address = "server01",
            UserName = "root",
            PlatformId = "UnixSSH",
            SafeName = "UnixSafe",
            Secret = "P@ssw0rd!",
            SecretManagement = new SecretManagementRequest
            {
                AutomaticManagementEnabled = false,
                ManualManagementReason = "Carga inicial"
            }
        });

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://pvwa.example.local/API/Accounts", request.RequestUri!.ToString());

        using var document = JsonDocument.Parse(request.Body!);
        var root = document.RootElement;
        Assert.Equal("root@server01", root.GetProperty("name").GetString());
        Assert.Equal("server01", root.GetProperty("address").GetString());
        Assert.Equal("root", root.GetProperty("userName").GetString());
        Assert.Equal("UnixSSH", root.GetProperty("platformId").GetString());
        Assert.Equal("UnixSafe", root.GetProperty("safeName").GetString());
        Assert.Equal("P@ssw0rd!", root.GetProperty("secret").GetString());
        Assert.False(root.GetProperty("secretManagement").GetProperty("automaticManagementEnabled").GetBoolean());
        Assert.Equal("Carga inicial", root.GetProperty("secretManagement").GetProperty("manualManagementReason").GetString());
    }

    [Fact]
    public async Task LinkAccountAsync_SendsExpectedJsonPayload()
    {
        var handler = new RecordingHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{}");

        var service = CreateService(handler);

        await service.LinkAccountAsync("target-123", "linked-987", 3);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://pvwa.example.local/API/Accounts/target-123/LinkAccount", request.RequestUri!.ToString());

        using var document = JsonDocument.Parse(request.Body!);
        var root = document.RootElement;
        Assert.Equal(3, root.GetProperty("extraPasswordIndex").GetInt32());
        Assert.True(root.GetProperty("linked").GetBoolean());
        Assert.Equal("linked-987", root.GetProperty("associatedLinkedAccountId").GetString());
    }

    [Fact]
    public async Task GetAccountsAsync_FollowsRelativeNextLink()
    {
        var handler = new RecordingHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """
            {
              "value": [
                {
                  "id": "1",
                  "name": "root@server01",
                  "address": "server01",
                  "userName": "root",
                  "platformId": "UnixSSH",
                  "safeName": "UnixSafe"
                }
              ],
              "count": 1,
              "nextLink": "/API/Accounts?limit=100&offset=100"
            }
            """);
        handler.EnqueueJson(HttpStatusCode.OK, """
            {
              "value": [
                {
                  "id": "2",
                  "name": "svc@server02",
                  "address": "server02",
                  "userName": "svc_app",
                  "platformId": "WinDomain",
                  "safeName": "WindowsSafe"
                }
              ],
              "count": 1
            }
            """);

        var service = CreateService(handler);

        var accounts = await service.GetAccountsAsync(search: "server");

        Assert.Equal(2, accounts.Count);
        Assert.Equal("https://pvwa.example.local/API/Accounts?limit=100&offset=0&search=server", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal("https://pvwa.example.local/API/Accounts?limit=100&offset=100", handler.Requests[1].RequestUri!.ToString());
    }

    static CyberArkApiService CreateService(RecordingHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var authService = new AuthService(httpClient);

        typeof(AuthService)
            .GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(authService, new UserSession
            {
                PvwaUrl = "https://pvwa.example.local",
                Token = "token",
                Username = "tester",
                HardExpiry = DateTime.Now.AddHours(1)
            });

        return new CyberArkApiService(httpClient, authService);
    }

    sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        readonly Queue<HttpResponseMessage> _responses = new();

        public List<CapturedRequest> Requests { get; } = new();

        public void EnqueueJson(HttpStatusCode statusCode, string json)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new CapturedRequest
            {
                Method = request.Method,
                RequestUri = request.RequestUri,
                Body = body
            });

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No fake response configured for request.");
            }

            return _responses.Dequeue();
        }
    }

    sealed class CapturedRequest
    {
        public HttpMethod Method { get; init; } = HttpMethod.Get;
        public Uri? RequestUri { get; init; }
        public string? Body { get; init; }
    }
}
