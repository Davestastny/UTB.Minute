namespace UTB.Minute.Contracts;

public record DishDto(
    int Id,
    string Name,
    string Description,
    decimal Price,
    bool IsActive
);

public record CreateDishDto(
    string Name,
    string Description,
    decimal Price
);

public record UpdateDishDto(
    string Name,
    string Description,
    decimal Price,
    bool IsActive
);