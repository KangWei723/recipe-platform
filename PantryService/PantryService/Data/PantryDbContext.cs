using Microsoft.EntityFrameworkCore;
using PantryService.Domain;

namespace PantryService.Data;

public class PantryDbContext(DbContextOptions<PantryDbContext> options) : DbContext(options)
{
    public DbSet<PantryItem> PantryItems => Set<PantryItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PantryItem>(entity =>
        {
            entity.ToTable("pantry_items");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.IngredientId).HasColumnName("ingredient_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity").HasColumnType("numeric(10,2)");
            entity.Property(e => e.Unit).HasColumnName("unit");
            entity.Property(e => e.ExpiryDate).HasColumnName("expiry_date");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => new { e.UserId, e.IngredientId }).IsUnique();
        });
    }
}
