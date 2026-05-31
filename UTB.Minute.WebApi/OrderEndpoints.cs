using Microsoft.EntityFrameworkCore;
using UTB.Minute.Contracts;
using UTB.Minute.Db;
using UTB.Minute.WebApi;

namespace UTB.Minute.WebApi.Endpoints;

public static class OrderEndpoints
{
    // Valid order status transitions
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.Preparing] = [OrderStatus.Ready, OrderStatus.Cancelled],
        [OrderStatus.Ready] = [OrderStatus.Completed],
        [OrderStatus.Cancelled] = [],
        [OrderStatus.Completed] = []
    };

    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/orders").WithTags("Orders");

        group.MapGet("/", GetActiveOrders).RequireAuthorization("AdminOrCook");
        group.MapGet("/student/{studentId}", GetActiveOrdersByStudent);             // public — student gets their active orders
        group.MapGet("/{id:int}", GetOrderById);                                    // public — student tracks their order
        group.MapPost("/", CreateOrder);                                             // public — student does not log in
        group.MapPatch("/{id:int}/status", UpdateOrderStatus).RequireAuthorization("AdminOrCook");
    }

    private static async Task<IResult> GetActiveOrders(MinuteDbContext db)
    {
        var orders = await db.Orders
            .Where(o => o.Status != OrderStatus.Completed)
            .Select(o => new OrderDto(
                o.Id,
                o.MenuItemId,
                o.StudentId,
                (int)o.Status,
                o.OrderedAt))
            .ToListAsync();

        return TypedResults.Ok(orders);
    }

    private static async Task<IResult> GetActiveOrdersByStudent(string studentId, MinuteDbContext db)
    {
        var orders = await db.Orders
            .Where(o => o.StudentId == studentId && o.Status != OrderStatus.Completed)
            .Select(o => new OrderDto(
                o.Id,
                o.MenuItemId,
                o.StudentId,
                (int)o.Status,
                o.OrderedAt))
            .ToListAsync();

        return TypedResults.Ok(orders);
    }

    private static async Task<IResult> GetOrderById(int id, MinuteDbContext db)
    {
        var order = await db.Orders.FindAsync(id);

        if (order is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(new OrderDto(
            order.Id,
            order.MenuItemId,
            order.StudentId,
            (int)order.Status,
            order.OrderedAt));
    }

    private static async Task<IResult> CreateOrder(CreateOrderDto dto, MinuteDbContext db, SseHub sse)
    {
        // Retry loop for optimistic concurrency on AvailablePortions
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var menuItem = await db.MenuItems
                .Include(m => m.Dish)
                .FirstOrDefaultAsync(m => m.Id == dto.MenuItemId);

            if (menuItem is null)
                return TypedResults.BadRequest("Menu item not found.");

            if (menuItem.AvailablePortions <= 0)
                return TypedResults.BadRequest("No portions available.");

            menuItem.AvailablePortions--;
            menuItem.UpdatedAt = DateTime.UtcNow;

            var order = new Order
            {
                MenuItemId = dto.MenuItemId,
                StudentId = dto.StudentId,
                Status = OrderStatus.Preparing,
                OrderedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Orders.Add(order);

            try
            {
                await db.SaveChangesAsync();

                var result = new OrderDto(
                    order.Id,
                    order.MenuItemId,
                    order.StudentId,
                    (int)order.Status,
                    order.OrderedAt);

                sse.Broadcast("order-created", result);

                return TypedResults.Created($"/orders/{order.Id}", result);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another request modified the same MenuItem concurrently – retry
                db.ChangeTracker.Clear();
            }
        }

        return TypedResults.BadRequest("Could not place order due to concurrent modifications. Please try again.");
    }

    private static async Task<IResult> UpdateOrderStatus(int id, UpdateOrderStatusDto dto, MinuteDbContext db, SseHub sse)
    {
        var order = await db.Orders.FindAsync(id);

        if (order is null)
            return TypedResults.NotFound();

        if (!Enum.IsDefined(typeof(OrderStatus), dto.Status))
            return TypedResults.BadRequest("Invalid status value.");

        var newStatus = (OrderStatus)dto.Status;

        if (!AllowedTransitions[order.Status].Contains(newStatus))
            return TypedResults.BadRequest(
                $"Cannot transition from {order.Status} to {newStatus}.");

        order.Status = newStatus;
        order.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var result = new OrderDto(
            order.Id,
            order.MenuItemId,
            order.StudentId,
            (int)order.Status,
            order.OrderedAt);

        sse.Broadcast("order-updated", result);

        return TypedResults.Ok(result);
    }
}