namespace UTB.Minute.Contracts;

public record MenuItemDto(
    int Id,
    int DishId,
    string DishName,
    decimal DishPrice,
    DateTime MenuDate,
    int AvailablePortions
);

public record CreateMenuItemDto(
    int DishId,
    DateTime MenuDate,
    int AvailablePortions
);

public record UpdateMenuItemDto(
    int DishId,
    DateTime MenuDate,
    int AvailablePortions
);