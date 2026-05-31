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
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is System.Security.Claims.ClaimsIdentity identity)
                {
                    // Extract and map roles from Keycloak's standard "realm_access" claim
                    var realmAccessClaim = identity.FindFirst("realm_access");
                    if (realmAccessClaim != null)
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(realmAccessClaim.Value);
                            if (doc.RootElement.TryGetProperty("roles", out var rolesElem) && rolesElem.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                foreach (var roleElem in rolesElem.EnumerateArray())
                                {
                                    var r = roleElem.GetString();
                                    if (!string.IsNullOrEmpty(r))
                                    {
                                        // Map to the default XML Role claim type
                                        if (!identity.HasClaim(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == r))
                                        {
                                            identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, r));
                                        }
                                        // Also map to the custom "roles" claim type configured in WebApi
                                        if (!identity.HasClaim(c => c.Type == "roles" && c.Value == r))
                                        {
                                            identity.AddClaim(new System.Security.Claims.Claim("roles", r));
                                        }
                                    }
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            System.Console.WriteLine($"[JWT Role Mapping Error]: {ex.Message}");
                        }
                    }

                    // Also support standard "roles" claim if it's already a string or string array in the token
                    var roleClaims = identity.FindAll("roles").ToList();
                    foreach (var claim in roleClaims)
                    {
                        var val = claim.Value.Trim();
                        if (val.StartsWith("[") && val.EndsWith("]"))
                        {
                            try
                            {
                                var roles = System.Text.Json.JsonSerializer.Deserialize<string[]>(val);
                                if (roles != null)
                                {
                                    foreach (var role in roles)
                                    {
                                        if (!identity.HasClaim(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == role))
                                        {
                                            identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role));
                                        }
                                        if (!identity.HasClaim(c => c.Type == "roles" && c.Value == role))
                                        {
                                            identity.AddClaim(new System.Security.Claims.Claim("roles", role));
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                        else
                        {
                            if (!identity.HasClaim(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == val))
                            {
                                identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, val));
                            }
                        }
                    }
                }
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                System.Console.WriteLine($"[JWT AUTHENTICATION FAILED] Path: {context.Request.Path} | Error: {context.Exception.Message}");
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