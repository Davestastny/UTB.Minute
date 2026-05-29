using Microsoft.EntityFrameworkCore;
using UTB.Minute.Contracts;
using UTB.Minute.Db;

namespace UTB.Minute.WebApi.Endpoints;

public static class MenuItemEndpoints
{
    public static void MapMenuItemEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/menu-items").WithTags("MenuItems");

        group.MapGet("/", GetAllMenuItems);
        group.MapGet("/{id:int}", GetMenuItemById);
        group.MapGet("/by-date/{date}", GetMenuItemsByDate);
        group.MapPost("/", CreateMenuItem).RequireAuthorization("AdminOrCook");
        group.MapPut("/{id:int}", UpdateMenuItem).RequireAuthorization("AdminOrCook");
        group.MapDelete("/{id:int}", DeleteMenuItem).RequireAuthorization("AdminOrCook");
    }

    private static async Task<IResult> GetAllMenuItems(MinuteDbContext db)
    {
        var items = await db.MenuItems
            .Include(m => m.Dish)
            .Select(m => new MenuItemDto(
                m.Id,
                m.DishId,
                m.Dish!.Name,
                m.Dish!.Price,
                m.MenuDate,
                m.AvailablePortions))
            .ToListAsync();

        return TypedResults.Ok(items);
    }

    private static async Task<IResult> GetMenuItemById(int id, MinuteDbContext db)
    {
        var item = await db.MenuItems
            .Include(m => m.Dish)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (item is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(new MenuItemDto(
            item.Id,
            item.DishId,
            item.Dish!.Name,
            item.Dish!.Price,
            item.MenuDate,
            item.AvailablePortions));
    }

    private static async Task<IResult> GetMenuItemsByDate(DateTime date, MinuteDbContext db)
    {
        var startDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var endDate = startDate.AddDays(1);

        var items = await db.MenuItems
            .Include(m => m.Dish)
            .Where(m => m.MenuDate >= startDate && m.MenuDate < endDate)
            .Select(m => new MenuItemDto(
                m.Id,
                m.DishId,
                m.Dish!.Name,
                m.Dish!.Price,
                m.MenuDate,
                m.AvailablePortions))
            .ToListAsync();

        return TypedResults.Ok(items);
    }

    private static async Task<IResult> CreateMenuItem(CreateMenuItemDto dto, MinuteDbContext db)
    {
        var dishExists = await db.Dishes.AnyAsync(d => d.Id == dto.DishId && d.IsActive);
        if (!dishExists)
            return TypedResults.BadRequest("Dish not found or inactive.");

        var item = new MenuItem
        {
            DishId = dto.DishId,
            MenuDate = dto.MenuDate.ToUniversalTime(),
            AvailablePortions = dto.AvailablePortions,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.MenuItems.Add(item);
        await db.SaveChangesAsync();

        await db.Entry(item).Reference(m => m.Dish).LoadAsync();

        var result = new MenuItemDto(
            item.Id,
            item.DishId,
            item.Dish!.Name,
            item.Dish!.Price,
            item.MenuDate,
            item.AvailablePortions);

        return TypedResults.Created($"/menu-items/{item.Id}", result);
    }

    private static async Task<IResult> UpdateMenuItem(int id, UpdateMenuItemDto dto, MinuteDbContext db)
    {
        var item = await db.MenuItems.Include(m => m.Dish).FirstOrDefaultAsync(m => m.Id == id);

        if (item is null)
            return TypedResults.NotFound();

        var dishExists = await db.Dishes.AnyAsync(d => d.Id == dto.DishId && d.IsActive);
        if (!dishExists)
            return TypedResults.BadRequest("Dish not found or inactive.");

        item.DishId = dto.DishId;
        item.MenuDate = dto.MenuDate.ToUniversalTime();
        item.AvailablePortions = dto.AvailablePortions;
        item.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        await db.Entry(item).Reference(m => m.Dish).LoadAsync();

        return TypedResults.Ok(new MenuItemDto(
            item.Id,
            item.DishId,
            item.Dish!.Name,
            item.Dish!.Price,
            item.MenuDate,
            item.AvailablePortions));
    }

    private static async Task<IResult> DeleteMenuItem(int id, MinuteDbContext db)
    {
        var item = await db.MenuItems.FindAsync(id);

        if (item is null)
            return TypedResults.NotFound();

        db.MenuItems.Remove(item);
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }
}