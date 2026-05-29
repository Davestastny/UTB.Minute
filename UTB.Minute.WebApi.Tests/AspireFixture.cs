using Aspire.Hosting.Testing;
using Projects;
using System.Net.Http.Json;
using Xunit;

namespace UTB.Minute.WebApi.Tests;

/// <summary>
/// Shared Aspire test fixture that starts the full application (PostgreSQL + DbManager + WebApi + Keycloak)
/// and resets the database before the test collection runs.
/// Obtains a Keycloak access token (cook role) so that protected WebApi endpoints can be called from tests.
/// </summary>
public sealed class AspireFixture : IAsyncLifetime
{
    private DistributedApplicationFactory? _factory;
    private HttpClient? _webApiClient;
    private HttpClient? _dbManagerClient;

    public HttpClient WebApiClient => _webApiClient!;
    public HttpClient DbManagerClient => _dbManagerClient!;

    public async Task InitializeAsync()
    {
        _factory = new DistributedApplicationFactory(typeof(UTB_Minute_AppHost));

        await _factory.StartAsync();

        _webApiClient    = _factory.CreateHttpClient("webapi");
        _dbManagerClient = _factory.CreateHttpClient("dbmanager");

        // Reset and seed the database before the test run
        var resetResponse = await _dbManagerClient.PostAsync("/dev/seed", null);
        resetResponse.EnsureSuccessStatusCode();

        // Give the WebApi a moment to detect the schema is ready
        await Task.Delay(500);

        // Obtain an access token from Keycloak using cook credentials (has AdminOrCook role)
        // so that tests can call protected endpoints
        await SetAuthorizationTokenAsync();
    }

    private async Task SetAuthorizationTokenAsync()
    {
        // Get Keycloak base URL from the factory
        var keycloakHttpClient = _factory!.CreateHttpClient("keycloak");

        // Retry a few times since Keycloak may still be starting
        string lastError = "";
        for (int attempt = 0; attempt < 15; attempt++)
        {
            try
            {
                var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"]    = "password",
                    ["client_id"]     = "admin-client",
                    ["client_secret"] = "admin-secret",
                    ["username"]      = "cook",
                    ["password"]      = "cook123",
                    ["scope"]         = "openid"
                });

                var tokenResponse = await keycloakHttpClient.PostAsync(
                    "/realms/minute/protocol/openid-connect/token",
                    tokenRequest);

                if (tokenResponse.IsSuccessStatusCode)
                {
                    var tokenData = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
                    if (tokenData?.AccessToken is not null)
                    {
                        string payloadJson = "";
                        try
                        {
                            var parts = tokenData.AccessToken.Split('.');
                            if (parts.Length > 1)
                            {
                                var padLength = (4 - parts[1].Length % 4) % 4;
                                var base64 = parts[1].Replace('-', '+').Replace('_', '/') + new string('=', padLength);
                                var payloadBytes = Convert.FromBase64String(base64);
                                payloadJson = System.Text.Encoding.UTF8.GetString(payloadBytes);
                                Console.WriteLine($"[JWT PAYLOAD]: {payloadJson}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[JWT DECODE ERROR]: {ex.Message}");
                        }

                        _webApiClient!.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenData.AccessToken);
                        return;
                    }
                }
                else
                {
                    var content = await tokenResponse.Content.ReadAsStringAsync();
                    lastError = $"Status: {tokenResponse.StatusCode}, Body: {content}";
                }
            }
            catch (Exception ex)
            {
                lastError = $"Exception: {ex.Message}";
            }

            await Task.Delay(2000);
        }

        throw new InvalidOperationException($"Could not obtain Keycloak access token for tests. Last error: {lastError}");
    }

    public async Task DisposeAsync()
    {
        _webApiClient?.Dispose();
        _dbManagerClient?.Dispose();

        if (_factory is not null)
            await _factory.DisposeAsync();
    }

    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")]
        string? AccessToken);
}