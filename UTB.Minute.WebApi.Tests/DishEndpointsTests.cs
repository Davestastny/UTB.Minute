using System.Net;
using System.Net.Http.Json;
using UTB.Minute.Contracts;
using Xunit;

namespace UTB.Minute.WebApi.Tests;

[Collection("Aspire")]
public class DishEndpointsTests
{
    private readonly HttpClient _client;

    public DishEndpointsTests(AspireFixture fixture)
    {
        _client = fixture.WebApiClient;
    }

    // -------------------------------------------------------------------------
    // GET /dishes
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllDishes_ReturnsOkWithDishes()
    {
        var response = await _client.GetAsync("/dishes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dishes = await response.Content.ReadFromJsonAsync<List<DishDto>>();
        Assert.NotNull(dishes);
        Assert.NotEmpty(dishes);
    }

    // -------------------------------------------------------------------------
    // POST /dishes + GET /dishes/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateDish_ReturnsCreatedAndCanBeRead()
    {
        var dto = new CreateDishDto("Test Dish", "A test dish description", 99m);

        var createResponse = await _client.PostAsJsonAsync("/dishes", dto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<DishDto>();
        Assert.NotNull(created);
        Assert.Equal("Test Dish", created.Name);
        Assert.Equal("A test dish description", created.Description);
        Assert.Equal(99m, created.Price);
        Assert.True(created.IsActive);

        // Verify it can be fetched by ID
        var getResponse = await _client.GetAsync($"/dishes/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<DishDto>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Test Dish", fetched.Name);
    }

    [Fact]
    public async Task GetDishById_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/dishes/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // PUT /dishes/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateDish_ReturnsOkWithUpdatedValues()
    {
        // Create a dish first
        var createDto = new CreateDishDto("Original Name", "Original description", 50m);
        var createResponse = await _client.PostAsJsonAsync("/dishes", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<DishDto>();
        Assert.NotNull(created);

        // Update it
        var updateDto = new UpdateDishDto("Updated Name", "Updated description", 75m, true);
        var updateResponse = await _client.PutAsJsonAsync($"/dishes/{created.Id}", updateDto);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<DishDto>();
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.Name);
        Assert.Equal("Updated description", updated.Description);
        Assert.Equal(75m, updated.Price);
    }

    [Fact]
    public async Task UpdateDish_NonExistent_ReturnsNotFound()
    {
        var dto = new UpdateDishDto("X", "Y", 1m, true);
        var response = await _client.PutAsJsonAsync("/dishes/999999", dto);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // PATCH /dishes/{id}/deactivate
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeactivateDish_SetsIsActiveFalse()
    {
        // Create an active dish
        var createDto = new CreateDishDto("Dish To Deactivate", "Will be deactivated", 60m);
        var createResponse = await _client.PostAsJsonAsync("/dishes", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<DishDto>();
        Assert.NotNull(created);
        Assert.True(created.IsActive);

        // Deactivate it
        var patchResponse = await _client.PatchAsync($"/dishes/{created.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var deactivated = await patchResponse.Content.ReadFromJsonAsync<DishDto>();
        Assert.NotNull(deactivated);
        Assert.False(deactivated.IsActive);
    }

    [Fact]
    public async Task DeactivateDish_NonExistent_ReturnsNotFound()
    {
        var response = await _client.PatchAsync("/dishes/999999/deactivate", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}