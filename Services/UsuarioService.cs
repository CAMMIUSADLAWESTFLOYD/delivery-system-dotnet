using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using delivery_system_dotnet.Data;
using delivery_system_dotnet.Models;
using delivery_system_dotnet.Models.Enums;

namespace delivery_system_dotnet.Services;

public class UsuarioService : IUsuarioService
{
    private readonly ApplicationDbContext _context;
    private readonly ICodigoVerificacionService _otpService;

    public UsuarioService(ApplicationDbContext context, ICodigoVerificacionService otpService)
    {
        _context = context;
        _otpService = otpService;
    }

    public async Task<Person> RegistrarClienteAsync(Client cliente, string password)
    {
        await ValidarEmailUnicoAsync(cliente.Mail);
        await ValidarDniUnicoAsync(cliente.Dni);

        cliente.SetPassword(BCrypt.Net.BCrypt.HashPassword(password));
        cliente.RegisterDate = DateTime.UtcNow;
        cliente.DateOrder = DateTime.UtcNow;

        _context.Clients.Add(cliente);
        _context.Entry(cliente).Property<bool>("Activo").CurrentValue = true;
        await _context.SaveChangesAsync();
        return cliente;
    }

    public async Task<Person> RegistrarDeliveryAsync(Delivery delivery, string password)
    {
        await ValidarEmailUnicoAsync(delivery.Mail);
        await ValidarDniUnicoAsync(delivery.Dni);

        delivery.SetPassword(BCrypt.Net.BCrypt.HashPassword(password));
        delivery.Status = true;
        delivery.Rating = 0.0; // sin calificación hasta que un cliente lo puntúe

        _context.Deliveries.Add(delivery);
        // Aprobación del administrador: el repartidor (y su vehículo) queda
        // INACTIVO hasta que el admin lo apruebe en Gestión de Repartidores.
        _context.Entry(delivery).Property<bool>("Activo").CurrentValue = false;
        await _context.SaveChangesAsync();
        return delivery;
    }

    public async Task<Person> RegistrarAdminAsync(Admin admin, string password)
    {
        await ValidarEmailUnicoAsync(admin.Mail);
        await ValidarDniUnicoAsync(admin.Dni);

        admin.SetPassword(BCrypt.Net.BCrypt.HashPassword(password));
        admin.Rol = "Admin";

        _context.Admins.Add(admin);
        _context.Entry(admin).Property<bool>("Activo").CurrentValue = true;
        await _context.SaveChangesAsync();
        return admin;
    }

    public async Task<Person> AutenticarAsync(string email, string password)
    {
        var usuario = await _context.Persons
            .FirstOrDefaultAsync(u => u.Mail == email);

        if (usuario == null)
        {
            throw new UnauthorizedAccessException("Credenciales de inicio de sesión inválidas.");
        }

        bool isActive = _context.Entry(usuario).Property<bool>("Activo").CurrentValue;
        if (!isActive)
        {
            throw new InvalidOperationException("Esta cuenta ha sido bloqueada. Use la opción 'Desbloquear Cuenta' para reactivarla.");
        }

        bool validPassword = false;
        if (usuario is Admin admin)
        {
            validPassword = admin.ComparePassword(password);
        }
        else
        {
            validPassword = BCrypt.Net.BCrypt.Verify(password, usuario.GetPassword());
        }

        if (!validPassword)
        {
            throw new UnauthorizedAccessException("Credenciales de inicio de sesión inválidas.");
        }

        return usuario;
    }

    public async Task<Person?> ObtenerPorDniAsync(string dni)
    {
        return await _context.Persons.FirstOrDefaultAsync(u => u.Dni == dni);
    }

    public async Task<Person?> ObtenerPorEmailAsync(string email)
    {
        return await _context.Persons.FirstOrDefaultAsync(u => u.Mail == email);
    }

    public async Task<List<Person>> ObtenerTodosAsync()
    {
        return await _context.Persons.ToListAsync();
    }

    public async Task<List<Person>> ObtenerPorRolAsync(Rol rol)
    {
        return rol switch
        {
            Rol.Admin => await _context.Persons.OfType<Admin>().Cast<Person>().ToListAsync(),
            Rol.Client => await _context.Persons.OfType<Client>().Cast<Person>().ToListAsync(),
            Rol.Delivery => await _context.Persons.OfType<Delivery>().Cast<Person>().ToListAsync(),
            _ => new List<Person>()
        };
    }

