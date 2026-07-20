using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using delivery_system_dotnet.Services;

namespace delivery_system_dotnet.Hubs;

// Hub de SignalR para el seguimiento de pedidos en tiempo real.
// El hub es intencionalmente delgado: la lógica vive en IDeliveryTrackingService
// (capa de Services), respetando la arquitectura MVC.
[Authorize]
public class TrackingHub : Hub
{
    private readonly IDeliveryTrackingService _tracking;

    public TrackingHub(IDeliveryTrackingService tracking)
    {
        _tracking = tracking;
    }

    // El cliente se suscribe al canal de su pedido. Si ya hay un recorrido en
    // curso, recibe inmediatamente la ruta y la posición actual (para que un
    // observador que llega tarde vea el estado real sin esperar el próximo tick).
    public async Task JoinOrderTracking(string orderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "order-" + orderId);

        var snapshot = _tracking.GetSnapshot(orderId);
        if (snapshot != null)
        {
            await Clients.Caller.SendAsync("ReceiveRoute", snapshot.Phase, snapshot.Route);
            await Clients.Caller.SendAsync("ReceivePosition", snapshot.Lat, snapshot.Lng, snapshot.Phase, snapshot.Progress);
        }
    }

    // Canal de NOTIFICACIONES personales: la identidad sale de la cookie de
    // sesión (Context.User), nunca de parámetros del cliente — nadie puede
    // suscribirse al canal de otro usuario.
    public async Task JoinNotifications()
    {
        var dni = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var rol = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        if (!string.IsNullOrEmpty(dni))
            await Groups.AddToGroupAsync(Context.ConnectionId, "user-" + dni);
        if (!string.IsNullOrEmpty(rol))
            await Groups.AddToGroupAsync(Context.ConnectionId, "role-" + rol);
    }
}
