using MediatR;
using SiteWeb.Api.Data;
using SiteWeb.Api.Entities;
using SiteWeb.Api.Tenancy;

namespace SiteWeb.Api.CQRS;

public record CreateBookingCommand(string ResourceName, DateTimeOffset Start, DateTimeOffset End, string CreatedBy) : IRequest<Booking>;

public class CreateBookingCommandHandler : IRequestHandler<CreateBookingCommand, Booking>
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenantContext;

    public CreateBookingCommandHandler(AppDbContext db, TenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Booking> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
    {
        // Crea una prenotazione nel tenant corrente.
        var booking = new Booking
        {
            ResourceName = request.ResourceName,
            Start = request.Start,
            End = request.End,
            CreatedBy = request.CreatedBy,
            TenantId = _tenantContext.TenantId
        };

        // Crea il messaggio outbox nello stesso SaveChanges (consistenza).
        var outbox = new OutboxMessage
        {
            Type = "BookingCreated",
            Payload = $"{booking.Id}|{booking.ResourceName}|{booking.Start:o}|{booking.End:o}",
            Status = OutboxStatus.Pending
        };

        _db.Bookings.Add(booking);
        _db.OutboxMessages.Add(outbox);

        await _db.SaveChangesAsync(cancellationToken);
        return booking;
    }
}