    public async Task ActualizarClienteAsync(string dni, Client clienteModificado)
    {
        var clienteExistente = await _context.Clients.FindAsync(dni);
        if (clienteExistente == null)
        {
            throw new KeyNotFoundException("Cliente no encontrado.");
        }

        if (clienteExistente.Mail != clienteModificado.Mail)
        {
            await ValidarEmailUnicoAsync(clienteModificado.Mail);
            clienteExistente.Mail = clienteModificado.Mail;
        }

        clienteExistente.Name = clienteModificado.Name;
        clienteExistente.Phone = clienteModificado.Phone;
        clienteExistente.Address = clienteModificado.Address;
        clienteExistente.DateBirth = clienteModificado.DateBirth;
        clienteExistente.Gender = clienteModificado.Gender;
        clienteExistente.PayMethod = clienteModificado.PayMethod;

        await _context.SaveChangesAsync();
    }

    public async Task ActualizarDeliveryAsync(string dni, Delivery deliveryModificado)
    {
        var deliveryExistente = await _context.Deliveries.FindAsync(dni);
        if (deliveryExistente == null)
        {
            throw new KeyNotFoundException("Repartidor no encontrado.");
        }

        if (deliveryExistente.Mail != deliveryModificado.Mail)
        {
            await ValidarEmailUnicoAsync(deliveryModificado.Mail);
            deliveryExistente.Mail = deliveryModificado.Mail;
        }

        deliveryExistente.Name = deliveryModificado.Name;
        deliveryExistente.Phone = deliveryModificado.Phone;
        deliveryExistente.Address = deliveryModificado.Address;
        deliveryExistente.TypeVehicle = deliveryModificado.TypeVehicle;
        deliveryExistente.LicensePlate = deliveryModificado.LicensePlate;
        deliveryExistente.DateBirth = deliveryModificado.DateBirth;
        deliveryExistente.Gender = deliveryModificado.Gender;
        deliveryExistente.Status = deliveryModificado.Status;

        await _context.SaveChangesAsync();
    }

    public async Task ActualizarAdminAsync(string dni, Admin adminModificado)
    {
        var adminExistente = await _context.Admins.FindAsync(dni);
        if (adminExistente == null)
        {
            throw new KeyNotFoundException("Administrador no encontrado.");
        }

        if (adminExistente.Mail != adminModificado.Mail)
        {
            await ValidarEmailUnicoAsync(adminModificado.Mail);
            adminExistente.Mail = adminModificado.Mail;
        }

        adminExistente.Name = adminModificado.Name;
        adminExistente.Phone = adminModificado.Phone;
        adminExistente.Address = adminModificado.Address;
        adminExistente.Succursal = adminModificado.Succursal;
        adminExistente.Salary = adminModificado.Salary;
        adminExistente.DateBirth = adminModificado.DateBirth;
        adminExistente.Gender = adminModificado.Gender;

        await _context.SaveChangesAsync();
    }

    // Cambio de contraseña desde "Mi perfil": exige la contraseña actual
    // (a diferencia del flujo de recuperación, que exige un código OTP).
    public async Task CambiarPasswordPerfilAsync(string dni, string passwordActual, string passwordNueva)
    {
        var usuario = await _context.Persons.FindAsync(dni);
        if (usuario == null)
        {
            throw new KeyNotFoundException("Usuario no encontrado.");
        }

        bool valida;
        try { valida = BCrypt.Net.BCrypt.Verify(passwordActual, usuario.GetPassword()); }
        catch { valida = usuario.GetPassword() == passwordActual; }

        if (!valida)
        {
            throw new UnauthorizedAccessException("La contraseña actual no es correcta.");
        }
        if (string.IsNullOrEmpty(passwordNueva) || passwordNueva.Length < 6)
        {
            throw new ArgumentException("La nueva contraseña debe tener al menos 6 caracteres.");
        }

        usuario.SetPassword(BCrypt.Net.BCrypt.HashPassword(passwordNueva));
        await _context.SaveChangesAsync();
    }

    // RESETEO por el ADMINISTRADOR: no pide la contraseña anterior (el admin
    // actúa cuando el usuario la perdió). Genera una contraseña TEMPORAL, la
    // guarda hasheada y la devuelve en texto plano UNA sola vez para que el
    // administrador se la entregue al usuario, que luego la cambia en su perfil.
    // Nunca se guarda ni se vuelve a mostrar en texto plano.
    public async Task<string> ResetearPasswordAsync(string dni)
    {
        var usuario = await _context.Persons.FindAsync(dni);
        if (usuario == null)
        {
            throw new KeyNotFoundException("Usuario no encontrado.");
        }

        var temporal = GenerarPasswordTemporal();
        usuario.SetPassword(BCrypt.Net.BCrypt.HashPassword(temporal));
        await _context.SaveChangesAsync();
        return temporal;
    }

