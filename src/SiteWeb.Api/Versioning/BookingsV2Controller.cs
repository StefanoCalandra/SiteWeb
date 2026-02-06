using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteWeb.Api.CQRS;
using SiteWeb.Api.Entities;

namespace SiteWeb.Api.Versioning;

[ApiController]
[ApiVersion(2.0)]
[Route("api/v{version:apiVersion}/bookings")]
public class BookingsV2Controller : ControllerBase
{
    private readonly IMediator _mediator;

    public BookingsV2Controller(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Booking>>> GetBookings()
    {
        var items = await _mediator.Send(new GetBookingsQuery());
        return Ok(items.Select(b => new
        {
            b.Id,
            b.ResourceName,
            b.Start,
            b.End,
            b.CreatedBy,
            DurationMinutes = (b.End - b.Start).TotalMinutes
        }));
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Booking>> CreateBooking(CreateBookingCommand command)
    {
        var booking = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetBookings), new { id = booking.Id }, booking);
    }
}
