using System.Collections.Generic;

namespace delivery_system_dotnet.Services;

// Estado actual de un seguimiento activo (para clientes que se conectan tarde).
public class TrackingSnapshot
{
    public string Phase { get; set; } = "idle";
    public List<double[]> Route { get; set; } = new();
    public double Lat { get; set; }
    public double Lng { get; set; }
    public double Progress { get; set; }
}

// Servicio de dominio responsable del seguimiento en tiempo real del repartidor:
// calcula rutas reales sobre calles (OSRM/OpenStreetMap), desplaza al repartidor
// de forma continua, persiste su posición y notifica a los clientes por SignalR.
public interface IDeliveryTrackingService
{
    // Invocado por RealOrderManager cada vez que cambia el estado de un pedido.
    void OnOrderStatusChanged(string orderId, string newStatus);

    // Invocado cuando el administrador asigna un repartidor al pedido.
    void OnRiderAssigned(string orderId);

    // Estado actual del recorrido (ruta + posición) para clientes recién conectados.
    TrackingSnapshot? GetSnapshot(string orderId);

    // Recupera simulaciones activas después de reiniciar el servidor.
    void ResumeActiveDeliveries();
}
