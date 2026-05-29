using System.Net;
using System.Net.Http.Json;
using UTB.Minute.Contracts;
using Xunit;

namespace UTB.Minute.WebApi.Tests;

[Collection("Aspire")]
public class OrderEndpointsTests
{
    private readonly HttpClient _client;

    public OrderEndpointsTests(AspireFixture fixture)
    {
        _client = fixture.WebApiClient;
    }

    // Helper: create a dish + menu item and return the menu item id
    private async Task<int> CreateMenuItemAsync(int portions = 10)
    {
        var dishDto = new CreateDishDto("Order Test Dish", "For order tests", 60m);
        var dishResponse = await _client.PostAsJsonAsync("/dishes", dishDto);
        dishResponse.EnsureSuccessStatusCode();
        var dish = await dishResponse.Content.ReadFromJsonAsync<DishDto>();

        // Use a unique date far in the future to avoid unique-constraint conflicts across tests
        var menuDate = DateTime.UtcNow.Date.AddDays(Random.Shared.Next(100, 9999));
        var menuDto = new CreateMenuItemDto(dish!.Id, menuDate, portions);
        var menuResponse = await _client.PostAsJsonAsync("/menu-items", menuDto);
        menuResponse.EnsureSuccessStatusCode();
        var menuItem = await menuResponse.Content.ReadFromJsonAsync<MenuItemDto>();
        return menuItem!.Id;
    }

    // -------------------------------------------------------------------------
    // POST /orders + GET /orders + GET /orders/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateOrder_ReturnsCreatedAndIsRetrievable()
    {
        int menuItemId = await CreateMenuItemAsync(5);
        var dto = new CreateOrderDto(menuItemId, "student-test-001");

        var createResponse = await _client.PostAsJsonAsync("/orders", dto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(created);
        Assert.Equal(menuItemId, created.MenuItemId);
        Assert.Equal("student-test-001", created.StudentId);
        Assert.Equal(0, created.Status); // Preparing

        // Fetch by id
        var getResponse = await _client.GetAsync($"/orders/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
    }

    [Fact]
    public async Task CreateOrder_NoPortionsLeft_ReturnsBadRequest()
    {
        int menuItemId = await CreateMenuItemAsync(0); // 0 portions
        var dto = new CreateOrderDto(menuItemId, "student-no-portions");

        var response = await _client.PostAsJsonAsync("/orders", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_NonExistentMenuItem_ReturnsBadRequest()
    {
        var dto = new CreateOrderDto(999999, "student-no-item");

        var response = await _client.PostAsJsonAsync("/orders", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetActiveOrders_ReturnsOkWithNonCompletedOrders()
    {
        var response = await _client.GetAsync("/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orders = await response.Content.ReadFromJsonAsync<List<OrderDto>>();
        Assert.NotNull(orders);
        // All returned orders must NOT be Completed (status 3)
        Assert.All(orders, o => Assert.NotEqual(3, o.Status));
    }

    [Fact]
    public async Task GetOrderById_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/orders/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // PATCH /orders/{id}/status – valid transitions
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateOrderStatus_Preparing_To_Ready_Succeeds()
    {
        int menuItemId = await CreateMenuItemAsync();
        var createDto = new CreateOrderDto(menuItemId, "student-tr-ready");
        var createResponse = await _client.PostAsJsonAsync("/orders", createDto);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);

        var statusDto = new UpdateOrderStatusDto(1); // Ready
        var patchResponse = await _client.PatchAsJsonAsync($"/orders/{order.Id}/status", statusDto);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var updated = await patchResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(updated);
        Assert.Equal(1, updated.Status);
    }

    [Fact]
    public async Task UpdateOrderStatus_Preparing_To_Cancelled_Succeeds()
    {
        int menuItemId = await CreateMenuItemAsync();
        var createDto = new CreateOrderDto(menuItemId, "student-tr-cancel");
        var createResponse = await _client.PostAsJsonAsync("/orders", createDto);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);

        var statusDto = new UpdateOrderStatusDto(2); // Cancelled
        var patchResponse = await _client.PatchAsJsonAsync($"/orders/{order.Id}/status", statusDto);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var updated = await patchResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(updated);
        Assert.Equal(2, updated.Status);
    }

    [Fact]
    public async Task UpdateOrderStatus_Ready_To_Completed_Succeeds()
    {
        int menuItemId = await CreateMenuItemAsync();
        var createDto = new CreateOrderDto(menuItemId, "student-tr-complete");
        var createResponse = await _client.PostAsJsonAsync("/orders", createDto);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);

        // Preparing -> Ready
        await _client.PatchAsJsonAsync($"/orders/{order.Id}/status", new UpdateOrderStatusDto(1));

        // Ready -> Completed
        var patchResponse = await _client.PatchAsJsonAsync($"/orders/{order.Id}/status", new UpdateOrderStatusDto(3));
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var updated = await patchResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(updated);
        Assert.Equal(3, updated.Status);
    }

    // -------------------------------------------------------------------------
    // PATCH /orders/{id}/status – invalid transitions
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateOrderStatus_Cancelled_To_Ready_ReturnsBadRequest()
    {
        int menuItemId = await CreateMenuItemAsync();
        var createDto = new CreateOrderDto(menuItemId, "student-inv-cancel");
        var createResponse = await _client.PostAsJsonAsync("/orders", createDto);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);

        // Cancel the order first
        await _client.PatchAsJsonAsync($"/orders/{order.Id}/status", new UpdateOrderStatusDto(2));

        // Attempt invalid transition: Cancelled -> Ready
        var patchResponse = await _client.PatchAsJsonAsync($"/orders/{order.Id}/status", new UpdateOrderStatusDto(1));
        Assert.Equal(HttpStatusCode.BadRequest, patchResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateOrderStatus_Completed_To_Preparing_ReturnsBadRequest()
    {
        int menuItemId = await CreateMenuItemAsync();
        var createDto = new CreateOrderDto(menuItemId, "student-inv-complete");
        var createResponse = await _client.PostAsJsonAsync("/orders", createDto);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);

        // Preparing -> Ready -> Completed
        await _client.PatchAsJsonAsync($"/orders/{order.Id}/status", new UpdateOrderStatusDto(1));
        await _client.PatchAsJsonAsync($"/orders/{order.Id}/status", new UpdateOrderStatusDto(3));

        // Attempt invalid transition: Completed -> Preparing
        var patchResponse = await _client.PatchAsJsonAsync($"/orders/{order.Id}/status", new UpdateOrderStatusDto(0));
        Assert.Equal(HttpStatusCode.BadRequest, patchResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateOrderStatus_InvalidStatusValue_ReturnsBadRequest()
    {
        int menuItemId = await CreateMenuItemAsync();
        var createDto = new CreateOrderDto(menuItemId, "student-inv-status");
        var createResponse = await _client.PostAsJsonAsync("/orders", createDto);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);

        var patchResponse = await _client.PatchAsJsonAsync($"/orders/{order.Id}/status", new UpdateOrderStatusDto(999));
        Assert.Equal(HttpStatusCode.BadRequest, patchResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateOrderStatus_NonExistentOrder_ReturnsNotFound()
    {
        var patchResponse = await _client.PatchAsJsonAsync("/orders/999999/status", new UpdateOrderStatusDto(1));
        Assert.Equal(HttpStatusCode.NotFound, patchResponse.StatusCode);
    }
}