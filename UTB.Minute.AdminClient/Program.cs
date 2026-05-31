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

// AccessDenied endpoint
app.MapGet("/Account/AccessDenied", (HttpContext ctx) =>
{
    return Results.Content(@"
        <html>
        <head>
            <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css' />
            <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css' />
            <title>Access Denied</title>
        </head>
        <body class='bg-light d-flex align-items-center justify-content-center' style='height: 100vh;'>
            <div class='card shadow-lg p-4' style='max-width: 500px; width: 100%; border-radius: 12px;'>
                <div class='card-body text-center'>
                    <i class='bi bi-shield-slash-fill text-danger' style='font-size: 3.5rem;'></i>
                    <h3 class='card-title mt-3 text-danger'>Access Denied / Přístup odepřen</h3>
                    <p class='card-text text-muted mt-3'>
                        You do not have the required role to access this application. 
                        Please log out and log in with an authorized account.
                    </p>
                    <p class='card-text text-muted small'>
                        Nemáte potřebnou roli pro přístup k této aplikaci. 
                        Odhlaste se a přihlaste se pod účtem s příslušnou rolí (např. admin pro administraci).
                    </p>
                    <div class='mt-4'>
                        <a href='/logout' class='btn btn-danger w-100 py-2 fw-semibold'>
                            <i class='bi bi-box-arrow-right me-2'></i> Log out & Try again / Odhlásit se a zkusit znovu
                        </a>
                    </div>
                </div>
            </div>
        </body>
        </html>", "text/html; charset=utf-8");
});

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