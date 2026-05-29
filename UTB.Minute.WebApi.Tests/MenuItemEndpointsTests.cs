using System.Net;
using System.Net.Http.Json;
using UTB.Minute.Contracts;
using Xunit;

namespace UTB.Minute.WebApi.Tests;

[Collection("Aspire")]
public class MenuItemEndpointsTests
{
    private readonly HttpClient _client;

    public MenuItemEndpointsTests(AspireFixture fixture)
    {
        _client = fixture.WebApiClient;
    }

    // Helper: create an active dish and return its id
    private async Task<int> CreateActiveDishAsync(string name = "Test Dish")
    {
        var dto = new CreateDishDto(name, "Description", 50m);
        var response = await _client.PostAsJsonAsync("/dishes", dto);
        response.EnsureSuccessStatusCode();
        var dish = await response.Content.ReadFromJsonAsync<DishDto>();
        return dish!.Id;
    }

    // -------------------------------------------------------------------------
    // GET /menu-items
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllMenuItems_ReturnsOk()
    {
        var response = await _client.GetAsync("/menu-items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<MenuItemDto>>();
        Assert.NotNull(items);
    }

    // -------------------------------------------------------------------------
    // POST /menu-items + GET /menu-items/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateMenuItem_ReturnsCreatedAndCanBeRead()
    {
        int dishId = await CreateActiveDishAsync("MenuItem Create Test Dish");
        var menuDate = DateTime.UtcNow.Date.AddDays(10);
        var dto = new CreateMenuItemDto(dishId, menuDate, 30);

        var createResponse = await _client.PostAsJsonAsync("/menu-items", dto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<MenuItemDto>();
        Assert.NotNull(created);
        Assert.Equal(dishId, created.DishId);
        Assert.Equal(30, created.AvailablePortions);

        // Read it back
        var getResponse = await _client.GetAsync($"/menu-items/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<MenuItemDto>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
    }

    [Fact]
    public async Task CreateMenuItem_InactiveDish_ReturnsBadRequest()
    {
        // Create dish and deactivate it
        int dishId = await CreateActiveDishAsync("Inactive Dish For MenuItem");
        await _client.PatchAsync($"/dishes/{dishId}/deactivate", null);

        var dto = new CreateMenuItemDto(dishId, DateTime.UtcNow.Date.AddDays(20), 10);
        var response = await _client.PostAsJsonAsync("/menu-items", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMenuItemById_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/menu-items/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // GET /menu-items/by-date/{date}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetMenuItemsByDate_ReturnsTodaysItems()
    {
        var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
        var response = await _client.GetAsync($"/menu-items/by-date/{today}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<MenuItemDto>>();
        Assert.NotNull(items);
        // Seeded data contains items for today
        Assert.NotEmpty(items);
    }

    // -------------------------------------------------------------------------
    // PUT /menu-items/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateMenuItem_ReturnsOkWithUpdatedValues()
    {
        int dishId = await CreateActiveDishAsync("MenuItem Update Test Dish");
        var menuDate = DateTime.UtcNow.Date.AddDays(30);
        var createDto = new CreateMenuItemDto(dishId, menuDate, 10);
        var createResponse = await _client.PostAsJsonAsync("/menu-items", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItemDto>();
        Assert.NotNull(created);

        var updateDto = new UpdateMenuItemDto(dishId, menuDate.AddDays(1), 99);
        var updateResponse = await _client.PutAsJsonAsync($"/menu-items/{created.Id}", updateDto);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<MenuItemDto>();
        Assert.NotNull(updated);
        Assert.Equal(99, updated.AvailablePortions);
    }

    [Fact]
    public async Task UpdateMenuItem_NonExistent_ReturnsNotFound()
    {
        var dto = new UpdateMenuItemDto(1, DateTime.UtcNow.Date, 5);
        var response = await _client.PutAsJsonAsync("/menu-items/999999", dto);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // DELETE /menu-items/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteMenuItem_ReturnsNoContentAndIsGone()
    {
        int dishId = await CreateActiveDishAsync("MenuItem Delete Test Dish");
        var menuDate = DateTime.UtcNow.Date.AddDays(40);
        var createDto = new CreateMenuItemDto(dishId, menuDate, 5);
        var createResponse = await _client.PostAsJsonAsync("/menu-items", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItemDto>();
        Assert.NotNull(created);

        var deleteResponse = await _client.DeleteAsync($"/menu-items/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Should now return 404
        var getResponse = await _client.GetAsync($"/menu-items/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteMenuItem_NonExistent_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/menu-items/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}