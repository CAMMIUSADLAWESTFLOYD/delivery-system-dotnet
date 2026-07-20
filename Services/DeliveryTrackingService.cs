using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using delivery_system_dotnet.Data;
using delivery_system_dotnet.Hubs;

namespace delivery_system_dotnet.Services;

// Implementación del seguimiento en tiempo real (singleton).
//  - La RUTA se calcula en el SERVIDOR con OSRM (OpenStreetMap): puntos reales
//    sobre las calles, nunca líneas rectas (la recta es solo degradación si el
//    servicio externo no responde, densificada para mantener el movimiento fluido).
//  - El MOVIMIENTO es continuo: velocidad constante (~36 km/h) recorriendo la
//    polilínea por distancia acumulada; cada tick (600 ms) se interpola la
//    posición exacta y se difunde por SignalR (sin polling del cliente).
//  - La POSICIÓN se persiste en Order.CurrentLatitude/Longitude y en
//    Delivery.Latitude/Longitude + LastLocationUpdate.
public class DeliveryTrackingService : IDeliveryTrackingService
{
    private readonly IHubContext<TrackingHub> _hub;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DeliveryTrackingService> _logger;
    private readonly INotificationService _notificaciones;

    // SIMULACIÓN REALISTA: el repartidor recorre la ruta a velocidad de moto
    // urbana lenta (~14 km/h: tráfico, semáforos, buscar la dirección). La
    // duración de cada tramo sale de la LONGITUD REAL de la ruta calle a calle,
    // acotada para que la demo siga siendo observable: nunca menos de 2 min ni
    // más de 7 min por tramo. El mínimo alto evita que en los trayectos cortos
    // de Ibarra el marcador "vuele". Entre ambos tramos el restaurante "prepara"
    // el pedido durante PrepSeconds.
    // Tick de 500 ms => pasos cortos y movimiento fluido, sin saltos bruscos.
    private const double SpeedKmh = 14.0;
    private const double MinLegSeconds = 120.0;
    private const double MaxLegSeconds = 420.0;
    private const double PrepSeconds = 45.0;
    private const int TickMilliseconds = 500;
    private const int PersistEveryTicks = 6;          // guarda en BD cada ~3 s

    private sealed class ActiveRun
    {
        public CancellationTokenSource Cts { get; } = new();
        public volatile string Phase = "idle";
        public List<double[]> Route = new();
        public double Lat;
        public double Lng;
        public double Progress;
    }

    private readonly ConcurrentDictionary<string, ActiveRun> _runs = new();

    public DeliveryTrackingService(
        IHubContext<TrackingHub> hub,
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<DeliveryTrackingService> logger,
        INotificationService notificaciones)
    {
        _hub = hub;
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _notificaciones = notificaciones;
    }

