using SiteWeb.Api.Data;
using SiteWeb.Api.Entities;

namespace SiteWeb.Api.GraphQL;

public class ExerciseQuery
{
    public IQueryable<Booking> GetBookings([Service] AppDbContext db) => db.Bookings.AsQueryable();

    public IQueryable<CatalogItem> GetCatalog([Service] AppDbContext db) => db.CatalogItems.AsQueryable();
}
