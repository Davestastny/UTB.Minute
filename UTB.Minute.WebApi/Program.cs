using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using UTB.Minute.Db;
using UTB.Minute.WebApi;
using UTB.Minute.WebApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<MinuteDbContext>("minutedb");

builder.Services.AddSingleton<SseHub>();
builder.Services.AddOpenApi();

// JWT Bearer autentizace přes Keycloak
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.RequireHttpsMetadata = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new()
        {
            RoleClaimType = "roles",
            ValidateAudience = false
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var authHeader = context.Request.Headers["Authorization"].ToString();
                var msg = $"[JWT RECEIVED] {System.DateTime.UtcNow}: Path: {context.Request.Path} | Has Auth Header: {!string.IsNullOrEmpty(authHeader)} | Length: {authHeader?.Length ?? 0}";
                System.IO.File.AppendAllText("e:\\programko\\AF\\UTB.Minute-main\\webapi_auth_errors.log", msg + System.Environment.NewLine);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var msg = $"[JWT VALIDATED] {System.DateTime.UtcNow}: User: {context.Principal?.Identity?.Name} | Has Roles Claim: {context.Principal?.HasClaim(c => c.Type == "roles")}";
                System.IO.File.AppendAllText("e:\\programko\\AF\\UTB.Minute-main\\webapi_auth_errors.log", msg + System.Environment.NewLine);
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                var authHeader = context.Request.Headers["Authorization"].ToString();
                var token = authHeader.StartsWith("Bearer ", System.StringComparison.OrdinalIgnoreCase)
                    ? authHeader.Substring(7)
                    : authHeader;
                var errorMsg = $"[JWT FAILED] {System.DateTime.UtcNow}: {context.Exception.Message} | Token: {token}";
                if (context.Exception.InnerException != null)
                {
                    errorMsg += $" | Inner: {context.Exception.InnerException.Message}";
                }
                System.IO.File.AppendAllText("e:\\programko\\AF\\UTB.Minute-main\\webapi_auth_errors.log", errorMsg + System.Environment.NewLine);
                System.Console.WriteLine(errorMsg);
                return Task.CompletedTask;
            }
        };
    });

// Autorizační policy
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOrCook", p => p.RequireRole("admin", "cook"));
    options.AddPolicy("Admin", p => p.RequireRole("admin"));
    options.AddPolicy("Student", p => p.RequireRole("student"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.MapDishEndpoints();
app.MapMenuItemEndpoints();
app.MapOrderEndpoints();
app.MapSseEndpoints();

app.Run();

public partial class Program { }