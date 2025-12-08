# Integration Tests

This directory contains integration tests that use [Testcontainers](https://dotnet.testcontainers.org/) to spin up real PostgreSQL databases for testing.

## Prerequisites

- **Docker** must be installed and running on your machine
- Docker daemon must be accessible (check with `docker ps`)

## Running the Tests

### All Tests (Unit + Integration)
```bash
dotnet test
```

### Only Integration Tests
```bash
dotnet test --filter "FullyQualifiedName~Integration"
```

### Only Unit Tests (excluding Integration)
```bash
dotnet test --filter "FullyQualifiedName!~Integration"
```

## Test Structure

### PostgreSqlFixture.cs
A shared fixture that:
- Creates a PostgreSQL container using Testcontainers
- Initializes the database schema
- Provides methods to reset and seed test data
- Is shared across all tests in the "PostgreSQL" collection

### ProductRepositoryIntegrationTests.cs
Tests for `ProductRepository` including:
- CRUD operations on products
- Sales recording and stock updates
- Sales history queries
- Dashboard data generation
- Concurrent operation handling

### CachedProductRepositoryIntegrationTests.cs
Tests for `CachedProductRepository` including:
- Cache hit/miss behavior
- Cache invalidation on writes
- Data consistency with caching
- Per-product cache isolation

## How It Works

1. Before any test runs, Testcontainers starts a PostgreSQL 16 container
2. The database schema is created automatically
3. Each test class resets the database to ensure isolation
4. Tests can seed specific data using `SeedTestDataAsync()`
5. After all tests complete, the container is destroyed

## Troubleshooting

### Docker not running
```
Error: Cannot connect to the Docker daemon
```
**Solution:** Start Docker Desktop or the Docker daemon

### Port conflicts
Testcontainers uses random ports, so conflicts are rare. If you see port issues, ensure no other PostgreSQL instances are blocking.

### Slow first run
The first run downloads the PostgreSQL image (~150MB). Subsequent runs use the cached image.

## Writing New Integration Tests

1. Add the `[Collection("PostgreSQL")]` attribute to share the container
2. Inject `PostgreSqlFixture` in the constructor
3. Implement `IAsyncLifetime` for setup/teardown
4. Call `_fixture.ResetDatabaseAsync()` in `InitializeAsync()` for test isolation

Example:
```csharp
[Collection("PostgreSQL")]
public class MyIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;

    public MyIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        // Setup your repository/service here
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task MyTest()
    {
        await _fixture.SeedTestDataAsync();
        // Test code here
    }
}
```

