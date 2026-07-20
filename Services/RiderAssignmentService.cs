using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using delivery_system_dotnet.Data;
using delivery_system_dotnet.Models;

namespace delivery_system_dotnet.Services;

public interface IRiderAssignmentService
{
    // Asigna al pedido el repartidor DISPONIBLE más cercano al restaurante.
    // Devuelve el repartidor asignado o null si nadie está libre.
    Delivery? AsignarMasCercano(Order order);

    // Cuando un repartidor queda libre (termina una entrega o se pone
    // disponible), toma el pedido "Pendiente" más antiguo y se lo asigna.
    void AsignarPendientes(string? excluirDni = null);
}

// ============================================================================
//  ASIGNACIÓN AUTOMÁTICA (mundo real): el cliente ya no elige repartidor;
//  el sistema selecciona al repartidor disponible, aprobado y SIN entrega
//  activa que esté más cerca del restaurante (distancia Haversine sobre su
//  última posición conocida).
// ============================================================================
public class RiderAssignmentService : IRiderAssignmentService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificaciones;

    // Estados que mantienen ocupado a un repartidor.
    private static readonly string[] EstadosActivos =
        { "PendienteRepartidor", "Aceptado", "Preparando", "En Camino", "CancelacionSolicitada", "Reportado" };

    public RiderAssignmentService(ApplicationDbContext context, INotificationService notificaciones)
    {
        _context = context;
        _notificaciones = notificaciones;
    }

    private static double Haversine(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLng = (lng2 - lng1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                 * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }

    private Delivery? BuscarDisponibleMasCercano(double refLat, double refLng, string? excluirDni)
    {
        var ocupados = _context.Orders
            .Where(o => o.DeliveryDni != null && EstadosActivos.Contains(o.Status))
            .Select(o => o.DeliveryDni!)
            .Distinct()
            .ToList();

        var candidatos = _context.Deliveries
            .Where(d => d.Status)                                    // disponible
            .Where(d => EF.Property<bool>(d, "Activo"))              // aprobado por el admin
            .Where(d => !ocupados.Contains(d.Dni))                   // sin entrega activa
            .Where(d => excluirDni == null || d.Dni != excluirDni)   // p. ej. quien rechazó
            .ToList();

        return candidatos
            .OrderBy(d => Haversine(d.Latitude ?? refLat + 0.01, d.Longitude ?? refLng + 0.01, refLat, refLng))
            .FirstOrDefault();
    }

    public Delivery? AsignarMasCercano(Order order)
    {
        var rest = order.Restaurant ?? _context.Restaurants.Find(order.RestaurantId);
        double refLat = rest?.Latitude ?? 0.3517;
        double refLng = rest?.Longitude ?? -78.1223;

        var repartidor = BuscarDisponibleMasCercano(refLat, refLng, null);
        if (repartidor == null) return null;

        order.DeliveryDni = repartidor.Dni;
        order.Status = "PendienteRepartidor";
        _context.SaveChanges();

        _ = _notificaciones.NotificarUsuarioAsync(repartidor.Dni,
            "Nuevo pedido asignado",
            $"Pedido {order.Id} de {rest?.Name ?? "un restaurante"}. Acéptalo en tus entregas.",
            "/Delivery/GestionarEntregas");

        return repartidor;
    }

    public void AsignarPendientes(string? excluirDni = null)
    {
        // Pedido más antiguo esperando repartidor (sin asignar).
        var pendiente = _context.Orders
            .Include(o => o.Restaurant)
            .Where(o => o.Status == "Pendiente" && o.DeliveryDni == null)
            .OrderBy(o => o.DateOrder)
            .FirstOrDefault();
        if (pendiente == null) return;

        var rest = pendiente.Restaurant;
        var repartidor = BuscarDisponibleMasCercano(
            rest?.Latitude ?? 0.3517, rest?.Longitude ?? -78.1223, excluirDni);
        if (repartidor == null) return;

        pendiente.DeliveryDni = repartidor.Dni;
        pendiente.Status = "PendienteRepartidor";
        _context.SaveChanges();

        _ = _notificaciones.NotificarUsuarioAsync(repartidor.Dni,
            "Nuevo pedido asignado",
            $"Pedido {pendiente.Id} de {rest?.Name ?? "un restaurante"}. Acéptalo en tus entregas.",
            "/Delivery/GestionarEntregas");
        if (pendiente.ClientDni != null)
        {
            _ = _notificaciones.NotificarUsuarioAsync(pendiente.ClientDni,
                "¡Repartidor encontrado!",
                $"{repartidor.Name} tomará tu pedido {pendiente.Id}.",
                "/Cliente/HistorialPedidos");
        }
    }
}