    // Contraseña temporal legible pero aleatoria (ej. "FB-7K2M9QX4"). Se excluyen
    // caracteres ambiguos (O/0, I/1) para que sea fácil de dictar por teléfono.
    private static string GenerarPasswordTemporal()
    {
        const string abc = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(8);
        var chars = bytes.Select(b => abc[b % abc.Length]).ToArray();
        return "FB-" + new string(chars);
    }

    // Guarda la foto de perfil en wwwroot/images/profiles y persiste la ruta.
    public async Task<string> GuardarFotoPerfilAsync(string dni, IFormFile foto, string webRootPath)
    {
        var usuario = await _context.Persons.FindAsync(dni);
        if (usuario == null)
        {
            throw new KeyNotFoundException("Usuario no encontrado.");
        }
        if (foto == null || foto.Length == 0)
        {
            throw new ArgumentException("Selecciona una imagen para subir.");
        }
        if (foto.Length > 2 * 1024 * 1024)
        {
            throw new ArgumentException("La imagen no puede superar los 2 MB.");
        }

        var ext = Path.GetExtension(foto.FileName).ToLowerInvariant();
        var permitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!permitidas.Contains(ext))
        {
            throw new ArgumentException("Formato no admitido. Usa JPG, PNG o WEBP.");
        }

        var carpeta = Path.Combine(webRootPath, "images", "profiles");
        Directory.CreateDirectory(carpeta);

        // Nombre de archivo derivado del DNI (sin caracteres inválidos).
        var nombreBase = string.Concat(dni.Where(char.IsLetterOrDigit));
        var archivo = nombreBase + ext;

        // Eliminar fotos anteriores del mismo usuario con otra extensión.
        foreach (var vieja in Directory.GetFiles(carpeta, nombreBase + ".*"))
        {
            try { File.Delete(vieja); } catch { /* en uso: se sobreescribe abajo */ }
        }

        var rutaFisica = Path.Combine(carpeta, archivo);
        using (var stream = new FileStream(rutaFisica, FileMode.Create))
        {
            await foto.CopyToAsync(stream);
        }

        // Sufijo de versión para invalidar la caché del navegador al cambiar la foto.
        usuario.ProfilePhotoUrl = "/images/profiles/" + archivo + "?v=" + DateTime.UtcNow.Ticks;
        await _context.SaveChangesAsync();
        return usuario.ProfilePhotoUrl;
    }

    public async Task EliminarAsync(string dni)
    {
        var usuario = await _context.Persons.FindAsync(dni);
        if (usuario == null)
        {
            throw new KeyNotFoundException("Usuario no encontrado.");
        }

        _context.Persons.Remove(usuario);
        await _context.SaveChangesAsync();
    }

    public async Task CambiarPasswordAsync(string email, string codigoOtp, string newPassword)
    {
        var usuario = await _context.Persons.FirstOrDefaultAsync(u => u.Mail == email);
        if (usuario == null)
        {
            throw new KeyNotFoundException("Usuario no encontrado.");
        }

        bool otpValido = await _otpService.ValidarCodigoOtpAsync(usuario, codigoOtp, TipoCodigo.Recuperacion);
        if (!otpValido)
        {
            throw new ArgumentException("El código de verificación OTP no es válido o ha expirado.");
        }

        usuario.SetPassword(BCrypt.Net.BCrypt.HashPassword(newPassword));
        await _context.SaveChangesAsync();
    }

    private async Task ValidarEmailUnicoAsync(string email)
    {
        bool existe = await _context.Persons.AnyAsync(u => u.Mail == email);
        if (existe)
        {
            throw new InvalidOperationException($"El correo electrónico '{email}' ya se encuentra registrado.");
        }
    }

    private async Task ValidarDniUnicoAsync(string dniVal)
    {
        bool existe = await _context.Persons.AnyAsync(u => u.Dni == dniVal);
        if (existe)
        {
            throw new InvalidOperationException($"El DNI '{dniVal}' ya se encuentra registrado.");
        }
    }

    public async Task DesbloquearCuentaAsync(string email)
    {
        var usuario = await _context.Persons.FirstOrDefaultAsync(u => u.Mail == email);
        if (usuario == null)
        {
            throw new KeyNotFoundException("Usuario no encontrado.");
        }

        // Las cuentas de repartidor las aprueba/suspende SOLO el administrador
        // (Gestión de Repartidores); el autodesbloqueo las saltaría.
        if (usuario is Delivery)
        {
            throw new InvalidOperationException(
                "Las cuentas de repartidor las activa un administrador. Contacta al soporte de FlashBite.");
        }

        _context.Entry(usuario).Property<bool>("Activo").CurrentValue = true;
        await _context.SaveChangesAsync();
    }
}
