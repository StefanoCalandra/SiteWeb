using Microsoft.EntityFrameworkCore;
using SiteWeb.Api.Entities;

namespace SiteWeb.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
}
