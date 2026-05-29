namespace UTB.Minute.Contracts;

public record OrderDto(
    int Id,
    int MenuItemId,
    string StudentId,
    int Status,
    DateTime OrderedAt
);

public record CreateOrderDto(
    int MenuItemId,
    string StudentId
);

public record UpdateOrderStatusDto(
    int Status
);