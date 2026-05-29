using Microsoft.EntityFrameworkCore;
using UTB.Minute.Db;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<MinuteDbContext>("minutedb");

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapPost("/dev/seed", async (MinuteDbContext db) =>
{
    await db.Database.EnsureDeletedAsync();
    await db.Database.EnsureCreatedAsync();

    await SeedData(db);

    return TypedResults.Ok("Database reset and seeded successfully.");
});

// Automatically ensure database is created and seeded on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MinuteDbContext>();
    await db.Database.EnsureCreatedAsync();
    if (!await db.Dishes.AnyAsync())
    {
        await SeedData(db);
    }
}

app.Run();

static async Task SeedData(MinuteDbContext db)
{
    var dishes = new List<Dish>
    {
        new() { Name = "Beef Sirloin in Cream Sauce", Description = "Beef sirloin with bread dumplings and cream sauce", Price = 89m, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new() { Name = "Fried Pork Schnitzel", Description = "Breaded pork schnitzel with potato salad", Price = 75m, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new() { Name = "Grilled Chicken Steak", Description = "Grilled chicken breast with seasonal vegetables", Price = 79m, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new() { Name = "Pasta Bolognese", Description = "Pasta with minced meat sauce", Price = 65m, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new() { Name = "Vegetarian Selection", Description = "Baked vegetables with rice", Price = 59m, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
    };

    db.Dishes.AddRange(dishes);
    await db.SaveChangesAsync();

    var today = DateTime.UtcNow.Date;

    var menuItems = new List<MenuItem>
    {
        new() { DishId = dishes[0].Id, MenuDate = today, AvailablePortions = 20, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new() { DishId = dishes[1].Id, MenuDate = today, AvailablePortions = 15, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new() { DishId = dishes[2].Id, MenuDate = today, AvailablePortions = 0,  CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new() { DishId = dishes[3].Id, MenuDate = today.AddDays(1), AvailablePortions = 25, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
    };

    db.MenuItems.AddRange(menuItems);
    await db.SaveChangesAsync();

    var orders = new List<Order>
    {
        new() { MenuItemId = menuItems[0].Id, StudentId = "student-001", Status = OrderStatus.Preparing, OrderedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new() { MenuItemId = menuItems[1].Id, StudentId = "student-002", Status = OrderStatus.Ready,     OrderedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new() { MenuItemId = menuItems[0].Id, StudentId = "student-003", Status = OrderStatus.Completed, OrderedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
    };

    db.Orders.AddRange(orders);
    await db.SaveChangesAsync();
}