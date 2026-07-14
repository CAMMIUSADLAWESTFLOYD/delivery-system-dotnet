using Microsoft.EntityFrameworkCore;
using delivery_system_dotnet.Models;

namespace delivery_system_dotnet.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Person> Persons { get; set; } = null!;
    public DbSet<Client> Clients { get; set; } = null!;
    public DbSet<Admin> Admins { get; set; } = null!;
    public DbSet<Delivery> Deliveries { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderDetails> OrderDetails { get; set; } = null!;
    public DbSet<Products> Products { get; set; } = null!;
    public DbSet<Restaurant> Restaurants { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<CodigoVerificacion> CodigosVerificacion { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. TPH (Table-Per-Hierarchy) mapping for Person subclasses using shadow Discriminator property
        modelBuilder.Entity<Person>()
            .HasKey(p => p.Dni);

        modelBuilder.Entity<Person>()
            .HasDiscriminator<string>("Discriminator")
            .HasValue<Client>("Client")
            .HasValue<Admin>("Admin")
            .HasValue<Delivery>("Delivery");

        modelBuilder.Entity<Person>()
            .Property<bool>("Activo")
            .HasDefaultValue(true);

        // Unique constraint on Mail
        modelBuilder.Entity<Person>()
            .HasIndex(p => p.Mail)
            .IsUnique();

        // TPH: Client y Delivery comparten las MISMAS columnas Latitude/Longitude
        // (ubicación actual). Evita que EF genere columnas duplicadas (Client_Latitude)
        // y preserva los datos ya guardados en la columna 'Latitude'/'Longitude'.
        modelBuilder.Entity<Client>().Property(c => c.Latitude).HasColumnName("Latitude");
        modelBuilder.Entity<Client>().Property(c => c.Longitude).HasColumnName("Longitude");
        modelBuilder.Entity<Delivery>().Property(d => d.Latitude).HasColumnName("Latitude");
        modelBuilder.Entity<Delivery>().Property(d => d.Longitude).HasColumnName("Longitude");

        // 2. Order relationships
        // Order 0..* --- 0..1 Client
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Client)
            .WithMany()
            .HasForeignKey(o => o.ClientDni)
            .OnDelete(DeleteBehavior.Restrict);

        // Order 0..* --- 0..1 Delivery
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Delivery)
            .WithMany()
            .HasForeignKey(o => o.DeliveryDni)
            .OnDelete(DeleteBehavior.Restrict);

        // Order 0..* --- 0..1 Restaurant
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Restaurant)
            .WithMany()
            .HasForeignKey(o => o.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Order 0..1 --- 0..* OrderDetails
        modelBuilder.Entity<Order>()
            .HasMany(o => o.OrderDetails)
            .WithOne(od => od.Order)
            .HasForeignKey(od => od.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Order 0..1 --- 0..1 Payment
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Payment)
            .WithOne(p => p.Order)
            .HasForeignKey<Payment>(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // 3. OrderDetails 0..1 --- 0..* Products
        modelBuilder.Entity<OrderDetails>()
            .HasMany(od => od.Products)
            .WithOne(p => p.OrderDetails)
            .HasForeignKey(p => p.OrderDetailsId)
            .OnDelete(DeleteBehavior.SetNull);

        // 3b. Restaurant 1 --- 0..* Products, relacionados EXCLUSIVAMENTE por
        // RestaurantId (clave foránea numérica; nunca por nombre).
        modelBuilder.Entity<Restaurant>()
            .HasMany(r => r.Products)
            .WithOne(p => p.Restaurant)
            .HasForeignKey(p => p.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        // 4. CodigoVerificacion 0..* --- 1 Person
        modelBuilder.Entity<CodigoVerificacion>()
            .HasOne(cv => cv.Usuario)
            .WithMany()
            .HasForeignKey(cv => cv.UsuarioDni)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
