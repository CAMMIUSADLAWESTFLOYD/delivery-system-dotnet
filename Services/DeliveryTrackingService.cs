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

    // SIMULACIÓN DE DEMO: cada tramo (repartidor→restaurante y
    // restaurante→cliente) dura un tiempo FIJO de 20 s, sin importar la
    // distancia; entre ambos hay una espera de 20 s mientras el restaurante
    // "prepara" el pedido. Así el recorrido siempre es visible en el mapa.
    private const double LegDurationSeconds = 20.0;
    private const double PrepSeconds = 20.0;
    // Emisión cada 1 s: sincronizada con el panTo animado del mapa.
    private const int TickMilliseconds = 1000;
    private const int PersistEveryTicks = 3;          // guarda en BD cada ~3 s

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
        ILogger<DeliveryTrackingService> logger)
    {
        _hub = hub;
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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
                    // Si nunca ha repartido, arranca desde un punto plausible de la
                    // ciudad a ~1 km del restaurante, nunca desde el restaurante mismo.
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

            // Tramo degenerado (< 10 m): no hay nada que recorrer.
            bool skipTravel = cum[^1] < 10.0;

            while (!ct.IsCancellationRequested && !skipTravel)
            {
                // Interpolación por TIEMPO: el tramo completo dura LegDurationSeconds,
                // así el movimiento siempre es visible sin importar la distancia real.
                double frac = Math.Min(1.0, (DateTime.UtcNow - started).TotalSeconds / LegDurationSeconds);
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

                // Tramo final dentro del mismo run (misma cancelación).
                await RunLegAsync(orderId, toClient: true, run, ct);
            }
            else
            {
                // Llegó al cliente: la entrega se completa automáticamente.
                run.Phase = "delivered";
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var order = db.Orders.FirstOrDefault(o => o.Id == orderId);
                    if (order != null && order.Status == "En Camino")
                    {
                        order.Status = "Entregado";
                        db.SaveChanges();
                    }
                }
                await _hub.Clients.Group(Group(orderId)).SendAsync("ReceivePhase", "delivered", ct);
                await _hub.Clients.Group(Group(orderId)).SendAsync("ReceiveStatus", "Entregado", ct);
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
