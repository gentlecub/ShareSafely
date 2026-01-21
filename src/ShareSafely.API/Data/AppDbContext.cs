using Microsoft.EntityFrameworkCore;
using ShareSafely.API.Models.Entities;

namespace ShareSafely.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Archivo> Archivos => Set<Archivo>();
    public DbSet<Enlace> Enlaces => Set<Enlace>();
    public DbSet<LogAcceso> LogsAcceso => Set<LogAcceso>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración de Archivo
        modelBuilder.Entity<Archivo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).HasMaxLength(255).IsRequired();
            entity.Property(e => e.NombreOriginal).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.BlobUrl).HasMaxLength(500);
            entity.HasIndex(e => e.Estado);
            entity.HasIndex(e => e.FechaExpiracion);
        });

        // Configuración de Enlace
        modelBuilder.Entity<Enlace>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).HasMaxLength(100).IsRequired();
            entity.Property(e => e.UrlCompleta).HasMaxLength(1000);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.Estado);

            entity.HasOne(e => e.Archivo)
                  .WithMany(a => a.Enlaces)
                  .HasForeignKey(e => e.ArchivoId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de LogAcceso
        modelBuilder.Entity<LogAcceso>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.HasIndex(e => e.Timestamp);

            entity.HasOne(e => e.Archivo)
                  .WithMany(a => a.Logs)
                  .HasForeignKey(e => e.ArchivoId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
