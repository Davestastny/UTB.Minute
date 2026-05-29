using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace UTB.Minute.AdminClient.Services;

public class TokenProvider : IDisposable
{
    private readonly PersistentComponentState _state;
    private PersistingComponentStateSubscription _subscription;
    private string? _accessToken;

    public TokenProvider(PersistentComponentState state)
    {
        _state = state;

        // Try to retrieve token from persistent state (runs during interactive initialization)
        if (_state.TryTakeFromJson<string>("AccessToken", out var token))
        {
            _accessToken = token;
        }

        // Register callback to persist the token (runs during pre-rendering before serialization)
        _subscription = _state.RegisterOnPersisting(() =>
        {
            _state.PersistAsJson("AccessToken", _accessToken);
            return Task.CompletedTask;
        });
    }

    public string? AccessToken
    {
        get => _accessToken;
        set => _accessToken = value;
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }
}

public class BearerTokenHandler : DelegatingHandler
{
    private readonly TokenProvider _tokenProvider;

    public BearerTokenHandler(TokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _tokenProvider.AccessToken;
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

public class ScopedHttpClientFactory : IHttpClientFactory
{
    private readonly IHttpMessageHandlerFactory _handlerFactory;
    private readonly TokenProvider _tokenProvider;

    public ScopedHttpClientFactory(IHttpMessageHandlerFactory handlerFactory, TokenProvider tokenProvider)
    {
        _handlerFactory = handlerFactory;
        _tokenProvider = tokenProvider;
    }

    public HttpClient CreateClient(string name)
    {
        var handler = _handlerFactory.CreateHandler(name);
        var client = new HttpClient(handler, disposeHandler: false);
        if (name == "webapi")
        {
            client.BaseAddress = new Uri("https+http://webapi");
            var token = _tokenProvider.AccessToken;
            System.Console.WriteLine($"[DEBUG Admin ScopedHttpClientFactory] Creating webapi client - Injecting Bearer Token: {!string.IsNullOrEmpty(token)} (Length: {token?.Length ?? 0})");
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        return client;
    }
}
