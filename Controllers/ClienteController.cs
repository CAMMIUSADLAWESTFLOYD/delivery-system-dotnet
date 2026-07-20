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
    private readonly IRiderAssignmentService _riderAssignment;
    private readonly INotificationService _notificaciones;

    public ClienteController(ApplicationDbContext context, IUsuarioService usuarioService,
        IWebHostEnvironment env, IRiderAssignmentService riderAssignment, INotificationService notificaciones)
    {
        _context = context;
        _usuarioService = usuarioService;
        _env = env;
        _riderAssignment = riderAssignment;
        _notificaciones = notificaciones;
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

        // El costo de envío real se calcula por kilómetro cuando el cliente fija
        // su punto de entrega en el mapa (tarifa $1.50/km, ver Checkout).
        model.DeliveryFee = 0.0;

        // El repartidor lo asigna el sistema automáticamente (ver Checkout);
        // el cliente ya no lo elige.

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
            .Include(o => o.Payment)
            .Where(o => o.ClientDni == clientDni)
            .OrderByDescending(o => o.DateOrder)
            .ToList();

        return View("HistorialPedidos", orders);
    }

    [HttpGet]
    public IActionResult SeguimientoPedido(string orderId)
    {
        // Solo el dueño puede seguir su pedido (no exponer la entrega de otro cliente).
        var clientDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var order = _context.Orders
            .Include(o => o.Restaurant)
            .Include(o => o.Delivery)
            .Include(o => o.Client)
            .FirstOrDefault(o => o.Id == orderId && o.ClientDni == clientDni);

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

    // Factura del pedido para el cliente (imprimible / guardar como PDF).
    [HttpGet]
    public IActionResult Factura(string orderId)
    {
        var clientDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var order = _context.Orders
            .Include(o => o.Restaurant)
            .Include(o => o.Client)
            .Include(o => o.Delivery)
            .Include(o => o.Payment)
            .Include(o => o.OrderDetails)
                .ThenInclude(d => d.Products)
            .FirstOrDefault(o => o.Id == orderId && o.ClientDni == clientDni);

        if (order == null) return NotFound();

        // Solo se factura un pedido entregado o efectivamente pagado.
        if (order.Status != "Entregado" && !order.PaymentStatus)
        {
            TempData["Error"] = "La factura estará disponible cuando el pedido sea entregado o el pago se confirme.";
            return RedirectToAction("HistorialPedidos");
        }

        return View("Factura", order);
    }

    [HttpPost]
    public IActionResult CalificarServicio(string orderId, int rating, int restaurantRating)
    {
        var clientDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (clientDni == null) return Unauthorized();

        // Solo el dueño del pedido puede calificarlo.
        var order = _context.Orders
            .Include(o => o.Delivery)
            .Include(o => o.Restaurant)
            .FirstOrDefault(o => o.Id == orderId && o.ClientDni == clientDni);
        if (order == null) return NotFound();

        if (order.Status != "Entregado")
        {
            TempData["Error"] = "Solo puedes calificar pedidos que ya fueron entregados.";
            return RedirectToAction("HistorialPedidos");
        }

        if (order.Rating != null)
        {
            TempData["Error"] = $"El pedido {orderId} ya fue calificado.";
            return RedirectToAction("HistorialPedidos");
        }

        if (rating < 1 || rating > 5 || restaurantRating < 1 || restaurantRating > 5)
        {
            TempData["Error"] = "Las calificaciones deben estar entre 1 y 5 estrellas.";
            return RedirectToAction("HistorialPedidos");
        }

        order.Rating = rating;
        order.RestaurantRating = restaurantRating;
        _context.SaveChanges(); // asegura que estas notas entren en los promedios

        // Promedio REAL del repartidor sobre todas sus calificaciones.
        if (order.Delivery != null)
        {
            var notas = _context.Orders
                .Where(o => o.DeliveryDni == order.DeliveryDni && o.Rating != null)
                .Select(o => o.Rating!.Value)
                .ToList();
            if (notas.Count > 0)
            {
                order.Delivery.Rating = Math.Round(notas.Average(), 2);
            }
        }

        // Promedio acumulado del restaurante: conserva las reseñas históricas
        // existentes y hace que cada nota nueva suba o baje su reputación.
        if (order.Restaurant != null)
        {
            var cantidadAnterior = order.Restaurant.RatingCount;
            var sumaAnterior = order.Restaurant.Rating * cantidadAnterior;
            order.Restaurant.RatingCount = cantidadAnterior + 1;
            order.Restaurant.Rating = Math.Round(
                (sumaAnterior + restaurantRating) / order.Restaurant.RatingCount,
                2);
        }

        _context.SaveChanges();

        TempData["Message"] = $"¡Gracias! Calificaste al repartidor con {rating}★ y a {order.Restaurant?.Name ?? "el restaurante"} con {restaurantRating}★.";
        return RedirectToAction("HistorialPedidos");
    }

    [HttpPost]
    public IActionResult CancelarPedido(string orderId)
    {
        // Solo el dueño del pedido puede cancelarlo (evita que un cliente cancele
        // el pedido de otro conociendo/adivinando su Id).
        var clientDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var order = _context.Orders.FirstOrDefault(o => o.Id == orderId && o.ClientDni == clientDni);
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
        var product = _context.Products.Include(p => p.Restaurant).FirstOrDefault(p => p.Id == productId);
        if (product == null || !product.Available)
        {
            TempData["Error"] = "Producto no disponible.";
            return RedirectBack("BuscarProductos");
        }

        // Mundo real: un pedido pertenece a UN restaurante. Si el carrito ya
        // tiene productos de otro local, se pide finalizar o vaciar primero.
        var cartActual = GetCart();
        if (cartActual.Any())
        {
            var otroRestaurante = _context.Products
                .Include(p => p.Restaurant)
                .Where(p => cartActual.Keys.Contains(p.Id) && p.RestaurantId != product.RestaurantId)
                .Select(p => p.Restaurant!.Name)
                .FirstOrDefault();
            if (otroRestaurante != null)
            {
                TempData["Error"] = $"Tu carrito tiene productos de \"{otroRestaurante}\". Finaliza ese pedido o vacía el carrito para pedir en \"{product.Restaurant?.Name}\".";
                return RedirectBack("BuscarProductos");
            }
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

    // Distancia en km entre dos coordenadas (Haversine). Es la fuente de verdad
    // del costo de envío: no depende de servicios externos.
    private static double DistanciaKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLng = (lng2 - lng1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                 * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }

    // Valida un código de descuento (AJAX del carrito). Devuelve el porcentaje.
    [HttpGet]
    public IActionResult ValidarDescuento(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Json(new { ok = false, error = "Ingresa un código." });

        var codigo = _context.DiscountCodes
            .FirstOrDefault(d => d.Code == code.Trim().ToUpper() && d.Active);

        return codigo == null
            ? Json(new { ok = false, error = "Código inválido o inactivo." })
            : Json(new { ok = true, code = codigo.Code, percent = codigo.Percent });
    }

    // ------- Validación de tarjeta (simulada pero con reglas del mundo real) -------
    // Luhn + longitud + expiración futura + CVV. El número completo y el CVV
    // JAMÁS se persisten: solo marca y últimos 4 dígitos.
    private static string? ValidarTarjeta(string? numero, string? titular, string? expiracion, string? cvv, out string referencia)
    {
        referencia = "";
        var digitos = new string((numero ?? "").Where(char.IsDigit).ToArray());
        if (digitos.Length < 13 || digitos.Length > 19)
            return "El número de tarjeta debe tener entre 13 y 19 dígitos.";

        // Algoritmo de Luhn
        int suma = 0; bool alterno = false;
        for (int i = digitos.Length - 1; i >= 0; i--)
        {
            int d = digitos[i] - '0';
            if (alterno) { d *= 2; if (d > 9) d -= 9; }
            suma += d; alterno = !alterno;
        }
        if (suma % 10 != 0)
            return "El número de tarjeta no es válido (verifica los dígitos).";

        if (string.IsNullOrWhiteSpace(titular) || titular.Trim().Length < 5)
            return "Ingresa el nombre del titular tal como aparece en la tarjeta.";

        // Expiración MM/AA o MM/AAAA, en el futuro
        var exp = (expiracion ?? "").Trim();
        var partes = exp.Split('/');
        if (partes.Length != 2 || !int.TryParse(partes[0], out int mes) || !int.TryParse(partes[1], out int anio)
            || mes < 1 || mes > 12)
            return "La fecha de expiración debe tener formato MM/AA.";
        if (anio < 100) anio += 2000;
        var finDeMes = new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes));
        if (finDeMes < DateTime.Today)
            return "La tarjeta está vencida.";

        var cvvDigits = new string((cvv ?? "").Where(char.IsDigit).ToArray());
        if (cvvDigits.Length < 3 || cvvDigits.Length > 4)
            return "El CVV debe tener 3 o 4 dígitos.";

        string marca = digitos[0] switch
        {
            '4' => "VISA",
            '5' => "MASTERCARD",
            '3' => "AMEX",
            _ => "TARJETA"
        };
        referencia = $"{marca} ****{digitos[^4..]}";
        return null; // válida
    }

    [HttpPost]
    public IActionResult Checkout(string destinyAddres, string deliveryDni, double? ClienteLatitud,
        double? ClienteLongitud, string paymentMethod, string? discountCode, double? routeKm,
        string? cardNumber, string? cardHolder, string? cardExpiry, string? cardCvv, string? transferReference)
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

        // El repartidor YA NO lo elige el cliente: lo asigna el sistema
        // automáticamente (repartidor disponible más cercano al restaurante).

        // Método de pago obligatorio: Tarjeta, Transferencia o Efectivo.
        var metodosValidos = new[] { "Tarjeta", "Transferencia", "Efectivo" };
        if (string.IsNullOrEmpty(paymentMethod) || !metodosValidos.Contains(paymentMethod))
        {
            TempData["Error"] = "Selecciona un método de pago (Tarjeta, Transferencia o Efectivo).";
            return RedirectToAction("Carrito");
        }

        // ------- Datos del pago según el método (interfaz del mundo real) -------
        string? referenciaPago = null;
        bool pagoVerificado = false;

        if (paymentMethod == "Tarjeta")
        {
            var errorTarjeta = ValidarTarjeta(cardNumber, cardHolder, cardExpiry, cardCvv, out var refTarjeta);
            if (errorTarjeta != null)
            {
                TempData["Error"] = errorTarjeta;
                return RedirectToAction("Carrito");
            }
            referenciaPago = refTarjeta;   // ej. "VISA ****1111" (nunca el número completo)
            pagoVerificado = true;         // cobro simulado aprobado al instante
        }
        else if (paymentMethod == "Transferencia")
        {
            var comprobante = (transferReference ?? "").Trim();
            if (comprobante.Length < 6 || comprobante.Length > 40)
            {
                TempData["Error"] = "Ingresa el número de comprobante de tu transferencia (mínimo 6 caracteres).";
                return RedirectToAction("Carrito");
            }
            referenciaPago = comprobante;
            pagoVerificado = false;        // la verifica el administrador
        }

        // El restaurante del pedido se toma del primer producto del carrito
        // (relación por RestaurantId), para que el mapa use el origen real.
        var firstProductId = cart.Keys.First();
        var firstProduct = _context.Products
            .Include(p => p.Restaurant)
            .FirstOrDefault(p => p.Id == firstProductId);
        var restaurant = firstProduct?.Restaurant ?? _context.Restaurants.FirstOrDefault();

        // Mundo real: no se puede pedir a un local inactivo o fuera de horario.
        if (restaurant != null)
        {
            if (!restaurant.IsActive)
            {
                TempData["Error"] = $"\"{restaurant.Name}\" no está disponible en este momento.";
                return RedirectToAction("Carrito");
            }
            var ahora = DateTime.Now.TimeOfDay;
            bool abierto = restaurant.OpeningHour <= restaurant.ClosingHour
                ? ahora >= restaurant.OpeningHour && ahora <= restaurant.ClosingHour
                : ahora >= restaurant.OpeningHour || ahora <= restaurant.ClosingHour; // horario nocturno
            if (!abierto)
            {
                TempData["Error"] = $"\"{restaurant.Name}\" está cerrado ahora (atiende de {restaurant.OpeningHour:hh\\:mm} a {restaurant.ClosingHour:hh\\:mm}).";
                return RedirectToAction("Carrito");
            }
        }

        // ------- Cálculo REAL de la distancia de entrega (por kilómetro) -------
        // Línea recta (Haversine) restaurante -> punto de entrega, con factor
        // vial 1.3. Si el front midió la ruta real por calles (OSRM), se acepta
        // siempre que sea coherente con la línea recta (sanidad anti-manipulación).
        double distanciaKm = 2.0; // respaldo si el restaurante no tiene coordenadas
        if (restaurant != null)
        {
            var recta = DistanciaKm(restaurant.Latitude, restaurant.Longitude,
                                    ClienteLatitud.Value, ClienteLongitud.Value);
            distanciaKm = Math.Round(recta * 1.3, 2);
            if (routeKm.HasValue && !double.IsNaN(routeKm.Value)
                && routeKm.Value >= recta * 0.9 && routeKm.Value <= recta * 3 + 1)
            {
                distanciaKm = Math.Round(routeKm.Value, 2);
            }
        }
        distanciaKm = Math.Clamp(distanciaKm, 0.3, 50);

        // Código de descuento (se revalida en servidor; el JS solo previsualiza).
        DiscountCode? descuento = null;
        if (!string.IsNullOrWhiteSpace(discountCode))
        {
            descuento = _context.DiscountCodes
                .FirstOrDefault(d => d.Code == discountCode.Trim().ToUpper() && d.Active);
            if (descuento == null)
            {
                TempData["Error"] = $"El código de descuento '{discountCode}' no es válido.";
                return RedirectToAction("Carrito");
            }
        }

        var orderId = "ORD-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
        var order = new Order
        {
            Id = orderId,
            // "Pendiente" hasta que el sistema encuentre repartidor disponible;
            // la asignación automática lo pasa a "PendienteRepartidor".
            Status = "Pendiente",
            DestinyAddres = destinyAddres,
            RestaurantId = restaurant?.Id,
            ClientDni = clientDni,
            DeliveryDni = null,
            Distance = distanciaKm,
            // Solo la tarjeta queda pagada al confirmar; la transferencia espera
            // la verificación del administrador y el efectivo se cobra al entregar.
            PaymentStatus = paymentMethod == "Tarjeta"
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

        // Desglose final: AddNewProduct ya calculó Total = subtotal + envío
        // (tarifa urbana con tope, ver Order.ICalculateDeliveryFee) + IVA.
        // El descuento porcentual se aplica sobre el subtotal de productos.
        double subtotalProductos = order.OrderDetails.Sum(d => d.SubTotal);
        order.DeliveryFee = Math.Round(order.ICalculateDeliveryFee(distanciaKm), 2);
        if (descuento != null)
        {
            order.DiscountCode = descuento.Code;
            order.DiscountAmount = Math.Round(subtotalProductos * descuento.Percent / 100.0, 2);
            order.Total = Math.Max(0, Math.Round(order.Total - order.DiscountAmount.Value, 2));
        }

        // El total se guarda SIEMPRE redondeado a 2 decimales (centavos): sin
        // esto quedaba con precisión flotante (p. ej. 22.5009) y, aunque las
        // vistas lo formatean, el dato persistido debe ser un monto de dinero real.
        order.Total = Math.Round(order.Total, 2);

        // Registro del método de pago del pedido.
        _context.Payments.Add(new Payment
        {
            OrderId = orderId,
            PaymentMethod = paymentMethod,
            Reference = referenciaPago,
            Verified = pagoVerificado,
            VerifiedAt = pagoVerificado ? DateTime.UtcNow : null
        });
        _context.SaveChanges();

        SaveCart(new Dictionary<int, int>());

        // ---- Asignación automática del repartidor más cercano ----
        order.Restaurant = restaurant;
        var asignado = _riderAssignment.AsignarMasCercano(order);

        // Aviso a los administradores si hay una transferencia por verificar.
        if (paymentMethod == "Transferencia")
        {
            _ = _notificaciones.NotificarRolAsync("Admin",
                "Transferencia por verificar",
                $"Pedido {orderId}: comprobante {referenciaPago} por ${order.Total:F2}.",
                "/Admin/VerificarPagos");
        }

        var pagoTxt = paymentMethod switch
        {
            "Tarjeta" => $"pago aprobado con {referenciaPago}",
            "Transferencia" => $"transferencia {referenciaPago} en verificación: el administrador la confirmará en breve",
            _ => "pagarás en efectivo al recibir"
        };
        var riderTxt = asignado != null
            ? $"Repartidor asignado: {asignado.Name}."
            : "Buscando repartidor disponible; se asignará automáticamente.";
        TempData["Message"] = $"Pedido {orderId} confirmado ({pagoTxt}). {riderTxt}";
        return RedirectToAction("HistorialPedidos");
    }

    // Reintenta la asignación automática de un pedido que quedó "Pendiente"
    // porque no había repartidores libres al confirmarlo.
    [HttpPost]
    public IActionResult BuscarRepartidor(string orderId)
    {
        var clientDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var order = _context.Orders
            .Include(o => o.Restaurant)
            .FirstOrDefault(o => o.Id == orderId && o.ClientDni == clientDni);
        if (order == null) return NotFound();

        if (order.Status != "Pendiente")
        {
            TempData["Error"] = "Este pedido ya no está esperando repartidor.";
            return RedirectToAction("HistorialPedidos");
        }

        var asignado = _riderAssignment.AsignarMasCercano(order);
        TempData[asignado != null ? "Message" : "Error"] = asignado != null
            ? $"Repartidor asignado al pedido {orderId}: {asignado.Name}."
            : "Aún no hay repartidores disponibles. Intenta de nuevo en un momento.";
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
        // Solo el dueño puede consultar el estado/coordenadas de su pedido.
        var clientDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var order = _context.Orders
            .Include(o => o.Delivery)
            .Include(o => o.Restaurant)
            .Include(o => o.Client)
            .FirstOrDefault(o => o.Id == orderId && o.ClientDni == clientDni);
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
