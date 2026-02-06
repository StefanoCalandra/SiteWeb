using MediatR;
using Microsoft.EntityFrameworkCore;
using SiteWeb.Api.Data;
using SiteWeb.Api.Entities;
using SiteWeb.Api.Tenancy;

namespace SiteWeb.Api.CQRS;

public record GetBookingsQuery : IRequest<IReadOnlyList<Booking>>;

public class GetBookingsQueryHandler : IRequestHandler<GetBookingsQuery, IReadOnlyList<Booking>>
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenantContext;

    public GetBookingsQueryHandler(AppDbContext db, TenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<Booking>> Handle(GetBookingsQuery request, CancellationToken cancellationToken)
    {
        return await _db.Bookings
            .Where(b => b.TenantId == _tenantContext.TenantId)
            .OrderBy(b => b.Start)
            .ToListAsync(cancellationToken);
    }
}
