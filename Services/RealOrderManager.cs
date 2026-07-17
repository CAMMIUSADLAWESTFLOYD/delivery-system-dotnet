using System;
using System.Linq;
using delivery_system_dotnet.Data;
using delivery_system_dotnet.Interfaces;
using delivery_system_dotnet.Models.OrderStates;

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
            // Antes se devolvía 0 y los controladores lo ignoraban, mostrando
            // "actualizado con éxito" para pedidos inexistentes. La excepción
            // la capturan todos los llamadores y la muestran como error.
            throw new KeyNotFoundException($"El pedido '{orderId}' no existe.");
        }

        // PATRÓN STATE: la máquina de estados (Models/OrderStates) valida la
        // transición según el estado actual y el rol. Una transición inválida
        // lanza InvalidOperationException y el pedido no se modifica.
        OrderState.ValidateTransition(order.Status, newStatus, userRole);

        order.Status = newStatus;
        _context.SaveChanges();

        // Notifica al servicio de seguimiento en tiempo real: inicia/detiene el
        // recorrido del repartidor y difunde el nuevo estado por SignalR.
        _tracking.OnOrderStatusChanged(orderId, newStatus);

        return 1; // Success
    }
}
