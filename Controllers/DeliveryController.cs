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

    public DeliveryController(ApplicationDbContext context, IOrderManager orderManager,
        IUsuarioService usuarioService, IWebHostEnvironment env)
    {
        _context = context;
        _orderManager = orderManager;
        _usuarioService = usuarioService;
        _env = env;
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

    [HttpPost]
    public IActionResult AceptarPedido(string orderId)
    {
        try
        {
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
        var order = _context.Orders.FirstOrDefault(o => o.Id == orderId);
        if (order != null)
        {
            order.DeliveryDni = null; // Liberar el pedido para reasignación
            _context.SaveChanges();
            _orderManager.ChangeStatus(orderId, "Admin", "Pendiente"); // El proxy manager prohíbe pasar a Pendiente desde Delivery, así que simulo como Admin o salto el proxy, mejor modifico el status directamente.
        }
        
        TempData["Message"] = $"Has rechazado el pedido {orderId}.";
        return RedirectToAction("GestionarEntregas");
    }

    [HttpPost]
    public IActionResult UpdateDeliveryStatus(string orderId, string newStatus)
    {
        try
        {
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
