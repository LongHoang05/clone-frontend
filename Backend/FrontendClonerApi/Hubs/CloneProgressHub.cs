using Microsoft.AspNetCore.SignalR;

namespace FrontendClonerApi.Hubs;

public class CloneProgressHub : Hub
{
    // The client will listen to "ReceiveLog" events.
    // Clients can also invoke methods here if needed, but for now we only push from server.
}
