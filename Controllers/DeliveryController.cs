using System;
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

[Authorize(Roles = "Delivery")]
public class DeliveryController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IOrderManager _orderManager;
    private readonly IUsuarioService _usuarioService;
    private readonly IWebHostEnvironment _env;
    private readonly IRiderAssignmentService _riderAssignment;
    private readonly INotificationService _notificaciones;

    public DeliveryController(ApplicationDbContext context, IOrderManager orderManager,
        IUsuarioService usuarioService, IWebHostEnvironment env,
        IRiderAssignmentService riderAssignment, INotificationService notificaciones)
    {
        _context = context;
        _orderManager = orderManager;
        _usuarioService = usuarioService;
        _env = env;
        _riderAssignment = riderAssignment;
        _notificaciones = notificaciones;
    }

    public IActionResult Dashboard()
    {
        var deliveryDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var repartidor = _context.Deliveries.FirstOrDefault(d => d.Dni == deliveryDni);
        if (repartidor == null) return NotFound();

        // Entregas activas (para la lista y el mapa del panel).
        ViewBag.Activas = _context.Orders
            .Include(o => o.Restaurant)
            .Include(o => o.Client)
            .Where(o => o.DeliveryDni == deliveryDni && o.Status != "Entregado" && o.Status != "Cancelado")
            .OrderBy(o => o.DateOrder)
            .ToList();

        // Resumen de ganancias (envíos de pedidos entregados).
        ViewBag.EntregasCompletadas = _context.Orders
            .Count(o => o.DeliveryDni == deliveryDni && o.Status == "Entregado");
        ViewBag.Ganancias = _context.Orders
            .Where(o => o.DeliveryDni == deliveryDni && o.Status == "Entregado")
            .Sum(o => (double?)(o.DeliveryFee ?? o.Distance * 1.5)) ?? 0;

        return View(repartidor);
    }

    // El repartidor no puede cancelar por sí mismo: SOLICITA la cancelación
    // y el administrador la resuelve en el monitor de pedidos (patrón State:
    // Shipped/Paid -> CancelRequested -> Cancelled | reanudar).
    [HttpPost]
    public IActionResult SolicitarCancelacion(string orderId, string motivo)
    {
        var deliveryDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var order = _context.Orders.FirstOrDefault(o => o.Id == orderId && o.DeliveryDni == deliveryDni);
        if (order == null) return NotFound();

        if (string.IsNullOrWhiteSpace(motivo))
        {
            TempData["Error"] = "Debes indicar el motivo de la cancelación.";
            return RedirectToAction("GestionarEntregas");
        }

        try
        {
            _orderManager.ChangeStatus(orderId, "Delivery", "CancelacionSolicitada");
            order.CancelRequestReason = motivo.Trim();
            _context.SaveChanges();

            _ = _notificaciones.NotificarRolAsync("Admin",
                "Solicitud de cancelación",
                $"El repartidor pide cancelar el pedido {orderId}: {motivo.Trim()}",
                "/Admin/MonitorearPedidos");

            TempData["Message"] = $"Solicitud de cancelación del pedido {orderId} enviada al administrador.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("GestionarEntregas");
    }

    // Reporte imprimible (guardar como PDF desde el navegador) del historial
    // de entregas con la ganancia por envío de cada una.
    [HttpGet]
    public IActionResult ReporteGanancias()
    {
        var deliveryDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var repartidor = _context.Deliveries.FirstOrDefault(d => d.Dni == deliveryDni);
        if (repartidor == null) return NotFound();

        var entregas = _context.Orders
            .Include(o => o.Restaurant)
            .Where(o => o.DeliveryDni == deliveryDni && o.Status == "Entregado")
            .OrderByDescending(o => o.DateOrder)
            .ToList();

        ViewBag.Repartidor = repartidor;
        return View("ReporteGanancias", entregas);
    }

    [HttpGet]
    public IActionResult GestionarEntregas()
    {
        var deliveryDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var deliveries = _context.Orders
            .Include(o => o.Restaurant)
            .Include(o => o.Client)
            .Where(o => o.DeliveryDni == deliveryDni && o.Status != "Entregado" && o.Status != "Cancelado")
            .ToList();
        return View("GestionarEntregas", deliveries);
    }

    // Mapa de la entrega para el repartidor: muestra el restaurante (recogida),
    // el destino del cliente y su propia posición en vivo sobre la ruta.
    [HttpGet]
    public IActionResult VerMapa(string orderId)
    {
        var deliveryDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var order = _context.Orders
            .Include(o => o.Restaurant)
            .Include(o => o.Client)
            .Include(o => o.Delivery)
            .FirstOrDefault(o => o.Id == orderId && o.DeliveryDni == deliveryDni);

        if (order == null) return NotFound();
        return View("VerMapa", order);
    }

    [HttpGet]
    public IActionResult TrackingStatus(string orderId)
    {
        var deliveryDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var order = _context.Orders
            .FirstOrDefault(o => o.Id == orderId && o.DeliveryDni == deliveryDni);
        if (order == null) return NotFound();

        var phase = order.Status switch
        {
            "Aceptado" => "to_restaurant",
            "Preparando" => "at_restaurant",
            "En Camino" => "to_client",
            "Entregado" => "delivered",
            _ => "idle"
        };

        return Json(new
        {
            order.Status,
            phase,
            currentLat = order.CurrentLatitude,
            currentLng = order.CurrentLongitude
        });
    }

    // IDs de los pedidos actualmente ASIGNADOS a este repartidor (activos).
    // Lo usan los paneles (Dashboard / GestionarEntregas) para detectar en vivo
    // cuando el sistema le asigna un pedido NUEVO y refrescar la lista solos,
    // sin depender de que el repartidor tuviera el navegador abierto justo en el
    // instante en que llegó el toast (que es efímero).
    [HttpGet]
    public IActionResult PedidosActivosIds()
    {
        var deliveryDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var ids = _context.Orders
            .Where(o => o.DeliveryDni == deliveryDni && o.Status != "Entregado" && o.Status != "Cancelado")
            .OrderBy(o => o.DateOrder)
            .Select(o => o.Id)
            .ToList();
        return Json(ids);
    }

    [HttpPost]
    public IActionResult AceptarPedido(string orderId, double? latitude, double? longitude)
    {
        try
        {
            var deliveryDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Solo el repartidor al que se le ASIGNÓ el pedido puede aceptarlo
            // (evita que un repartidor acepte el pedido de otro).
            var pedido = _context.Orders.FirstOrDefault(o => o.Id == orderId && o.DeliveryDni == deliveryDni);
            if (pedido == null)
            {
                TempData["Error"] = "Ese pedido no está asignado a ti.";
                return RedirectToAction("GestionarEntregas");
            }

            var delivery = _context.Deliveries.FirstOrDefault(d => d.Dni == deliveryDni);
            if (delivery != null && latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180
                && latitude != 0 && longitude != 0)
            {
                delivery.Latitude = latitude;
                delivery.Longitude = longitude;
                delivery.LastLocationUpdate = DateTime.UtcNow;
                _context.SaveChanges();
            }
            // Mundo real: un pago por transferencia debe estar VERIFICADO por el
            // administrador antes de que el pedido entre a reparto.
            var pago = _context.Payments.FirstOrDefault(p => p.OrderId == orderId);
            if (pago != null && pago.PaymentMethod == "Transferencia" && !pago.Verified)
            {
                TempData["Error"] = $"El pedido {orderId} se pagó por transferencia (comprobante {pago.Reference}) y el administrador aún no la verifica. Podrás aceptarlo cuando el pago esté aprobado.";
                return RedirectToAction("GestionarEntregas");
            }

            _orderManager.ChangeStatus(orderId, "Delivery", "Aceptado");
            TempData["Message"] = $"Pedido {orderId} aceptado con éxito.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("GestionarEntregas");
    }

    [HttpPost]
    public IActionResult RechazarPedido(string orderId)
    {
        var deliveryDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        // Solo el repartidor asignado puede rechazar el pedido (evita que un
        // repartidor libere/reasigne la entrega en curso de otro).
        var order = _context.Orders.FirstOrDefault(o => o.Id == orderId && o.DeliveryDni == deliveryDni);
        if (order == null)
        {
            TempData["Error"] = "Ese pedido no está asignado a ti.";
            return RedirectToAction("GestionarEntregas");
        }

        order.DeliveryDni = null;    // liberar el pedido
        order.Status = "Pendiente";  // vuelve a la cola de asignación
        _context.SaveChanges();

        // Reasignar al SIGUIENTE repartidor más cercano, excluyendo a quien
        // rechazó (para que no se le vuelva a asignar el mismo pedido).
        _riderAssignment.AsignarPendientes(excluirDni: deliveryDni);

        TempData["Message"] = $"Has rechazado el pedido {orderId}. Se reasignará a otro repartidor.";
        return RedirectToAction("GestionarEntregas");
    }

    [HttpPost]
    public IActionResult UpdateDeliveryStatus(string orderId, string newStatus)
    {
        try
        {
            var deliveryDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            // Solo el repartidor asignado puede actualizar el estado de la entrega.
            if (!_context.Orders.Any(o => o.Id == orderId && o.DeliveryDni == deliveryDni))
            {
                TempData["Error"] = "Ese pedido no está asignado a ti.";
                return RedirectToAction("GestionarEntregas");
            }

            // La salida del restaurante la controla la simulación después de
            // completar el primer trayecto y la espera de preparación.
            if (newStatus == "En Camino")
            {
                TempData["Error"] = "Debes completar primero el recorrido y la espera en el restaurante.";
                return RedirectToAction("GestionarEntregas");
            }

            _orderManager.ChangeStatus(orderId, "Delivery", newStatus);
            TempData["Message"] = $"Entrega {orderId} actualizada a '{newStatus}' con éxito.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("GestionarEntregas");
    }

    [HttpPost]
    public IActionResult ReportIssue(string orderId, string issueDescription)
    {
        try
        {
            var deliveryDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            // Solo el repartidor asignado puede reportar una incidencia del pedido.
            if (!_context.Orders.Any(o => o.Id == orderId && o.DeliveryDni == deliveryDni))
            {
                TempData["Error"] = "Ese pedido no está asignado a ti.";
                return RedirectToAction("GestionarEntregas");
            }

            _orderManager.ChangeStatus(orderId, "Delivery", "Reportado");
            TempData["Message"] = $"Incidente para el pedido {orderId} reportado: '{issueDescription}'.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("GestionarEntregas");
    }

    [HttpGet]
    public IActionResult HistorialRutas()
    {
        var deliveryDni = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var orders = _context.Orders
            .Include(o => o.Restaurant)
            .Where(o => o.DeliveryDni == deliveryDni && (o.Status == "Entregado" || o.Status == "Reportado"))
            .ToList();
        return View("HistorialRutas", orders);
    }

    // ============================================================
    //  Mi perfil / Configuración del repartidor
    // ============================================================
    private string? DniActual => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet]
    public async Task<IActionResult> Perfil()
    {
        var repartidor = await _usuarioService.ObtenerPorDniAsync(DniActual ?? "") as Delivery;
        if (repartidor == null) return NotFound();

        ViewBag.EntregasCompletadas = _context.Orders
            .Count(o => o.DeliveryDni == repartidor.Dni && o.Status == "Entregado");

        return View("Perfil", repartidor);
    }

    [HttpPost]
    public async Task<IActionResult> ActualizarPerfil(string name, string mail, string phone, string address, string typeVehicle, string licensePlate)
    {
        try
        {
            var actual = await _usuarioService.ObtenerPorDniAsync(DniActual ?? "") as Delivery;
            if (actual == null) return NotFound();

            var modificado = new Delivery
            {
                Name = name,
                Mail = mail,
                Phone = phone,
                Address = address,
                TypeVehicle = typeVehicle,
                LicensePlate = licensePlate,
                DateBirth = actual.DateBirth,
                Gender = actual.Gender,
                Status = actual.Status
            };
            await _usuarioService.ActualizarDeliveryAsync(actual.Dni, modificado);
            await RefrescarSesionAsync(actual.Dni, name, mail, "Delivery");

            TempData["Message"] = "Tus datos se guardaron correctamente.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction("Perfil");
    }

    // Estado del repartidor: Disponible (true) / Ocupado (false).
    [HttpPost]
    public IActionResult CambiarEstado(bool disponible)
    {
        var repartidor = _context.Deliveries.FirstOrDefault(d => d.Dni == DniActual);
        if (repartidor == null) return NotFound();

        repartidor.Status = disponible;
        _context.SaveChanges();

        // Al ponerse disponible, toma un pedido que estuviera esperando repartidor.
        if (disponible)
        {
            _riderAssignment.AsignarPendientes();
        }

        TempData["Message"] = disponible
            ? "Ahora apareces como disponible."
            : "Ahora apareces como ocupado.";
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