    // Notifica (toast) al cliente dueño del pedido, si lo hay.
    private void NotificarClientePedido(string orderId, string titulo, string mensaje)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clientDni = db.Orders.Where(o => o.Id == orderId).Select(o => o.ClientDni).FirstOrDefault();
        if (!string.IsNullOrEmpty(clientDni))
            _ = _notificaciones.NotificarUsuarioAsync(clientDni, titulo, mensaje, "/Cliente/SeguimientoPedido?orderId=" + orderId);
    }

    private static string Group(string orderId) => "order-" + orderId;

    public void OnOrderStatusChanged(string orderId, string newStatus)
    {
        // Notificar el nuevo estado a todos los observadores del pedido.
        _ = _hub.Clients.Group(Group(orderId)).SendAsync("ReceiveStatus", newStatus);

        switch (newStatus)
        {
            case "Aceptado":
                StartLeg(orderId, toClient: false); // repartidor -> restaurante
                break;
            case "En Camino":
                StartLeg(orderId, toClient: true);  // restaurante -> cliente
                break;
            case "Entregado":
            case "Cancelado":
            case "CancelacionSolicitada": // pausa mientras el admin decide
            case "Reportado":
                Stop(orderId);
                break;
        }
    }

    public void OnRiderAssigned(string orderId)
    {
        // Si el pedido ya fue aceptado, el repartidor recién asignado inicia
        // inmediatamente su recorrido hacia el restaurante.
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var status = db.Orders.Where(o => o.Id == orderId).Select(o => o.Status).FirstOrDefault();
        if (status == "Aceptado")
        {
            StartLeg(orderId, toClient: false);
        }
    }

    public TrackingSnapshot? GetSnapshot(string orderId)
    {
        if (!_runs.TryGetValue(orderId, out var run) || run.Route.Count == 0) return null;
        return new TrackingSnapshot
        {
            Phase = run.Phase,
            Route = run.Route,
            Lat = run.Lat,
            Lng = run.Lng,
            Progress = run.Progress
        };
    }

    public void ResumeActiveDeliveries()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var active = db.Orders
            .Where(o => o.DeliveryDni != null &&
                        (o.Status == "Aceptado" || o.Status == "Preparando" || o.Status == "En Camino"))
            .Select(o => new { o.Id, o.Status })
            .ToList();

        foreach (var order in active)
        {
            if (order.Status == "Aceptado") StartLeg(order.Id, toClient: false);
            else if (order.Status == "En Camino") StartLeg(order.Id, toClient: true);
            else ResumePreparation(order.Id);
        }
    }

    private void ResumePreparation(string orderId)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(PrepSeconds));
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var order = db.Orders.FirstOrDefault(o => o.Id == orderId);
            if (order == null || order.Status != "Preparando") return;
            order.Status = "En Camino";
            db.SaveChanges();
            await _hub.Clients.Group(Group(orderId)).SendAsync("ReceiveStatus", "En Camino");
            StartLeg(orderId, toClient: true);
        });
    }

    private void StartLeg(string orderId, bool toClient)
    {
        Stop(orderId);
        var run = new ActiveRun();
        _runs[orderId] = run;
        _ = Task.Run(() => RunLegAsync(orderId, toClient, run, run.Cts.Token));
    }

    private void Stop(string orderId)
    {
        if (_runs.TryRemove(orderId, out var run))
        {
            run.Cts.Cancel();
            run.Cts.Dispose();
        }
    }

    private async Task RunLegAsync(string orderId, bool toClient, ActiveRun run, CancellationToken ct)
    {
        try
        {
            double fromLat, fromLng, toLat, toLng;
            string phase;

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var order = db.Orders
                    .Include(o => o.Restaurant)
                    .Include(o => o.Client)
                    .Include(o => o.Delivery)
                    .FirstOrDefault(o => o.Id == orderId);

                if (order?.Restaurant == null) return;

                double restLat = order.Restaurant.Latitude;
                double restLng = order.Restaurant.Longitude;
                double? cliLat = order.DestLatitude ?? order.Client?.Latitude;
                double? cliLng = order.DestLongitude ?? order.Client?.Longitude;

                if (toClient)
                {
                    if (cliLat == null || cliLng == null) return;
                    if (order.Delivery == null) return; // sin repartidor no hay recorrido
                    // Continuidad: parte de donde esté realmente (última posición conocida).
                    fromLat = order.CurrentLatitude ?? restLat;
                    fromLng = order.CurrentLongitude ?? restLng;
                    toLat = cliLat.Value; toLng = cliLng.Value;
                    phase = "to_client";
                }
                else
                {
                    if (order.Delivery == null) return; // sin repartidor no hay recorrido
                    // Posición REAL del repartidor (persistida en su última entrega).
                    // La vista de entregas captura la geolocalización del dispositivo
                    // al aceptar. Para cuentas antiguas sin ubicación se usa un punto
                    // estable cercano, evitando saltos aleatorios entre simulaciones.
                    fromLat = order.Delivery.Latitude ?? restLat + 0.0080;
                    fromLng = order.Delivery.Longitude ?? restLng - 0.0060;
                    toLat = restLat; toLng = restLng;
                    phase = "to_restaurant";
                }
            }

            var route = await GetRouteAsync(fromLat, fromLng, toLat, toLng, ct);
            if (route.Count < 2) route = new List<double[]> { new[] { fromLat, fromLng }, new[] { toLat, toLng } };

            run.Phase = phase;
            run.Route = route;
            run.Lat = route[0][0];
            run.Lng = route[0][1];

            await _hub.Clients.Group(Group(orderId)).SendAsync("ReceiveRoute", phase, route, ct);

            var cum = CumulativeDistances(route);
            var started = DateTime.UtcNow;
            int tick = 0;

            // Duración del tramo según la LONGITUD REAL de la ruta a ~25 km/h,
            // acotada a [45 s, 4 min] para que la demo siga siendo observable.
            double legSeconds = Math.Clamp(
                (cum[^1] / 1000.0) / SpeedKmh * 3600.0, MinLegSeconds, MaxLegSeconds);
            _logger.LogInformation(
                "Pedido {OrderId} — tramo {Phase}: {Metros:F0} m a {Vel} km/h => {Seg:F0} s de recorrido.",
                orderId, phase, cum[^1], SpeedKmh, legSeconds);

            // Tramo degenerado (< 10 m): no hay nada que recorrer.
            bool skipTravel = cum[^1] < 10.0;

            while (!ct.IsCancellationRequested && !skipTravel)
            {
                // Interpolación por tiempo sobre la geometría real de la ruta.
                double frac = Math.Min(1.0, (DateTime.UtcNow - started).TotalSeconds / legSeconds);
                var (lat, lng) = PointAt(route, cum, frac);

                run.Lat = lat; run.Lng = lng; run.Progress = frac;
                await _hub.Clients.Group(Group(orderId)).SendAsync("ReceivePosition", lat, lng, phase, frac, ct);

                if (++tick % PersistEveryTicks == 0 || frac >= 1.0)
                {
                    if (!await PersistPositionAsync(orderId, lat, lng, ct)) return; // cancelado externamente
                }

                if (frac >= 1.0) break;
                await Task.Delay(TickMilliseconds, ct);
            }

            if (ct.IsCancellationRequested) return;

            if (phase == "to_restaurant")
            {
                // Llegó al restaurante: el pedido pasa a "Preparando" y el
                // repartidor espera PrepSeconds a que el local se lo entregue.
                run.Phase = "at_restaurant";
                await _hub.Clients.Group(Group(orderId)).SendAsync("ReceivePhase", "at_restaurant", ct);

                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var order = db.Orders.FirstOrDefault(o => o.Id == orderId);
                    if (order == null || order.Status != "Aceptado") return; // el estado cambió por fuera
                    order.Status = "Preparando";
                    db.SaveChanges();
                }
                await _hub.Clients.Group(Group(orderId)).SendAsync("ReceiveStatus", "Preparando", ct);
                NotificarClientePedido(orderId, "Preparando tu pedido",
                    "El repartidor llegó al restaurante; están preparando tu pedido.");

                await Task.Delay(TimeSpan.FromSeconds(PrepSeconds), ct);

                // Pedido listo: sale automáticamente hacia el cliente (si nadie
                // cambió el estado durante la preparación).
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var order = db.Orders.FirstOrDefault(o => o.Id == orderId);
                    if (order == null || order.Status != "Preparando") return;
                    order.Status = "En Camino";
                    db.SaveChanges();
                }
                await _hub.Clients.Group(Group(orderId)).SendAsync("ReceiveStatus", "En Camino", ct);
                NotificarClientePedido(orderId, "¡Tu pedido va en camino!",
                    "El repartidor salió del restaurante hacia tu dirección.");

                // Tramo final dentro del mismo run (misma cancelación).
                await RunLegAsync(orderId, toClient: true, run, ct);
            }
            else
            {
                // Llegó al cliente: se detiene el movimiento en la puerta del cliente.
                // NO marcamos como "Entregado" automáticamente para obligar al repartidor
                // a realizar y registrar el cobro en la aplicación.
                run.Phase = "delivered";
                await _hub.Clients.Group(Group(orderId)).SendAsync("ReceivePhase", "delivered", ct);
                // El estado sigue siendo "En Camino", esperando el clic de confirmación.
                _runs.TryRemove(orderId, out _);
            }
        }
        catch (OperationCanceledException) { /* recorrido detenido */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo en la simulación de entrega del pedido {OrderId}", orderId);
        }
    }

    // Persiste la posición actual; devuelve false si el pedido fue cancelado
    // externamente (en cuyo caso el recorrido se detiene).
    private async Task<bool> PersistPositionAsync(string orderId, double lat, double lng, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var order = await db.Orders.Include(o => o.Delivery).FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order == null || order.Status == "Cancelado")
        {
            Stop(orderId);
            return false;
        }

        order.CurrentLatitude = lat;
        order.CurrentLongitude = lng;
        if (order.Delivery != null)
        {
            order.Delivery.Latitude = lat;
            order.Delivery.Longitude = lng;
            order.Delivery.LastLocationUpdate = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return true;
    }

    // Ruta real sobre calles usando el servicio público de OSRM (OpenStreetMap).
    // Devuelve la geometría como lista de [lat, lng]. Si OSRM no responde, se
    // degrada a una línea densificada (50 puntos) para no interrumpir la entrega.
    private async Task<List<double[]>> GetRouteAsync(double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct)
    {
        try
        {
            var inv = CultureInfo.InvariantCulture;
            var url = "https://router.project-osrm.org/route/v1/driving/"
                      + fromLng.ToString(inv) + "," + fromLat.ToString(inv) + ";"
                      + toLng.ToString(inv) + "," + toLat.ToString(inv)
                      + "?overview=full&geometries=geojson";

            var client = _httpClientFactory.CreateClient("osrm");
            client.DefaultRequestHeaders.Add("User-Agent", "FlashBiteDeliveryApp/1.0");
            client.Timeout = TimeSpan.FromSeconds(5);
            using var resp = await client.GetAsync(url, ct);
            if (resp.IsSuccessStatusCode)
            {
                using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (doc.RootElement.TryGetProperty("routes", out var routes) && routes.GetArrayLength() > 0)
                {
                    var coords = routes[0].GetProperty("geometry").GetProperty("coordinates");
                    var pts = new List<double[]>(coords.GetArrayLength());
                    foreach (var c in coords.EnumerateArray())
                    {
                        // GeoJSON entrega [lng, lat] -> convertimos a [lat, lng].
                        pts.Add(new[] { c[1].GetDouble(), c[0].GetDouble() });
                    }
                    if (pts.Count >= 2)
                    {
                        _logger.LogInformation("Ruta OSRM obtenida para tramo ({Count} puntos de calle).", pts.Count);
                        return pts;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OSRM no disponible; se usa trayecto densificado de respaldo.");
        }

        // Respaldo densificado (mantiene el movimiento fluido aunque no siga calles).
        var fallback = new List<double[]>(51);
        for (int i = 0; i <= 50; i++)
        {
            double t = i / 50.0;
            fallback.Add(new[] { fromLat + (toLat - fromLat) * t, fromLng + (toLng - fromLng) * t });
        }
        return fallback;
    }

    // ----- Geometría: distancia acumulada e interpolación sobre la polilínea -----

    private static double Haversine(double[] a, double[] b)
    {
        const double R = 6371000.0;
        double dLat = (b[0] - a[0]) * Math.PI / 180.0;
        double dLng = (b[1] - a[1]) * Math.PI / 180.0;
        double la1 = a[0] * Math.PI / 180.0, la2 = b[0] * Math.PI / 180.0;
        double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(la1) * Math.Cos(la2) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Sqrt(h));
    }

    private static double[] CumulativeDistances(List<double[]> pts)
    {
        var cum = new double[pts.Count];
        for (int i = 1; i < pts.Count; i++)
            cum[i] = cum[i - 1] + Haversine(pts[i - 1], pts[i]);
        return cum;
    }

    private static (double lat, double lng) PointAt(List<double[]> pts, double[] cum, double frac)
    {
        double target = frac * cum[^1];
        for (int i = 1; i < pts.Count; i++)
        {
            if (cum[i] >= target)
            {
                double seg = cum[i] - cum[i - 1];
                double t = seg <= 0 ? 0 : (target - cum[i - 1]) / seg;
                return (pts[i - 1][0] + (pts[i][0] - pts[i - 1][0]) * t,
                        pts[i - 1][1] + (pts[i][1] - pts[i - 1][1]) * t);
            }
        }
        return (pts[^1][0], pts[^1][1]);
    }
}
