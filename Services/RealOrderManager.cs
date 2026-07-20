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
    private readonly INotificationService _notificaciones;
    private readonly IRiderAssignmentService _riderAssignment;

    public RealOrderManager(ApplicationDbContext context, IDeliveryTrackingService tracking,
        INotificationService notificaciones, IRiderAssignmentService riderAssignment)
    {
        _context = context;
        _tracking = tracking;
        _notificaciones = notificaciones;
        _riderAssignment = riderAssignment;
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
        if (order.Status == newStatus)
        {
            return 1; // Ya tiene el estado, retornamos éxito como no-op para evitar errores molestos en la UI.
        }
        OrderState.ValidateTransition(order.Status, newStatus, userRole);

        order.Status = newStatus;

        // Cobro contra entrega: al confirmar la entrega, el pago queda
        // registrado (los pagos con tarjeta/transferencia ya venían en true).
        if (newStatus == "Entregado" || newStatus == "Entregada")
        {
            order.PaymentStatus = true;
        }

        _context.SaveChanges();

        // Notifica al servicio de seguimiento en tiempo real: inicia/detiene el
        // recorrido del repartidor y difunde el nuevo estado por SignalR.
        _tracking.OnOrderStatusChanged(orderId, newStatus);

        // Notificación (toast) al cliente en los hitos clave de su pedido.
        NotificarCliente(order, newStatus);

        // Al entregar o cancelar, el repartidor queda libre: intenta tomar un
        // pedido que estuviera esperando repartidor.
        if (newStatus is "Entregado" or "Entregada" or "Cancelado")
        {
            _riderAssignment.AsignarPendientes();
        }

        return 1; // Success
    }

    private void NotificarCliente(delivery_system_dotnet.Models.Order order, string newStatus)
    {
        if (string.IsNullOrEmpty(order.ClientDni)) return;

        var (titulo, mensaje) = newStatus switch
        {
            "Aceptado" => ("Pedido aceptado", $"Un repartidor va camino al restaurante por tu pedido {order.Id}."),
            "En Camino" => ("¡Tu pedido va en camino!", $"El repartidor salió con tu pedido {order.Id}."),
            "Entregado" => ("Pedido entregado", $"Tu pedido {order.Id} fue entregado. ¡Buen provecho!"),
            "Cancelado" => ("Pedido cancelado", $"Tu pedido {order.Id} fue cancelado."),
            _ => ("", "")
        };
        if (titulo.Length > 0)
        {
            _ = _notificaciones.NotificarUsuarioAsync(order.ClientDni, titulo, mensaje, "/Cliente/HistorialPedidos");
        }
    }
}
