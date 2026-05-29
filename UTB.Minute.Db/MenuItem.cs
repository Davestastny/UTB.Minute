namespace UTB.Minute.Db;

public class MenuItem
{
    public int Id { get; set; }
    public int DishId { get; set; }
    public DateTime MenuDate { get; set; }
    public int AvailablePortions { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // PostgreSQL xmin used as optimistic concurrency token
    public uint Version { get; set; }

    public Dish? Dish { get; set; }
}