using Microsoft.EntityFrameworkCore;
using RecipeService.Domain;

namespace RecipeService.Data;

public class RecipeDbContext(DbContextOptions<RecipeDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AuthSub).HasColumnName("auth0_sub");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(e => e.AuthSub).IsUnique();
        });

        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.ToTable("ingredients");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Category).HasColumnName("category");
            entity.Property(e => e.DefaultUnit).HasColumnName("default_unit");
        });

        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.ToTable("recipes");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Servings).HasColumnName("servings");
            entity.Property(e => e.PrepTimeMin).HasColumnName("prep_time_min");
            entity.Property(e => e.CookTimeMin).HasColumnName("cook_time_min");
            entity.Property(e => e.ImageUrl).HasColumnName("image_url");
            // Required + a DB-level default, not just the C# `= new()` default on the domain
            // property -- a row inserted by anything other than this EF model (a raw SQL
            // statement, a future migration) must still get a non-null array, or reading it back
            // throws trying to materialize a NULL into the non-nullable List<string> Tips.
            entity.Property(e => e.Tips)
                .HasColumnName("tips")
                .HasColumnType("text[]")
                .IsRequired()
                .HasDefaultValueSql("ARRAY[]::text[]");
            entity.Property(e => e.Pairing).HasColumnName("pairing");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Author)
                .WithMany()
                .HasForeignKey(e => e.AuthorId);

            entity.HasMany(e => e.Steps)
                .WithOne(s => s.Recipe)
                .HasForeignKey(s => s.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Ingredients)
                .WithOne(i => i.Recipe)
                .HasForeignKey(i => i.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecipeStep>(entity =>
        {
            entity.ToTable("recipe_steps");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RecipeId).HasColumnName("recipe_id");
            entity.Property(e => e.StepNumber).HasColumnName("step_number");
            entity.Property(e => e.Instruction).HasColumnName("instruction");
            entity.Property(e => e.TimerSeconds).HasColumnName("timer_seconds");
            entity.Property(e => e.ImageUrl).HasColumnName("image_url");
            entity.HasIndex(e => new { e.RecipeId, e.StepNumber }).IsUnique();
        });

        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.ToTable("recipe_ingredients");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RecipeId).HasColumnName("recipe_id");
            entity.Property(e => e.IngredientId).HasColumnName("ingredient_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity").HasColumnType("numeric(10,2)");
            entity.Property(e => e.Unit).HasColumnName("unit");
            entity.Property(e => e.Optional).HasColumnName("optional");

            // Explicit Restrict, not the EF Core default (Cascade for a required FK): the SQL
            // schema (init-db/01-schema.sql) declares this FK with no ON DELETE clause, which
            // Postgres defaults to RESTRICT/NO ACTION. Without this, EnsureCreatedAsync/migrations
            // would silently generate ON DELETE CASCADE instead, diverging from production and
            // undermining IngredientsService.DeleteAsync's "block, don't cascade" design -- caught
            // by IngredientRepositoryTests expecting a foreign-key-violation, not a silent cascade.
            entity.HasOne(e => e.Ingredient)
                .WithMany()
                .HasForeignKey(e => e.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
