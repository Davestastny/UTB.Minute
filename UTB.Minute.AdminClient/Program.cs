using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using UTB.Minute.AdminClient.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// HttpClient for WebApi via Aspire Service Discovery
builder.Services.AddScoped<UTB.Minute.AdminClient.Services.TokenProvider>();
builder.Services.AddScoped<IHttpClientFactory, UTB.Minute.AdminClient.Services.ScopedHttpClientFactory>();
builder.Services.AddTransient<UTB.Minute.AdminClient.Services.BearerTokenHandler>();

builder.Services.AddHttpClient("webapi", client =>
{
    client.BaseAddress = new Uri("https+http://webapi");
})
.AddHttpMessageHandler<UTB.Minute.AdminClient.Services.BearerTokenHandler>();

// Keycloak OIDC authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Authority = builder.Configuration["Keycloak:Authority"];
    options.RequireHttpsMetadata = false;
    options.MapInboundClaims = false;
    options.ClientId = builder.Configuration["Keycloak:ClientId"] ?? "admin-client";
    options.ClientSecret = builder.Configuration["Keycloak:ClientSecret"] ?? "admin-secret";
    options.ResponseType = "code";
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    options.TokenValidationParameters.NameClaimType = "preferred_username";
    options.TokenValidationParameters.RoleClaimType = "roles";
    
    // Explicitly map "roles" and Keycloak's default nested "realm_access" claims
    options.ClaimActions.MapJsonKey("roles", "roles");
    options.ClaimActions.MapJsonKey("realm_access", "realm_access");

    options.Events = new OpenIdConnectEvents
    {
        OnTicketReceived = context =>
        {
            if (context.Principal?.Identity is System.Security.Claims.ClaimsIdentity identity)
            {
                // 1. Try to extract roles from standard "roles" claim (JSON array or single string)
                var roleClaims = identity.FindAll("roles").ToList();

                // 2. Try to extract roles from Keycloak's default nested "realm_access" object
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
                                    if (!roleClaims.Any(c => c.Value == r))
                                    {
                                        var newClaim = new System.Security.Claims.Claim("roles", r);
                                        identity.AddClaim(newClaim);
                                        roleClaims.Add(newClaim);
                                    }
                                }
                            }
                        }
                    }
                    catch { /* ignore invalid JSON */ }
                }

                // 3. For each role claim found, ensure it is registered under both standard ClaimTypes.Role and "roles"
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
                        catch
                        {
                            if (!identity.HasClaim(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == val))
                            {
                                identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, val));
                            }
                            if (!identity.HasClaim(c => c.Type == "roles" && c.Value == val))
                            {
                                identity.AddClaim(new System.Security.Claims.Claim("roles", val));
                            }
                        }
                    }
                    else
                    {
                        if (!identity.HasClaim(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == val))
                        {
                            identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, val));
                        }
                        if (!identity.HasClaim(c => c.Type == "roles" && c.Value == val))
                        {
                            identity.AddClaim(new System.Security.Claims.Claim("roles", val));
                        }
                    }
                }
            }
            return System.Threading.Tasks.Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

// Logout endpoint
app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
});

// Debug claims endpoint
app.MapGet("/debug-claims", (System.Security.Claims.ClaimsPrincipal user) =>
{
    return user.Claims.Select(c => new { c.Type, c.Value }).ToList();
});

// Debug token endpoint
app.MapGet("/debug-token", async (HttpContext ctx) =>
{
    var token = await ctx.GetTokenAsync(Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectDefaults.AuthenticationScheme, "access_token");
    var tokenDefault = await ctx.GetTokenAsync("access_token");
    return Results.Ok(new { Token = token, TokenDefault = tokenDefault });
});

app.Run();