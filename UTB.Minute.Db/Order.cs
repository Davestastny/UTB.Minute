namespace UTB.Minute.Db;

public class Order
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.Preparing;
    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public MenuItem? MenuItem { get; set; }
}