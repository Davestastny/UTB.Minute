var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var minutedb = postgres.AddDatabase("minutedb");

// DbManager (Seeds DB on startup)
var dbManager = builder.AddProject<Projects.UTB_Minute_DbManager>("dbmanager")
    .WithReference(minutedb)
    .WithHttpCommand("/dev/seed", "Reset Database")
    .WaitFor(minutedb);

// Keycloak
var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume()
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
    .WithRealmImport("../keycloak");

// WebAPI
var webApi = builder.AddProject<Projects.UTB_Minute_WebApi>("webapi")
    .WithHttpEndpoint(port: 5258, name: "external-http")
    .WithReference(minutedb)
    .WithReference(keycloak)
    .WithEnvironment("Keycloak__Authority", $"{keycloak.GetEndpoint("http")}/realms/minute")
    .WaitFor(dbManager)
    .WaitFor(keycloak);

// AdminClient (Blazor Server)
var adminClient = builder.AddProject<Projects.UTB_Minute_AdminClient>("adminclient")
    .WithHttpEndpoint(port: 61971, name: "external-http")
    .WithReference(webApi)
    .WithReference(keycloak)
    .WithEnvironment("Keycloak__Authority", $"{keycloak.GetEndpoint("http")}/realms/minute")
    .WaitFor(webApi)
    .WaitFor(keycloak);

// CanteenClient (Blazor Server)
var canteenClient = builder.AddProject<Projects.UTB_Minute_Web>("canteenclient")
    .WithHttpEndpoint(port: 5014, name: "external-http")
    .WithReference(webApi)
    .WithReference(keycloak)
    .WithEnvironment("Keycloak__Authority", $"{keycloak.GetEndpoint("http")}/realms/minute")
    .WaitFor(webApi)
    .WaitFor(keycloak);

builder.Build().Run();