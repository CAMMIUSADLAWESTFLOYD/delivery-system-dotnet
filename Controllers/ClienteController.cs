using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delivery_system_dotnet.Data;
using delivery_system_dotnet.Models;
using delivery_system_dotnet.Services;

namespace delivery_system_dotnet.Controllers;

[Authorize(Roles = "Client")]
public class ClienteController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IUsuarioService _usuarioService;
    private readonly IWebHostEnvironment _env;

    public ClienteController(ApplicationDbContext context, IUsuarioService usuarioService, IWebHostEnvironment env)
    {
        _context = context;
        _usuarioService = usuarioService;
        _env = env;
    }

    public IActionResult Dashboard()
    {
        return View();
    }

    [HttpGet]
    public IActionResult BuscarProductos(string? category, string? search)
    {
        var query = _context.Products.Include(p => p.Restaurant).AsQueryable();
        
        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(p => p.Category == category);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(p => p.Name.Contains(search));
        }

        var products = query.ToList();
        ViewBag.Category = category;
        ViewBag.Search = search;
        ViewBag.Categorias = _context.Products.Where(p => p.Category != null).Select(p => p.Category!).Distinct().ToList();
        return View("BuscarProductos", products);
    }

    [HttpGet]
    public IActionResult DetalleProducto(int id)
    {
        var product = _context.Products
            .Include(p => p.Restaurant)
            .FirstOrDefault(p => p.Id == id);
        if (product == null) return NotFound();
        return View("DetalleProducto", product);
    }

    [HttpGet]
    public IActionResult Carrito()
    {
        var cart = GetCart();
        var model = new CartViewModel();

        foreach (var kv in cart)
        {
            var p = _context.Products.FirstOrDefault(x => x.Id == kv.Key);
            if (p == null) continue;
            model.Items.Add(new CartLineItem
            {
                ProductId = p.Id,
                ProductName = p.Name,
                Price = p.Price,
                Quantity = kv.Value,
                ImagePath = p.ImagePath,
                Available = p.Available
            });
        }

        model.DeliveryFee = model.Items.Any() ? 1.50 : 0.0;

        // Cargar repartidores disponibles
        ViewBag.Repartidores = _context.Deliveries.Where(d => d.Status).ToList();

        // Origen para la previsualización de ruta del PinSelector
        var firstId = cart.Keys.FirstOrDefault();
        var firstProd = firstId != 0
            ? _context.Products.Include(p => p.Restaurant).FirstOrDefault(p => p.Id == firstId)
            : null;
        var rest = firstProd?.Restaurant ?? _context.Restaurants.FirstOrDefault();
        ViewBag.RestName = rest?.Name;
        ViewBag.RestLat = rest?.Latitude;
        ViewBag.RestLng = rest?.Longitude;

        return View("Carrito", model);
    }

    [HttpPost]
    public IActionResult RealizarPedido(string destinyAddres, int productId, int quantity, double? ClienteLatitud, double? ClienteLongitud)
    {
        // Validación Anti-Océano (Failsafe)
        if (ClienteLatitud == 0 || ClienteLongitud == 0 || ClienteLatitud == null || ClienteLongitud == null)
        {
            // Si el frontend falló, forzamos coordenadas válidas por defecto (Ibarra)
            ClienteLatitud = 0.3517;
            ClienteLongitud = -78.1218;
        }

        var clientDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (clientDni == null) return Unauthorized();

        var product = _context.Products
            .Include(p => p.Restaurant)
            .FirstOrDefault(p => p.Id == productId);
        if (product == null || !product.Available)
        {
            TempData["Error"] = "Producto no disponible.";
            return RedirectToAction("BuscarProductos");
        }

        // El restaurante del pedido es SIEMPRE el del propio producto,
        // relacionado por RestaurantId (el mapa usa su origen real).
        var restaurant = product.Restaurant;
        if (restaurant == null) return NotFound();

        var orderId = "ORD-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
        var order = new Order
        {
            Id = orderId,
            Status = "Pendiente",
            DestinyAddres = destinyAddres,
            RestaurantId = restaurant.Id,
            ClientDni = clientDni,
            Distance = 4.2,
            PaymentStatus = false
        };
        AplicarUbicacionEntrega(order, clientDni, ClienteLatitud, ClienteLongitud);
        order.ClienteLatitud = ClienteLatitud ?? 0.3517;
        order.ClienteLongitud = ClienteLongitud ?? -78.1218;

        _context.Orders.Add(order);
        _context.SaveChanges();

        order.AddNewProduct(product, quantity);
        _context.SaveChanges();

        TempData["Message"] = $"Pedido {orderId} realizado con éxito.";
        return RedirectToAction("HistorialPedidos");
    }

    // Fija las coordenadas de destino del pedido a partir de la geolocalización
    // enviada (si existe) y, en su defecto, de la ubicación guardada del cliente.
    // Si llega una geolocalización nueva, también actualiza la del cliente en BD.
    private void AplicarUbicacionEntrega(Order order, string clientDni, double? destLat, double? destLng)
    {
        var client = _context.Clients.FirstOrDefault(c => c.Dni == clientDni);

        if (destLat.HasValue && destLng.HasValue)
        {
            order.DestLatitude = destLat;
            order.DestLongitude = destLng;
            if (client != null)
            {
                client.Latitude = destLat;
                client.Longitude = destLng;
            }
        }
        else if (client?.Latitude != null && client.Longitude != null)
        {
            order.DestLatitude = client.Latitude;
            order.DestLongitude = client.Longitude;
        }
    }

    // Guarda la ubicación del cliente obtenida por navigator.geolocation (AJAX).
    [HttpPost]
    public IActionResult GuardarUbicacion(double lat, double lng)
    {
        var clientDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (clientDni == null) return Unauthorized();

        var client = _context.Clients.FirstOrDefault(c => c.Dni == clientDni);
        if (client == null) return NotFound();

        client.Latitude = lat;
        client.Longitude = lng;
        _context.SaveChanges();

        return Json(new { ok = true, lat, lng });
    }

    [HttpGet]
    public IActionResult HistorialPedidos()
    {
        var clientDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (clientDni == null) return Unauthorized();

        var orders = _context.Orders
            .Include(o => o.Restaurant)
            .Include(o => o.Delivery)
            .Where(o => o.ClientDni == clientDni)
            .OrderByDescending(o => o.DateOrder)
            .ToList();

        // Cargar repartidores disponibles para reasignación de pedidos pendientes
        ViewBag.Repartidores = _context.Deliveries.Where(d => d.Status).ToList();

        return View("HistorialPedidos", orders);
    }

    [HttpGet]
    public IActionResult SeguimientoPedido(string orderId)
    {
        var order = _context.Orders
            .Include(o => o.Restaurant)
            .Include(o => o.Delivery)
            .Include(o => o.Client)
            .FirstOrDefault(o => o.Id == orderId);
        
        if (order == null) return NotFound();

        // Parche de emergencia para Leaflet Routing (Asignar destino por defecto)
        if (order.ClienteLatitud == 0 && order.ClienteLongitud == 0)
        {
            order.ClienteLatitud = 0.3510;
            order.ClienteLongitud = -78.1220;
            _context.SaveChanges();
        }

        return View("SeguimientoPedido", order);
    }

    [HttpPost]
    public IActionResult CalificarServicio(string orderId, int rating)
    {
        var clientDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (clientDni == null) return Unauthorized();

        // Solo el dueño del pedido puede calificarlo.
        var order = _context.Orders
            .Include(o => o.Delivery)
            .FirstOrDefault(o => o.Id == orderId && o.ClientDni == clientDni);
        if (order == null) return NotFound();

        if (order.Status != "Entregado")
        {
            TempData["Error"] = "Solo puedes calificar pedidos que ya fueron entregados.";
            return RedirectToAction("HistorialPedidos");
        }

        if (order.Rating != null)
        {
            TempData["Error"] = $"El pedido {orderId} ya fue calificado con {order.Rating} estrellas.";
            return RedirectToAction("HistorialPedidos");
        }

        if (rating < 1 || rating > 5)
        {
            TempData["Error"] = "La calificación debe estar entre 1 y 5 estrellas.";
            return RedirectToAction("HistorialPedidos");
        }

        order.Rating = rating;

        // Recalcular el promedio del repartidor a partir de TODAS sus calificaciones.
        if (order.Delivery != null)
        {
            _context.SaveChanges(); // asegura que esta calificación entre en el promedio

            var notas = _context.Orders
                .Where(o => o.DeliveryDni == order.DeliveryDni && o.Rating != null)
                .Select(o => o.Rating!.Value)
                .ToList();

            if (notas.Count > 0)
            {
                order.Delivery.Rating = Math.Round(notas.Average(), 2);
            }
        }

        _context.SaveChanges();

        var quien = order.Delivery?.Name != null ? $"al repartidor {order.Delivery.Name}" : "el servicio";
        TempData["Message"] = $"¡Gracias! Calificaste {quien} con {rating} estrella{(rating == 1 ? "" : "s")}.";
        return RedirectToAction("HistorialPedidos");
    }

    [HttpPost]
    public IActionResult CancelarPedido(string orderId)
    {
        var order = _context.Orders.FirstOrDefault(o => o.Id == orderId);
        if (order == null) return NotFound();

        if (order.Status != "Pendiente")
        {
            TempData["Error"] = "Solo se pueden cancelar pedidos en estado 'Pendiente'.";
            return RedirectToAction("HistorialPedidos");
        }

        order.Status = "Cancelado";
        _context.SaveChanges();

        TempData["Message"] = $"El pedido {orderId} fue cancelado exitosamente.";
        return RedirectToAction("HistorialPedidos");
    }

    // ============================================================

    // ============================================================
    //  Buscar restaurantes: listado con buscador por nombre y
    //  filtro por categoría (vista BuscarRestaurantes.cshtml).
    // ============================================================
    [HttpGet]
    public IActionResult BuscarRestaurantes(string? search, string? category)
    {
        // Solo restaurantes activos.
        var query = _context.Restaurants.Where(r => r.IsActive);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(r => r.Name.Contains(search));

        if (!string.IsNullOrEmpty(category))
            query = query.Where(r => r.Category == category);

        var restaurantes = query.ToList();

        // Lista de categorías disponibles para el filtro (a partir de los datos reales).
        ViewBag.Categorias = _context.Restaurants
            .Where(r => r.IsActive && r.Category != null)
            .Select(r => r.Category!)
            .Distinct()
            .ToList();
        ViewBag.Search = search;
        ViewBag.Category = category;

        return View("BuscarRestaurantes", restaurantes);
    }

    // Detalle de un restaurante (por Id): su información + todos sus productos.
    [HttpGet]
    public IActionResult DetalleRestaurante(int id)
    {
        var restaurante = _context.Restaurants
            .Include(r => r.Products)
            .FirstOrDefault(r => r.Id == id);
            
        if (restaurante == null)
            return NotFound();

        ViewBag.CartCount = HttpContext.Session.GetInt32("CartCount") ?? 0;
        return View("DetalleRestaurante", restaurante);
    }

    // ============================================================
    //  Carrito de compras basado en sesión (vista ViewCart.cshtml)
    //  Acompaña a la clase de dominio Cart (UML).
    // ============================================================
    private const string CartSessionKey = "Cart";

    // El carrito de sesión se indexa por ProductId (los nombres de producto
    // pueden repetirse entre restaurantes; el Id es la única clave fiable).
    private Dictionary<int, int> GetCart()
    {
        var json = HttpContext.Session.GetString(CartSessionKey);
        if (string.IsNullOrEmpty(json)) return new Dictionary<int, int>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<int, int>>(json) ?? new Dictionary<int, int>();
        }
        catch
        {
            // Carrito con formato antiguo (claves por nombre): se descarta.
            return new Dictionary<int, int>();
        }
    }

    private void SaveCart(Dictionary<int, int> cart)
    {
        HttpContext.Session.SetString(CartSessionKey, JsonSerializer.Serialize(cart));
        HttpContext.Session.SetInt32("CartCount", cart.Values.Sum());
    }

    private IActionResult RedirectBack(string fallbackAction)
    {
        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrEmpty(referer)) return Redirect(referer);
        return RedirectToAction(fallbackAction);
    }

    [HttpPost]
    public IActionResult AddToCart(int productId, int quantity)
    {
        var product = _context.Products.FirstOrDefault(p => p.Id == productId);
        if (product == null || !product.Available)
        {
            TempData["Error"] = "Producto no disponible.";
            return RedirectBack("BuscarProductos");
        }

        if (quantity < 1) quantity = 1;

        var cart = GetCart();
        var current = cart.TryGetValue(productId, out var q) ? q : 0;
        cart[productId] = current + quantity;
        SaveCart(cart);

        TempData["Message"] = $"{product.Name} agregado al carrito.";
        return RedirectBack("BuscarProductos");
    }



    [HttpPost]
    public IActionResult UpdateCartItem(int productId, int quantity)
    {
        var cart = GetCart();
        if (quantity <= 0)
            cart.Remove(productId);
        else
            cart[productId] = quantity;
        SaveCart(cart);
        return RedirectToAction("Carrito");
    }

    [HttpPost]
    public IActionResult RemoveFromCart(int productId)
    {
        var cart = GetCart();
        cart.Remove(productId);
        SaveCart(cart);
        return RedirectToAction("Carrito");
    }

    [HttpPost]
    public IActionResult Checkout(string destinyAddres, string deliveryDni, double? ClienteLatitud, double? ClienteLongitud)
    {
        // Validación Anti-Océano (Failsafe)
        if (ClienteLatitud == 0 || ClienteLongitud == 0 || ClienteLatitud == null || ClienteLongitud == null) 
        {
            // Si el frontend falló, forzamos coordenadas válidas por defecto (Ibarra)
            ClienteLatitud = 0.3517;
            ClienteLongitud = -78.1218;
        }

        var clientDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (clientDni == null) return Unauthorized();

        var cart = GetCart();
        if (!cart.Any())
        {
            TempData["Error"] = "Tu carrito está vacío.";
            return RedirectToAction("Carrito");
        }
        
        if (string.IsNullOrEmpty(deliveryDni))
        {
            TempData["Error"] = "Debes seleccionar un repartidor disponible.";
            return RedirectToAction("Carrito");
        }

        // El restaurante del pedido se toma del primer producto del carrito
        // (relación por RestaurantId), para que el mapa use el origen real.
        var firstProductId = cart.Keys.First();
        var firstProduct = _context.Products
            .Include(p => p.Restaurant)
            .FirstOrDefault(p => p.Id == firstProductId);
        var restaurant = firstProduct?.Restaurant ?? _context.Restaurants.FirstOrDefault();

        var orderId = "ORD-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
        var order = new Order
        {
            Id = orderId,
            Status = "PendienteRepartidor", // Nuevo estado esperando aceptación
            DestinyAddres = destinyAddres,
            RestaurantId = restaurant?.Id,
            ClientDni = clientDni,
            DeliveryDni = deliveryDni,
            Distance = 4.2,
            PaymentStatus = false
        };
        AplicarUbicacionEntrega(order, clientDni, ClienteLatitud, ClienteLongitud);
        order.ClienteLatitud = ClienteLatitud ?? 0.3517;
        order.ClienteLongitud = ClienteLongitud ?? -78.1218;

        _context.Orders.Add(order);
        _context.SaveChanges();

        foreach (var kv in cart)
        {
            var product = _context.Products.FirstOrDefault(p => p.Id == kv.Key);
            if (product == null || !product.Available) continue;
            order.AddNewProduct(product, kv.Value);
        }
        _context.SaveChanges();

        SaveCart(new Dictionary<int, int>());
        TempData["Message"] = $"Pedido {orderId} confirmado. Esperando que el repartidor lo acepte.";
        return RedirectToAction("HistorialPedidos");
    }

    [HttpPost]
    public IActionResult ElegirRepartidor(string orderId, string deliveryDni)
    {
        var clientDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var order = _context.Orders.FirstOrDefault(o => o.Id == orderId && o.ClientDni == clientDni);
        if (order == null) return NotFound();

        if (order.Status != "Pendiente")
        {
            TempData["Error"] = "El pedido no está en estado pendiente de asignación.";
            return RedirectToAction("HistorialPedidos");
        }

        order.DeliveryDni = deliveryDni;
        order.Status = "PendienteRepartidor";
        _context.SaveChanges();

        TempData["Message"] = $"Repartidor asignado al pedido {orderId}. Esperando que lo acepte.";
        return RedirectToAction("HistorialPedidos");
    }

    // ============================================================
    //  Caso de uso "Track Order" (vista TrackOrder.cshtml) con mapa.
    //  TrackOrder renderiza la vista; TrackOrderStatus expone el
    //  estado y el progreso en JSON para el sondeo en tiempo real.
    // ============================================================


    [HttpGet]
    public IActionResult TrackOrderStatus(string orderId)
    {
        var order = _context.Orders
            .Include(o => o.Delivery)
            .Include(o => o.Restaurant)
            .Include(o => o.Client)
            .FirstOrDefault(o => o.Id == orderId);
        if (order == null) return NotFound();

        // Fase del recorrido derivada del estado REAL del pedido. Define qué tramo
        // de la ruta (repartidor->restaurante->cliente) debe recorrer el marcador.
        //   idle          : repartidor en su punto de partida
        //   to_restaurant : va del punto de partida al restaurante (recoger)
        //   at_restaurant : esperando en el restaurante (preparando)
        //   to_client     : va del restaurante al cliente (entregando)
        //   delivered     : ya en el cliente
        string phase = order.Status switch
        {
            "Aceptado" => "to_restaurant",
            "Preparando" => "at_restaurant",
            "En Camino" => "to_client",
            "Entregado" => "delivered",
            "Cancelado" => "idle",
            _ => "idle"
        };

        // PUNTOS REALES (desde la BD). Sin coordenadas fijas ni de ejemplo.
        double? restLat = order.Restaurant?.Latitude;
        double? restLng = order.Restaurant?.Longitude;

        double? clientLat = order.DestLatitude ?? order.Client?.Latitude;
        double? clientLng = order.DestLongitude ?? order.Client?.Longitude;

        // Punto de partida del repartidor: su ubicación real; si no tiene, arranca en
        // el restaurante (para que exista un primer tramo válido).
        double? delivLat = order.Delivery?.Latitude ?? restLat;
        double? delivLng = order.Delivery?.Longitude ?? restLng;

        bool hasRoute = restLat.HasValue && restLng.HasValue && clientLat.HasValue && clientLng.HasValue;

        // Este GET es de solo lectura: la posición del repartidor la escribe y
        // difunde DeliveryTrackingService (SignalR). Aquí solo se entrega el
        // estado inicial para pintar el mapa al cargar la página.
        return Json(new
        {
            status = order.Status,
            phase,
            rider = order.Delivery?.Name ?? "Asignando repartidor...",
            distance = order.Distance,
            hasRoute,
            delivLat,
            delivLng,
            restLat,
            restLng,
            clientLat,
            clientLng,
            // Última posición persistida del recorrido (si hubo movimiento).
            currentLat = order.CurrentLatitude,
            currentLng = order.CurrentLongitude
        });
    }

    // ============================================================
    //  Mi perfil / Configuración del cliente
    // ============================================================
    private string? DniActual => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet]
    public async Task<IActionResult> Perfil()
    {
        var cliente = await _usuarioService.ObtenerPorDniAsync(DniActual ?? "") as Client;
        if (cliente == null) return NotFound();
        return View("Perfil", cliente);
    }

    [HttpPost]
    public async Task<IActionResult> ActualizarPerfil(string name, string mail, string phone, string address, int payMethod)
    {
        try
        {
            var actual = await _usuarioService.ObtenerPorDniAsync(DniActual ?? "") as Client;
            if (actual == null) return NotFound();

            var modificado = new Client
            {
                Name = name,
                Mail = mail,
                Phone = phone,
                Address = address,
                PayMethod = payMethod,
                DateBirth = actual.DateBirth,
                Gender = actual.Gender
            };
            await _usuarioService.ActualizarClienteAsync(actual.Dni, modificado);
            await RefrescarSesionAsync(actual.Dni, name, mail, "Client");

            TempData["Message"] = "Tus datos se guardaron correctamente.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction("Perfil");
    }

    [HttpPost]
    public async Task<IActionResult> CambiarPasswordPerfil(string passwordActual, string passwordNueva, string passwordConfirmar)
    {
        try
        {
            if (passwordNueva != passwordConfirmar)
            {
                TempData["Error"] = "La confirmación no coincide con la nueva contraseña.";
                return RedirectToAction("Perfil");
            }
            await _usuarioService.CambiarPasswordPerfilAsync(DniActual ?? "", passwordActual, passwordNueva);
            TempData["Message"] = "Contraseña actualizada.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction("Perfil");
    }

    [HttpPost]
    public async Task<IActionResult> CambiarFotoPerfil(IFormFile foto)
    {
        try
        {
            await _usuarioService.GuardarFotoPerfilAsync(DniActual ?? "", foto, _env.WebRootPath);
            TempData["Message"] = "Foto de perfil actualizada.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction("Perfil");
    }

    // Reemite la cookie de autenticación para que el nombre y el correo
    // mostrados en la barra de navegación reflejen los cambios del perfil.
    private async Task RefrescarSesionAsync(string dni, string nombre, string correo, string rol)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, dni),
            new Claim(ClaimTypes.Name, nombre),
            new Claim(ClaimTypes.Email, correo),
            new Claim(ClaimTypes.Role, rol)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }
}
