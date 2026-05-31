using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UTB.Minute.Contracts;

namespace UTB.Minute.Web.Services;

public class SseListenerService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<SseListenerService> _logger;

    public event Action<string, JsonDocument>? OnEventReceived;

    public SseListenerService(IServiceScopeFactory serviceScopeFactory, ILogger<SseListenerService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
                using var client = httpClientFactory.CreateClient("webapi");
                using var response = await client.GetAsync("/sse", HttpCompletionOption.ResponseHeadersRead, stoppingToken);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(stoppingToken);
                using var reader = new StreamReader(stream);

                string? eventType = null;

                while (!stoppingToken.IsCancellationRequested && !reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(stoppingToken);

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        eventType = null; // End of event
                        continue;
                    }

                    if (line.StartsWith("event: "))
                    {
                        eventType = line["event: ".Length..].Trim();
                    }
                    else if (line.StartsWith("data: ") && eventType != null)
                    {
                        var data = line["data: ".Length..].Trim();
                        try
                        {
                            var jsonDoc = JsonDocument.Parse(data);
                            OnEventReceived?.Invoke(eventType, jsonDoc);
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse SSE JSON data");
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "SSE connection error. Retrying in 5 seconds...");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
