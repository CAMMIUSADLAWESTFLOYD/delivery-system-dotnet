using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using delivery_system_dotnet.Models;
using delivery_system_dotnet.Models.Enums;
using delivery_system_dotnet.Services;

namespace delivery_system_dotnet.Controllers;

public class AuthController : Controller
{
    private readonly IUsuarioService _usuarioService;
    private readonly ICodigoVerificacionService _otpService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;

    public AuthController(
        IUsuarioService usuarioService,
        ICodigoVerificacionService otpService,
        IEmailService emailService,
        IConfiguration configuration,
        IWebHostEnvironment env)
    {
        _usuarioService = usuarioService;
        _otpService = otpService;
        _emailService = emailService;
        _configuration = configuration;
        _env = env;
    }

    // ¿Hay un servidor SMTP realmente configurado? (misma regla que EmailService)
    private bool SmtpConfigurado()
    {
        var server = !string.IsNullOrEmpty(_configuration["SmtpSettings:Server"])
            ? _configuration["SmtpSettings:Server"] : _configuration["Email:Host"];
        var user = !string.IsNullOrEmpty(_configuration["SmtpSettings:Username"])
            ? _configuration["SmtpSettings:Username"] : _configuration["Email:Username"];
        return !string.IsNullOrEmpty(server) && !string.IsNullOrEmpty(user);
    }

