using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
using delivery_system_dotnet.Interfaces;
using delivery_system_dotnet.Services;

namespace delivery_system_dotnet.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IOrderManager _orderManager;
    private readonly IUsuarioService _usuarioService;
    private readonly IDeliveryTrackingService _tracking;
    private readonly IWebHostEnvironment _env;
    private readonly INotificationService _notificaciones;

    public AdminController(ApplicationDbContext context, IOrderManager orderManager,
        IUsuarioService usuarioService, IDeliveryTrackingService tracking, IWebHostEnvironment env,
        INotificationService notificaciones)
    {
        _context = context;
        _orderManager = orderManager;
        _usuarioService = usuarioService;
        _tracking = tracking;
        _env = env;
        _notificaciones = notificaciones;
    }

    public IActionResult Dashboard()
    {
        return View();
    }

    [HttpGet]
    public IActionResult GestionarProductos()
    {
        var products = _context.Products.Include(p => p.Restaurant).ToList();
        return View("GestionarProductos", products);
    }


    // Guarda un archivo de imagen bajo wwwroot/images/{subcarpeta} y devuelve su ruta pública.
    private async Task<string> GuardarImagenAsync(IFormFile file, string subcarpeta)
    {
        var uploadsFolder = Path.Combine(_env.WebRootPath, "images", subcarpeta);
        Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }

        return $"/images/{subcarpeta}/{uniqueFileName}";
    }

    // Combos que necesita el formulario de producto (restaurantes y categorías existentes).
    private void CargarCombosProducto()
    {
        ViewBag.Restaurantes = _context.Restaurants
            .OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.Name })
            .ToList();
        ViewBag.Categorias = _context.Products
            .Where(p => p.Category != null && p.Category != "")
            .Select(p => p.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }

    [HttpGet]
    public IActionResult CreateProduct(int? restaurantId)
    {
        CargarCombosProducto();
        var model = new ProductImageViewModel();
        if (restaurantId.HasValue) model.Product.RestaurantId = restaurantId.Value;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct(ProductImageViewModel model)
    {
        if (!_context.Restaurants.Any(r => r.Id == model.Product.RestaurantId))
        {
            ModelState.AddModelError("Product.RestaurantId", "Selecciona el restaurante al que pertenece el producto.");
        }

        // Nombre único dentro del mismo restaurante (puede repetirse entre locales).
        if (_context.Products.Any(p => p.Name == model.Product.Name && p.RestaurantId == model.Product.RestaurantId))
        {
            ModelState.AddModelError("Product.Name", "Este restaurante ya tiene un producto con este nombre.");
        }

        if (ModelState.IsValid)
        {
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                model.Product.ImagePath = await GuardarImagenAsync(model.ImageFile, "products");
            }
            else if (!string.IsNullOrWhiteSpace(model.ImageUrl))
            {
                model.Product.ImagePath = model.ImageUrl.Trim();
            }

            _context.Products.Add(model.Product);
            await _context.SaveChangesAsync();
            TempData["Message"] = $"Producto \"{model.Product.Name}\" creado con éxito.";
            return RedirectToAction("GestionarProductos");
        }

        CargarCombosProducto();
        return View(model);
    }

    [HttpGet]
    public IActionResult EditProduct(int id)
    {
        var product = _context.Products.FirstOrDefault(p => p.Id == id);
        if (product == null) return NotFound();

        CargarCombosProducto();
        return View(new ProductImageViewModel { Product = product });
    }

    [HttpPost]
    public async Task<IActionResult> EditProduct(ProductImageViewModel model)
    {
        var productInDb = _context.Products.FirstOrDefault(p => p.Id == model.Product.Id);
        if (productInDb == null) return NotFound();

        // Permite renombrar, validando duplicados dentro del mismo restaurante.
        if (_context.Products.Any(p => p.Name == model.Product.Name
                                       && p.RestaurantId == productInDb.RestaurantId
                                       && p.Id != productInDb.Id))
        {
            ModelState.AddModelError("Product.Name", "Este restaurante ya tiene otro producto con este nombre.");
        }

        if (ModelState.IsValid)
        {
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                productInDb.ImagePath = await GuardarImagenAsync(model.ImageFile, "products");
            }
            else if (!string.IsNullOrWhiteSpace(model.ImageUrl))
            {
                productInDb.ImagePath = model.ImageUrl.Trim();
            }

            productInDb.Name = model.Product.Name;
            productInDb.Price = model.Product.Price;
            productInDb.Category = model.Product.Category;
            productInDb.Description = model.Product.Description;
            productInDb.Available = model.Product.Available;

            await _context.SaveChangesAsync();
            TempData["Message"] = $"Producto \"{productInDb.Name}\" actualizado con éxito.";
            return RedirectToAction("GestionarProductos");
        }

        CargarCombosProducto();
        return View(model);
    }

    // Devuelve al usuario a la vista desde la que envió el formulario,
    // con reserva a la acción indicada como respaldo.
    private IActionResult RedirectBack(string fallbackAction)
    {
        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrEmpty(referer)) return Redirect(referer);
        return RedirectToAction(fallbackAction);
    }


    [HttpPost]
    public IActionResult DeleteProduct(int id)
    {
        var product = _context.Products.FirstOrDefault(p => p.Id == id);
        if (product == null) return NotFound();

        _context.Products.Remove(product);
        _context.SaveChanges();

        TempData["Message"] = $"Producto \"{product.Name}\" eliminado.";
        return RedirectBack("GestionarProductos");
    }

    [HttpPost]
    public IActionResult AssignCategory(int id, string category)
    {
        var product = _context.Products.FirstOrDefault(p => p.Id == id);
        if (product == null) return NotFound();

        if (string.IsNullOrWhiteSpace(category))
        {
            TempData["Error"] = "La categoría no puede estar vacía.";
            return RedirectBack("GestionarProductos");
        }

        product.Category = category.Trim();
        _context.SaveChanges();

        TempData["Message"] = $"Categoría \"{product.Category}\" asignada a \"{product.Name}\".";
        return RedirectBack("GestionarProductos");
    }

    [HttpGet]
    public IActionResult GestionarCategorias()
    {
        var categories = new[] { "Hamburguesas", "Bebidas", "Postres", "Acompañamientos" }.ToList();
        return View("GestionarCategorias", categories);
    }

    [HttpPost]
    public IActionResult DeleteCategory(string category)
    {
        TempData["Message"] = $"Categoría '{category}' eliminada.";
        return RedirectToAction("GestionarCategorias");
    }

    [HttpPost]
    public IActionResult ReorderList(string orderData)
    {
        TempData["Message"] = "Orden de categorías actualizado con éxito.";
        return RedirectToAction("GestionarCategorias");
    }

    // ============================================================
    //  Gestión de Restaurantes (listar / crear / editar / activar)
    // ============================================================

    [HttpGet]
    public IActionResult GestionarRestaurantes()
    {
        var restaurantes = _context.Restaurants
            .Include(r => r.Products)
            .OrderBy(r => r.Name)
            .ToList();
        return View("GestionarRestaurantes", restaurantes);
    }

    [HttpGet]
    public IActionResult CreateRestaurant()
    {
        ViewData["FormTitle"] = "Nuevo restaurante";
        ViewData["FormAction"] = "CreateRestaurant";
        return View("RestaurantForm", new RestaurantFormViewModel
        {
            Restaurant = new Restaurant
            {
                IsActive = true,
                OpeningHour = new TimeSpan(9, 0, 0),
                ClosingHour = new TimeSpan(21, 0, 0),
                // Centro de Ibarra como punto de partida del mapa
                Latitude = 0.3517,
                Longitude = -78.1223
            }
        });
    }

    // Coordenadas plausibles para un local del sistema (Ecuador continental).
    private void ValidarCoordenadasRestaurante(Restaurant r)
    {
        if (r.Latitude < -5 || r.Latitude > 2 || r.Latitude == 0)
        {
            ModelState.AddModelError("Restaurant.Latitude", "Latitud fuera de rango: usa el mapa para fijar la ubicación (Ecuador ≈ -5 a 2).");
        }
        if (r.Longitude < -92 || r.Longitude > -75 || r.Longitude == 0)
        {
            ModelState.AddModelError("Restaurant.Longitude", "Longitud fuera de rango: usa el mapa para fijar la ubicación (Ecuador ≈ -92 a -75).");
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateRestaurant(RestaurantFormViewModel model)
    {
        if (_context.Restaurants.Any(r => r.Name == model.Restaurant.Name))
        {
            ModelState.AddModelError("Restaurant.Name", "Ya existe un restaurante con este nombre.");
        }
        ValidarCoordenadasRestaurante(model.Restaurant);

        if (ModelState.IsValid)
        {
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                model.Restaurant.ImagePath = await GuardarImagenAsync(model.ImageFile, "restaurants");
            }
            else if (!string.IsNullOrWhiteSpace(model.ImageUrl))
            {
                model.Restaurant.ImagePath = model.ImageUrl.Trim();
            }

            _context.Restaurants.Add(model.Restaurant);
            await _context.SaveChangesAsync();
            TempData["Message"] = $"Restaurante \"{model.Restaurant.Name}\" creado con éxito.";
            return RedirectToAction("GestionarRestaurantes");
        }

        ViewData["FormTitle"] = "Nuevo restaurante";
        ViewData["FormAction"] = "CreateRestaurant";
        return View("RestaurantForm", model);
    }

    [HttpGet]
    public IActionResult EditRestaurant(int id)
    {
        var restaurante = _context.Restaurants.Find(id);
        if (restaurante == null) return NotFound();

        ViewData["FormTitle"] = $"Editar: {restaurante.Name}";
        ViewData["FormAction"] = "EditRestaurant";
        return View("RestaurantForm", new RestaurantFormViewModel { Restaurant = restaurante });
    }

    [HttpPost]
    public async Task<IActionResult> EditRestaurant(RestaurantFormViewModel model)
    {
        var enDb = _context.Restaurants.Find(model.Restaurant.Id);
        if (enDb == null) return NotFound();

        if (_context.Restaurants.Any(r => r.Name == model.Restaurant.Name && r.Id != model.Restaurant.Id))
        {
            ModelState.AddModelError("Restaurant.Name", "Ya existe otro restaurante con este nombre.");
        }
        ValidarCoordenadasRestaurante(model.Restaurant);

        if (ModelState.IsValid)
        {
            enDb.Name = model.Restaurant.Name;
            enDb.Description = model.Restaurant.Description;
            enDb.Address = model.Restaurant.Address;
            enDb.Phone = model.Restaurant.Phone;
            enDb.Category = model.Restaurant.Category;
            enDb.Latitude = model.Restaurant.Latitude;
            enDb.Longitude = model.Restaurant.Longitude;
            enDb.OpeningHour = model.Restaurant.OpeningHour;
            enDb.ClosingHour = model.Restaurant.ClosingHour;
            enDb.IsActive = model.Restaurant.IsActive;

            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                enDb.ImagePath = await GuardarImagenAsync(model.ImageFile, "restaurants");
            }
            else if (!string.IsNullOrWhiteSpace(model.ImageUrl))
            {
                enDb.ImagePath = model.ImageUrl.Trim();
            }

            await _context.SaveChangesAsync();
            TempData["Message"] = $"Restaurante \"{enDb.Name}\" actualizado.";
            return RedirectToAction("GestionarRestaurantes");
        }

        ViewData["FormTitle"] = $"Editar: {model.Restaurant.Name}";
        ViewData["FormAction"] = "EditRestaurant";
        return View("RestaurantForm", model);
    }

    // Activa o desactiva el restaurante (no se elimina: los pedidos históricos lo referencian).
    [HttpPost]
    public IActionResult ToggleRestaurante(int id)
    {
        var restaurante = _context.Restaurants.Find(id);
        if (restaurante == null) return NotFound();

        restaurante.IsActive = !restaurante.IsActive;
        _context.SaveChanges();
        TempData["Message"] = restaurante.IsActive
            ? $"\"{restaurante.Name}\" activado: vuelve a ser visible para los clientes."
            : $"\"{restaurante.Name}\" desactivado: dejará de mostrarse a los clientes.";
        return RedirectToAction("GestionarRestaurantes");
    }

    [HttpGet]
    public IActionResult MonitorearPedidos()
    {
        var orders = _context.Orders
            .Include(o => o.Restaurant)
            .Include(o => o.Client)
            .Include(o => o.Delivery)
            .ToList();

        // Repartidores disponibles para asignación (paso del flujo de entrega).
        ViewBag.Repartidores = _context.Deliveries.ToList();

        return View("MonitorearPedidos", orders);
    }

    // Flujo de entrega: el administrador acepta el pedido y asigna un repartidor.
    // Si el pedido ya está "Aceptado", el repartidor inicia su recorrido real
    // hacia el restaurante (notificado por SignalR).
    [HttpPost]
    public IActionResult AsignarRepartidor(string orderId, string deliveryDni)
    {
        var order = _context.Orders.FirstOrDefault(o => o.Id == orderId);
        if (order == null) return NotFound();

        var rider = _context.Deliveries.FirstOrDefault(d => d.Dni == deliveryDni);
        if (rider == null)
        {
            TempData["Error"] = "Repartidor no encontrado.";
            return RedirectToAction("MonitorearPedidos");
        }

        order.DeliveryDni = rider.Dni;
        _context.SaveChanges();

        _tracking.OnRiderAssigned(order.Id);

        TempData["Message"] = $"Repartidor {rider.Name} asignado al pedido {orderId}.";
        return RedirectToAction("MonitorearPedidos");
    }

    [HttpPost]
    public IActionResult UpdateOrderStatus(string orderId, string newStatus)
    {
        try
        {
            _orderManager.ChangeStatus(orderId, "Admin", newStatus);
            TempData["Message"] = $"Estado del pedido {orderId} actualizado a '{newStatus}'.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("MonitorearPedidos");
    }

    [HttpPost]
    public IActionResult CancelOrderAdmin(string orderId)
    {
        try
        {
            _orderManager.ChangeStatus(orderId, "Admin", "Cancelado");
            TempData["Message"] = $"El pedido {orderId} fue cancelado por el administrador.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("MonitorearPedidos");
    }

    [HttpGet]
    public IActionResult GenerarReporte()
    {
        ViewBag.TotalOrders = _context.Orders.Count();
        ViewBag.ActiveOrders = _context.Orders.Count(o => o.Status != "Entregado" && o.Status != "Cancelado");
        ViewBag.CompletedOrders = _context.Orders.Count(o => o.Status == "Entregado");
        ViewBag.Revenue = _context.Orders.Where(o => o.Status == "Entregado").Sum(o => o.Total);

        return View("GenerarReporte");
    }

    // ============================================================
    //  Reporte individual de un pedido (imprimible / exportable a PDF
    //  desde el navegador) con el detalle de items y el desglose de pago.
    // ============================================================
    [HttpGet]
    public IActionResult ReportePedido(string orderId)
    {
        var order = _context.Orders
            .Include(o => o.Restaurant)
            .Include(o => o.Client)
            .Include(o => o.Delivery)
            .Include(o => o.Payment)
            .Include(o => o.OrderDetails)
                .ThenInclude(d => d.Products)
            .FirstOrDefault(o => o.Id == orderId);

        if (order == null) return NotFound();
        return View("ReportePedido", order);
    }

    // Resuelve una solicitud de cancelación del repartidor: el admin cancela
    // definitivamente o reanuda la entrega (vuelve a "En Camino").
    [HttpPost]
    public IActionResult ResolverCancelacion(string orderId, bool cancelar)
    {
        try
        {
            var deliveryDni = _context.Orders.Where(o => o.Id == orderId).Select(o => o.DeliveryDni).FirstOrDefault();
            _orderManager.ChangeStatus(orderId, "Admin", cancelar ? "Cancelado" : "En Camino");

            // Avisar al repartidor de la decisión del admin. (Al cliente ya lo
            // notifica RealOrderManager según el nuevo estado.)
            if (deliveryDni != null)
            {
                _ = _notificaciones.NotificarUsuarioAsync(deliveryDni,
                    cancelar ? "Cancelación aprobada" : "Continúa la entrega",
                    cancelar
                        ? $"El administrador canceló el pedido {orderId}."
                        : $"El administrador rechazó la cancelación: continúa la entrega del pedido {orderId}.",
                    "/Delivery/GestionarEntregas");
            }

            TempData["Message"] = cancelar
                ? $"El pedido {orderId} fue cancelado a solicitud del repartidor."
                : $"La entrega del pedido {orderId} se reanudó.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction("MonitorearPedidos");
    }

    // ============================================================
    //  Verificación de pagos por transferencia: el cliente reporta el
    //  número de comprobante en el checkout y el administrador lo
    //  aprueba (habilita el reparto) o lo rechaza (cancela el pedido).
    // ============================================================
    [HttpGet]
    public IActionResult VerificarPagos()
    {
        var pendientes = _context.Payments
            .Include(p => p.Order)!.ThenInclude(o => o!.Client)
            .Include(p => p.Order)!.ThenInclude(o => o!.Restaurant)
            .Where(p => p.PaymentMethod == "Transferencia" && !p.Verified
                        && p.Order!.Status != "Cancelado")
            .OrderBy(p => p.Order!.DateOrder)
            .ToList();

        // Historial reciente de transferencias ya resueltas (contexto útil).
        ViewBag.Verificadas = _context.Payments
            .Include(p => p.Order)
            .Where(p => p.PaymentMethod == "Transferencia" && p.Verified)
            .OrderByDescending(p => p.VerifiedAt)
            .Take(10)
            .ToList();

        // Reembolsos pendientes: pedidos YA pagados (tarjeta o transferencia
        // verificada) que fueron CANCELADOS y aún no se han reembolsado.
        ViewBag.ReembolsosPendientes = _context.Payments
            .Include(p => p.Order)!.ThenInclude(o => o!.Client)
            .Where(p => p.PaymentMethod != "Efectivo" && p.Verified && !p.Refunded
                        && p.Order!.Status == "Cancelado")
            .OrderBy(p => p.Order!.DateOrder)
            .ToList();

        return View("VerificarPagos", pendientes);
    }

    // Marca un pago cancelado como reembolsado al cliente.
    [HttpPost]
    public IActionResult MarcarReembolso(string orderId)
    {
        var pago = _context.Payments
            .Include(p => p.Order)
            .FirstOrDefault(p => p.OrderId == orderId);
        if (pago?.Order == null) return NotFound();

        if (pago.Refunded)
        {
            TempData["Error"] = $"El pedido {orderId} ya fue reembolsado.";
            return RedirectToAction("VerificarPagos");
        }

        pago.Refunded = true;
        pago.RefundedAt = DateTime.UtcNow;
        pago.Order.PaymentStatus = false; // el dinero ya no está en poder del negocio
        _context.SaveChanges();

        if (pago.Order.ClientDni != null)
        {
            _ = _notificaciones.NotificarUsuarioAsync(pago.Order.ClientDni,
                "Reembolso procesado",
                $"Se reembolsó ${pago.Order.Total:F2} de tu pedido cancelado {orderId}.",
                "/Cliente/HistorialPedidos");
        }

        TempData["Message"] = $"Reembolso del pedido {orderId} (${pago.Order.Total:F2}) marcado como procesado.";
        return RedirectToAction("VerificarPagos");
    }

    [HttpPost]
    public IActionResult ResolverPago(string orderId, bool aprobar)
    {
        var pago = _context.Payments
            .Include(p => p.Order)
            .FirstOrDefault(p => p.OrderId == orderId && p.PaymentMethod == "Transferencia");
        if (pago?.Order == null) return NotFound();

        if (aprobar)
        {
            pago.Verified = true;
            pago.VerifiedAt = DateTime.UtcNow;
            pago.Order.PaymentStatus = true;
            _context.SaveChanges();

            if (pago.Order.ClientDni != null)
            {
                _ = _notificaciones.NotificarUsuarioAsync(pago.Order.ClientDni,
                    "Pago verificado",
                    $"Tu transferencia del pedido {orderId} fue confirmada. ¡Ya está en preparación!",
                    "/Cliente/HistorialPedidos");
            }
            TempData["Message"] = $"Transferencia {pago.Reference} del pedido {orderId} verificada: el pedido queda habilitado para reparto.";
        }
        else
        {
            try
            {
                var clientDni = pago.Order.ClientDni;
                _orderManager.ChangeStatus(orderId, "Admin", "Cancelado");
                if (clientDni != null)
                {
                    _ = _notificaciones.NotificarUsuarioAsync(clientDni,
                        "Pago rechazado",
                        $"No pudimos verificar tu transferencia del pedido {orderId}; el pedido fue cancelado.",
                        "/Cliente/HistorialPedidos");
                }
                TempData["Message"] = $"Transferencia {pago.Reference} rechazada: el pedido {orderId} fue cancelado.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
        }

        return RedirectToAction("VerificarPagos");
    }

    // ============================================================
    //  Gestión de repartidores: aprobación de registros y suspensión.
    //  Un repartidor recién registrado queda INACTIVO hasta que un
    //  administrador lo apruebe aquí.
    // ============================================================
    [HttpGet]
    public IActionResult GestionarRepartidores()
    {
        var repartidores = _context.Deliveries
            .Select(d => new RepartidorItemViewModel
            {
                Delivery = d,
                Activo = EF.Property<bool>(d, "Activo"),
                EntregasCompletadas = _context.Orders.Count(o => o.DeliveryDni == d.Dni && o.Status == "Entregado"),
                Ganancias = _context.Orders
                    .Where(o => o.DeliveryDni == d.Dni && o.Status == "Entregado")
                    .Sum(o => o.DeliveryFee ?? o.Distance * 1.5)
            })
            .ToList();

        return View("GestionarRepartidores", repartidores);
    }

    [HttpPost]
    public IActionResult CambiarEstadoRepartidor(string dni, bool activar)
    {
        var repartidor = _context.Deliveries.FirstOrDefault(d => d.Dni == dni);
        if (repartidor == null) return NotFound();

        _context.Entry(repartidor).Property<bool>("Activo").CurrentValue = activar;
        _context.SaveChanges();

        TempData["Message"] = activar
            ? $"Repartidor '{repartidor.Name}' aprobado: ya puede iniciar sesión y recibir pedidos."
            : $"Repartidor '{repartidor.Name}' suspendido: no podrá iniciar sesión.";
        return RedirectToAction("GestionarRepartidores");
    }

    // ============================================================
    //  Gestión de clientes: listado con actividad y suspensión de cuentas.
    // ============================================================
    [HttpGet]
    public IActionResult GestionarClientes()
    {
        var clientes = _context.Clients
            .Select(c => new ClienteItemViewModel
            {
                Client = c,
                Activo = EF.Property<bool>(c, "Activo"),
                Pedidos = _context.Orders.Count(o => o.ClientDni == c.Dni),
                TotalGastado = _context.Orders
                    .Where(o => o.ClientDni == c.Dni && o.Status == "Entregado")
                    .Sum(o => (double?)o.Total) ?? 0
            })
            .ToList();

        return View("GestionarClientes", clientes);
    }

    [HttpPost]
    public IActionResult CambiarEstadoCliente(string dni, bool activar)
    {
        var cliente = _context.Clients.FirstOrDefault(c => c.Dni == dni);
        if (cliente == null) return NotFound();

        _context.Entry(cliente).Property<bool>("Activo").CurrentValue = activar;
        _context.SaveChanges();

        TempData["Message"] = activar
            ? $"Cliente '{cliente.Name}' activado."
            : $"Cliente '{cliente.Name}' suspendido.";
        return RedirectToAction("GestionarClientes");
    }

    // ============================================================
    //  RESETEO DE CONTRASEÑA por el administrador (clientes y repartidores).
    //  Caso real: el usuario perdió su contraseña y no puede recuperarla por
    //  correo. El admin genera una TEMPORAL, se la entrega, y el usuario la
    //  cambia luego desde "Mi perfil". La contraseña se muestra UNA sola vez:
    //  en la base de datos solo queda su hash (BCrypt).
    // ============================================================
    [HttpPost]
    public async Task<IActionResult> ResetearPassword(string dni)
    {
        var usuario = await _usuarioService.ObtenerPorDniAsync(dni);

        // Solo clientes y repartidores: las cuentas de administrador se
        // gestionan en su propia pantalla.
        var esCliente = usuario is Client;
        var esRepartidor = usuario is Delivery;
        var destino = esRepartidor ? "GestionarRepartidores" : "GestionarClientes";

        if (usuario == null || (!esCliente && !esRepartidor))
        {
            TempData["Error"] = "Solo se puede resetear la contraseña de clientes y repartidores.";
            return RedirectToAction(destino);
        }

        try
        {
            var temporal = await _usuarioService.ResetearPasswordAsync(dni);
            TempData["Message"] = $"Contraseña de '{usuario.Name}' reseteada. Contraseña temporal: {temporal} — entrégasela y pídele que la cambie desde su perfil.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(destino);
    }

    // ============================================================
    //  Gestión de administradores (SOLO desde el panel de admin).
    //  Reutiliza IUsuarioService para no duplicar la lógica de
    //  registro/actualización ni el sistema de autenticación.
    // ============================================================
    [HttpGet]
    public IActionResult GestionarAdministradores()
    {
        var admins = _context.Admins
            .Select(a => new AdminItemViewModel
            {
                Admin = a,
                Activo = EF.Property<bool>(a, "Activo")
            })
            .ToList();

        return View("GestionarAdministradores", admins);
    }

    [HttpPost]
    public async Task<IActionResult> CrearAdministrador(Admin model, string password)
    {
        try
        {
            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                TempData["Error"] = "La contraseña debe tener al menos 6 caracteres.";
                return RedirectToAction("GestionarAdministradores");
            }

            model.Rol = string.IsNullOrEmpty(model.Rol) ? "Admin" : model.Rol;
            await _usuarioService.RegistrarAdminAsync(model, password);
            TempData["Message"] = $"Administrador '{model.Name}' creado con éxito.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("GestionarAdministradores");
    }

    [HttpPost]
    public async Task<IActionResult> EditarAdministrador(string dni, Admin model)
    {
        try
        {
            await _usuarioService.ActualizarAdminAsync(dni, model);
            TempData["Message"] = $"Administrador '{model.Name}' actualizado con éxito.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("GestionarAdministradores");
    }

    [HttpPost]
    public IActionResult CambiarEstadoAdministrador(string dni, bool activar)
    {
        var admin = _context.Admins.FirstOrDefault(a => a.Dni == dni);
        if (admin == null) return NotFound();

        // No permitir que un administrador se desactive a sí mismo.
        var currentDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!activar && dni == currentDni)
        {
            TempData["Error"] = "No puedes desactivar tu propia cuenta de administrador.";
            return RedirectToAction("GestionarAdministradores");
        }

        // No permitir desactivar el último administrador activo.
        if (!activar)
        {
            int activos = _context.Admins.Count(a => EF.Property<bool>(a, "Activo"));
            if (activos <= 1)
            {
                TempData["Error"] = "Debe existir al menos un administrador activo en el sistema.";
                return RedirectToAction("GestionarAdministradores");
            }
        }

        _context.Entry(admin).Property<bool>("Activo").CurrentValue = activar;
        _context.SaveChanges();

        TempData["Message"] = activar
            ? $"Administrador '{admin.Name}' activado."
            : $"Administrador '{admin.Name}' desactivado.";
        return RedirectToAction("GestionarAdministradores");
    }

    // ============================================================
    //  Mi perfil / Configuración del administrador
    // ============================================================
    private string? DniActual => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet]
    public async Task<IActionResult> Perfil()
    {
        var admin = await _usuarioService.ObtenerPorDniAsync(DniActual ?? "") as Admin;
        if (admin == null) return NotFound();
        return View("Perfil", admin);
    }

    [HttpPost]
    public async Task<IActionResult> ActualizarPerfil(string name, string mail, string phone, string address, string succursal)
    {
        try
        {
            var actual = await _usuarioService.ObtenerPorDniAsync(DniActual ?? "") as Admin;
            if (actual == null) return NotFound();

            var modificado = new Admin
            {
                Name = name,
                Mail = mail,
                Phone = phone,
                Address = address,
                Succursal = succursal,
                Salary = actual.Salary,
                DateBirth = actual.DateBirth,
                Gender = actual.Gender,
                Rol = actual.Rol
            };
            await _usuarioService.ActualizarAdminAsync(actual.Dni, modificado);
            await RefrescarSesionAsync(actual.Dni, name, mail, "Admin");

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

    // Reemite la cookie de autenticación para que la barra de navegación
    // muestre el nombre y correo actualizados.
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
