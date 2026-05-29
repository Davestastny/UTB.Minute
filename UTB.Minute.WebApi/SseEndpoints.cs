namespace UTB.Minute.WebApi.Endpoints;

public static class SseEndpoints
{
    public static void MapSseEndpoints(this WebApplication app)
    {
        app.MapGet("/sse", async (SseHub hub, HttpContext ctx, CancellationToken ct) =>
        {
            ctx.Response.Headers.Append("Content-Type", "text/event-stream");
            ctx.Response.Headers.Append("Cache-Control", "no-cache");
            ctx.Response.Headers.Append("X-Accel-Buffering", "no");

            // Send initial heartbeat
            await ctx.Response.WriteAsync("event: connected\ndata: {}\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);

            await foreach (var message in hub.Subscribe(ct))
            {
                await ctx.Response.WriteAsync(message, ct);
                await ctx.Response.Body.FlushAsync(ct);
            }
        }).WithTags("SSE");
    }
}