using Xunit;

namespace UTB.Minute.WebApi.Tests;

/// <summary>
/// Defines a shared xUnit collection so all test classes share a single
/// AspireFixture instance (one PostgreSQL + AppHost startup per test run).
/// </summary>
[CollectionDefinition("Aspire")]
public class AspireCollection : ICollectionFixture<AspireFixture>
{
}