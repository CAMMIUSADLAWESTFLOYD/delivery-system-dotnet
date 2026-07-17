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
            // Estados alcanzables por el repartidor; el resto de las reglas de
            // transición las valida la máquina de estados (patrón State).
            var permitidos = new[] { "En Camino", "Entregado", "Entregada", "Reportado", "Aceptado", "CancelacionSolicitada" };
            if (Array.IndexOf(permitidos, newStatus) < 0)
            {
                throw new UnauthorizedAccessException("El repartidor solo puede cambiar la orden a 'En Camino', 'Entregado', 'Reportado' o solicitar su cancelación.");
            }
        }

        // Delegate to actual RealOrderManager
        return _realOrderManager.ChangeStatus(orderId, userRole, newStatus);
    }
}
