namespace UTB.Minute.Db;

using Microsoft.EntityFrameworkCore;

public class MinuteDbContext : DbContext
{
    public MinuteDbContext(DbContextOptions<MinuteDbContext> options) : base(options)
    {
    }

    public DbSet<Dish> Dishes { get; set; } = null!;
    public DbSet<MenuItem> MenuItems { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Dish configuration
        modelBuilder.Entity<Dish>()
            .HasKey(d => d.Id);

        // MenuItem configuration
        modelBuilder.Entity<MenuItem>()
            .HasKey(m => m.Id);

        modelBuilder.Entity<MenuItem>()
            .HasOne(m => m.Dish)
            .WithMany(d => d.MenuItems)
            .HasForeignKey(m => m.DishId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MenuItem>()
            .HasIndex(m => new { m.DishId, m.MenuDate })
            .IsUnique();

        // Use PostgreSQL xmin system column as optimistic concurrency token
        modelBuilder.Entity<MenuItem>()
            .Property(m => m.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        // Order configuration
        modelBuilder.Entity<Order>()
            .HasKey(o => o.Id);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.MenuItem)
            .WithMany()
            .HasForeignKey(o => o.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Order>()
            .Property(o => o.Status)
            .HasConversion<int>();
    }
}