    [HttpGet]
    public IActionResult Login()
    {
        ViewBag.Error = Request.Query["error"].ToString();
        ViewBag.Info = Request.Query["info"].ToString();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    {
        try
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Debe ingresar el correo y la contraseña.";
                return View();
            }

            var usuario = await _usuarioService.AutenticarAsync(email, password);

            string userRole = usuario switch
            {
                Admin => "Admin",
                Client => "Client",
                Delivery => "Delivery",
                _ => "Client"
            };

            // Iniciar sesión del usuario usando Cookies de Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Dni),
                new Claim(ClaimTypes.Name, usuario.Name),
                new Claim(ClaimTypes.Email, usuario.Mail),
                new Claim(ClaimTypes.Role, userRole)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties { IsPersistent = true };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // Redirección según el Rol
            return userRole switch
            {
                "Admin" => RedirectToAction("Dashboard", "Admin"),
                "Client" => RedirectToAction("Dashboard", "Cliente"),
                "Delivery" => RedirectToAction("Dashboard", "Delivery"),
                _ => RedirectToAction("Login")
            };
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
            return View();
        }
    }

    [HttpGet]
    public IActionResult VerificarCodigo()
    {
        var pendingEmail = HttpContext.Session.GetString("Pending2faEmail");
        if (string.IsNullOrEmpty(pendingEmail))
        {
            return RedirectToAction("Login", new { error = "Sesión de inicio expirada o no encontrada." });
        }

        ViewBag.Email = pendingEmail;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> VerificarCodigo(string codigo)
    {
        var pendingEmail = HttpContext.Session.GetString("Pending2faEmail");
        if (string.IsNullOrEmpty(pendingEmail))
        {
            return RedirectToAction("Login", new { error = "Sesión de inicio expirada." });
        }

        try
        {
            if (string.IsNullOrEmpty(codigo))
            {
                ViewBag.Error = "El código OTP es requerido.";
                ViewBag.Email = pendingEmail;
                return View();
            }

            var usuario = await _usuarioService.ObtenerPorEmailAsync(pendingEmail);
            if (usuario == null)
            {
                return RedirectToAction("Login", new { error = "Usuario no encontrado." });
            }

            bool otpValido = await _otpService.ValidarCodigoOtpAsync(usuario, codigo, TipoCodigo.Login);
            if (!otpValido)
            {
                ViewBag.Error = "Código OTP inválido o expirado.";
                ViewBag.Email = pendingEmail;
                return View();
            }

            // Limpiar la sesión temporal
            HttpContext.Session.Remove("Pending2faEmail");

            string userRole = usuario switch
            {
                Admin => "Admin",
                Client => "Client",
                Delivery => "Delivery",
                _ => "Client"
            };

            // Iniciar sesión del usuario usando Cookies de Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Dni),
                new Claim(ClaimTypes.Name, usuario.Name),
                new Claim(ClaimTypes.Email, usuario.Mail),
                new Claim(ClaimTypes.Role, userRole)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties { IsPersistent = true };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // Redirección según el Rol
            return userRole switch
            {
                "Admin" => RedirectToAction("Dashboard", "Admin"),
                "Client" => RedirectToAction("Dashboard", "Cliente"),
                "Delivery" => RedirectToAction("Dashboard", "Delivery"),
                _ => RedirectToAction("Login")
            };
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
            ViewBag.Email = pendingEmail;
            return View();
        }
    }

    // Caso de uso "Resend code" (<<include>> Login): reenvía un nuevo OTP de 2FA
    // reutilizando el correo pendiente guardado en sesión durante el Login.
    [HttpGet]
    public async Task<IActionResult> ReenviarCodigo()
    {
        var pendingEmail = HttpContext.Session.GetString("Pending2faEmail");
        if (string.IsNullOrEmpty(pendingEmail))
        {
            return RedirectToAction("Login", new { error = "Sesión de inicio expirada. Vuelve a iniciar sesión." });
        }

        try
        {
            var usuario = await _usuarioService.ObtenerPorEmailAsync(pendingEmail);
            if (usuario == null)
            {
                return RedirectToAction("Login", new { error = "Usuario no encontrado." });
            }

            var otp = await _otpService.GenerarCodigoOtpAsync(usuario, TipoCodigo.Login);
            await _emailService.EnviarCodigoOtpAsync(usuario.Mail, usuario.Name, otp, "Reenvío de Código (2FA)");

            TempData["Info"] = "Se ha reenviado un nuevo código OTP a tu correo electrónico.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("VerificarCodigo");
    }

    [HttpGet]
    public IActionResult Registro(string? error)
    {
        ViewBag.Error = error;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> RegistroCliente(Client model, string password)
    {
        try
        {
            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                return RedirectToAction("Registro", new { error = "La contraseña debe tener al menos 6 caracteres." });
            }

            await _usuarioService.RegistrarClienteAsync(model, password);
            return RedirectToAction("Login", new { info = "Cliente registrado con éxito. Ya puedes iniciar sesión." });
        }
        catch (Exception ex)
        {
            return RedirectToAction("Registro", new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> RegistroDelivery(Delivery model, string password)
    {
        try
        {
            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                return RedirectToAction("Registro", new { error = "La contraseña debe tener al menos 6 caracteres." });
            }

            await _usuarioService.RegistrarDeliveryAsync(model, password);
            return RedirectToAction("Login", new { info = "Registro recibido. Un administrador debe aprobar tu vehículo antes de que puedas iniciar sesión." });
        }
        catch (Exception ex)
        {
            return RedirectToAction("Registro", new { error = ex.Message });
        }
    }

    // NOTA: El registro público de administradores fue eliminado a propósito.
    // Un administrador solo puede crearse desde el panel de administración
    // (AdminController.CrearAdministrador), nunca desde el formulario público.
    // El administrador inicial se crea mediante el seed de la base de datos.

    [HttpGet]
    public IActionResult RecuperarPassword()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> RecuperarPassword(string email)
    {
        try
        {
            if (string.IsNullOrEmpty(email))
            {
                ViewBag.Error = "Debe ingresar su correo electrónico.";
                return View();
            }

            var usuario = await _usuarioService.ObtenerPorEmailAsync(email);
            if (usuario == null)
            {
                ViewBag.Error = "No se encontró ningún usuario con ese correo.";
                return View();
            }

            var otp = await _otpService.GenerarCodigoOtpAsync(usuario, TipoCodigo.Recuperacion);
            await _emailService.EnviarCodigoOtpAsync(usuario.Mail, usuario.Name, otp, "Recuperación de Contraseña");

            // Sin SMTP configurado el correo NUNCA sale (EmailService solo lo
            // registra en consola), así que el código se muestra en pantalla para
            // que la recuperación sea utilizable. Al configurar SMTP real en
            // appsettings.json este aviso desaparece automáticamente.
            if (!SmtpConfigurado())
            {
                TempData["DevOtp"] = otp;
            }

            HttpContext.Session.SetString("RecoveryEmail", email);
            return RedirectToAction("RestablecerPassword");
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
            return View();
        }
    }

    [HttpGet]
    public IActionResult RestablecerPassword()
    {
        var recoveryEmail = HttpContext.Session.GetString("RecoveryEmail");
        if (string.IsNullOrEmpty(recoveryEmail))
        {
            return RedirectToAction("RecuperarPassword", new { error = "Sesión de recuperación expirada." });
        }

        ViewBag.Email = recoveryEmail;
        return View("CambiarPassword");
    }

    [HttpPost]
    public async Task<IActionResult> RestablecerPassword(string codigo, string newPassword)
    {
        var recoveryEmail = HttpContext.Session.GetString("RecoveryEmail");
        if (string.IsNullOrEmpty(recoveryEmail))
        {
            return RedirectToAction("RecuperarPassword", new { error = "Sesión de recuperación expirada." });
        }

        try
        {
            if (string.IsNullOrEmpty(codigo) || string.IsNullOrEmpty(newPassword))
            {
                ViewBag.Error = "Todos los campos son obligatorios.";
                ViewBag.Email = recoveryEmail;
                return View("CambiarPassword");
            }

            await _usuarioService.CambiarPasswordAsync(recoveryEmail, codigo, newPassword);
            HttpContext.Session.Remove("RecoveryEmail");

            return RedirectToAction("Login", new { info = "Contraseña restablecida con éxito. Inicia sesión con tus nuevas credenciales." });
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
            ViewBag.Email = recoveryEmail;
            return View("CambiarPassword");
        }
    }

    [HttpGet]
    public IActionResult DesbloquearCuenta()
    {
        ViewBag.Error = Request.Query["error"].ToString();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> DesbloquearCuenta(string email)
    {
        try
        {
            if (string.IsNullOrEmpty(email))
            {
                ViewBag.Error = "Debe ingresar su correo electrónico.";
                return View();
            }

            var usuario = await _usuarioService.ObtenerPorEmailAsync(email);
            if (usuario == null)
            {
                ViewBag.Error = "No se encontró ningún usuario con ese correo.";
                return View();
            }

            var otp = await _otpService.GenerarCodigoOtpAsync(usuario, TipoCodigo.Recuperacion);
            await _emailService.EnviarCodigoOtpAsync(usuario.Mail, usuario.Name, otp, "Desbloqueo de Cuenta");

            HttpContext.Session.SetString("UnlockEmail", email);
            return RedirectToAction("VerificarDesbloqueo");
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
            return View();
        }
    }

    [HttpGet]
    public IActionResult VerificarDesbloqueo()
    {
        var unlockEmail = HttpContext.Session.GetString("UnlockEmail");
        if (string.IsNullOrEmpty(unlockEmail))
        {
            return RedirectToAction("DesbloquearCuenta", new { error = "Sesión expirada." });
        }

        ViewBag.Email = unlockEmail;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> VerificarDesbloqueo(string codigo)
    {
        var unlockEmail = HttpContext.Session.GetString("UnlockEmail");
        if (string.IsNullOrEmpty(unlockEmail))
        {
            return RedirectToAction("DesbloquearCuenta", new { error = "Sesión expirada." });
        }

        try
        {
            if (string.IsNullOrEmpty(codigo))
            {
                ViewBag.Error = "El código es obligatorio.";
                ViewBag.Email = unlockEmail;
                return View();
            }

            var usuario = await _usuarioService.ObtenerPorEmailAsync(unlockEmail);
            if (usuario == null) return NotFound();

            bool otpValido = await _otpService.ValidarCodigoOtpAsync(usuario, codigo, TipoCodigo.Recuperacion);
            if (!otpValido)
            {
                ViewBag.Error = "Código OTP inválido o expirado.";
                ViewBag.Email = unlockEmail;
                return View();
            }

            await _usuarioService.DesbloquearCuentaAsync(unlockEmail);
            HttpContext.Session.Remove("UnlockEmail");

            return RedirectToAction("Login", new { info = "Cuenta desbloqueada con éxito. Ya puedes iniciar sesión." });
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
            ViewBag.Email = unlockEmail;
            return View();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
}
