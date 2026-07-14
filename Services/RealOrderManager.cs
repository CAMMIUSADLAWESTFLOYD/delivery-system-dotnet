using System.Linq;
using delivery_system_dotnet.Data;
using delivery_system_dotnet.Interfaces;

namespace delivery_system_dotnet.Services;

public class RealOrderManager : IOrderManager
{
    private readonly ApplicationDbContext _context;
    private readonly IDeliveryTrackingService _tracking;

    public RealOrderManager(ApplicationDbContext context, IDeliveryTrackingService tracking)
    {
        _context = context;
        _tracking = tracking;
    }

    public int ChangeStatus(string orderId, string userRole, string newStatus)
    {
        var order = _context.Orders.FirstOrDefault(o => o.Id == orderId);
        if (order == null)
        {
            return 0; // Order not found
        }

        order.Status = newStatus;
        _context.SaveChanges();

        // Notifica al servicio de seguimiento en tiempo real: inicia/detiene el
        // recorrido del repartidor y difunde el nuevo estado por SignalR.
        _tracking.OnOrderStatusChanged(orderId, newStatus);

        return 1; // Success
    }
}
