using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using delivery_system_dotnet.Hubs;

namespace delivery_system_dotnet.Services;

public interface INotificationService
{
    // Notificación personal (toast) a un usuario por su DNI.
    Task NotificarUsuarioAsync(string dni, string titulo, string mensaje, string? url = null);

    // Notificación a todos los usuarios conectados de un rol (Admin, Delivery, Client).
    Task NotificarRolAsync(string rol, string titulo, string mensaje, string? url = null);
}

// Notificaciones en tiempo real sobre el MISMO hub del tracking (SignalR).
// El layout abre la conexión y se une a "user-{dni}" y "role-{rol}" mediante
// TrackingHub.JoinNotifications (identidad tomada de la cookie, no del cliente).
public class NotificationService : INotificationService
{
    private readonly IHubContext<TrackingHub> _hub;

    public NotificationService(IHubContext<TrackingHub> hub)
    {
        _hub = hub;
    }

    public Task NotificarUsuarioAsync(string dni, string titulo, string mensaje, string? url = null)
        => _hub.Clients.Group("user-" + dni).SendAsync("Notify", titulo, mensaje, url ?? "");

    public Task NotificarRolAsync(string rol, string titulo, string mensaje, string? url = null)
        => _hub.Clients.Group("role-" + rol).SendAsync("Notify", titulo, mensaje, url ?? "");
}
