using System;
using delivery_system_dotnet.Interfaces;

namespace delivery_system_dotnet.Services;

public class ProxyOrderManager : IOrderManager
{
    private readonly RealOrderManager _realOrderManager;

    public ProxyOrderManager(RealOrderManager realOrderManager)
    {
        _realOrderManager = realOrderManager;
    }

    public int ChangeStatus(string orderId, string userRole, string newStatus)
    {
        // Proxy access control validation
        if (userRole != "Admin" && userRole != "Delivery")
        {
            throw new UnauthorizedAccessException("El rol del usuario no tiene permisos para cambiar el estado de la orden.");
        }

        // Delivery actor has restricted states
        if (userRole == "Delivery")
        {
            // Allowed states for Delivery: "En Camino", "Entregado", "Reportado"
            if (newStatus != "En Camino" && newStatus != "Entregado" && newStatus != "Reportado" && newStatus != "Entregada" && newStatus != "Aceptado")
            {
                throw new UnauthorizedAccessException("El repartidor solo puede cambiar el estado de la orden a 'En Camino', 'Entregado' o 'Reportado'.");
            }
        }

        // Delegate to actual RealOrderManager
        return _realOrderManager.ChangeStatus(orderId, userRole, newStatus);
    }
}
