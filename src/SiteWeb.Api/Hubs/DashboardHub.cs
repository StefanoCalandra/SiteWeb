using Microsoft.AspNetCore.SignalR;

namespace SiteWeb.Api.Hubs;

public class DashboardHub : Hub
{
    public async Task BroadcastKpi(string name, decimal value)
    {
        await Clients.All.SendAsync("kpi", new { name, value });
    }
}
