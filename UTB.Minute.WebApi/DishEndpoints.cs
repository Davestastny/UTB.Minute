using Microsoft.EntityFrameworkCore;
using UTB.Minute.Contracts;
using UTB.Minute.Db;

namespace UTB.Minute.WebApi.Endpoints;

public static class DishEndpoints
{
    public static void MapDishEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/dishes").WithTags("Dishes");

        group.MapGet("/", GetAllDishes);
        group.MapGet("/{id:int}", GetDishById);
        group.MapPost("/", CreateDish).RequireAuthorization("AdminOrCook");
        group.MapPut("/{id:int}", UpdateDish).RequireAuthorization("AdminOrCook");
        group.MapPatch("/{id:int}/deactivate", DeactivateDish).RequireAuthorization("AdminOrCook");
    }

    private static async Task<IResult> GetAllDishes(MinuteDbContext db)
    {
        var dishes = await db.Dishes
            .Select(d => new DishDto(d.Id, d.Name, d.Description, d.Price, d.IsActive))
            .ToListAsync();

        return TypedResults.Ok(dishes);
    }

    private static async Task<IResult> GetDishById(int id, MinuteDbContext db)
    {
        var dish = await db.Dishes.FindAsync(id);

        if (dish is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(new DishDto(dish.Id, dish.Name, dish.Description, dish.Price, dish.IsActive));
    }

    private static async Task<IResult> CreateDish(CreateDishDto dto, MinuteDbContext db)
    {
        var dish = new Dish
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Dishes.Add(dish);
        await db.SaveChangesAsync();

        var result = new DishDto(dish.Id, dish.Name, dish.Description, dish.Price, dish.IsActive);
        return TypedResults.Created($"/dishes/{dish.Id}", result);
    }

    private static async Task<IResult> UpdateDish(int id, UpdateDishDto dto, MinuteDbContext db)
    {
        var dish = await db.Dishes.FindAsync(id);

        if (dish is null)
            return TypedResults.NotFound();

        dish.Name = dto.Name;
        dish.Description = dto.Description;
        dish.Price = dto.Price;
        dish.IsActive = dto.IsActive;
        dish.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return TypedResults.Ok(new DishDto(dish.Id, dish.Name, dish.Description, dish.Price, dish.IsActive));
    }

    private static async Task<IResult> DeactivateDish(int id, MinuteDbContext db)
    {
        var dish = await db.Dishes.FindAsync(id);

        if (dish is null)
            return TypedResults.NotFound();

        dish.IsActive = false;
        dish.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return TypedResults.Ok(new DishDto(dish.Id, dish.Name, dish.Description, dish.Price, dish.IsActive));
    }
}