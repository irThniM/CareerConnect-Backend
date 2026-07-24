using CareerConnect.Api.Entities;
using Microsoft.EntityFrameworkCore;
namespace CareerConnect.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.Email)
            .IsUnique();

            entity.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(255);

            entity.Property(x => x.PasswordHash)
            .IsRequired();

            entity.Property(x => x.Role)
            .IsRequired()
            .HasMaxLength(30);

            entity.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(30);
        });
    }
}