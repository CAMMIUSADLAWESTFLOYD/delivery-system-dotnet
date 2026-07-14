using System.Collections.Generic;
using System.Threading.Tasks;
using delivery_system_dotnet.Models;
using delivery_system_dotnet.Models.Enums;

namespace delivery_system_dotnet.Services;

public interface IUsuarioService
{
    Task<Person> RegistrarClienteAsync(Client cliente, string password);
    Task<Person> RegistrarDeliveryAsync(Delivery delivery, string password);
    Task<Person> RegistrarAdminAsync(Admin admin, string password);
    
    Task<Person> AutenticarAsync(string email, string password);
    Task<Person?> ObtenerPorDniAsync(string dni);
    Task<Person?> ObtenerPorEmailAsync(string email);
    Task<List<Person>> ObtenerTodosAsync();
    Task<List<Person>> ObtenerPorRolAsync(Rol rol);
    
    Task ActualizarClienteAsync(string dni, Client clienteModificado);
    Task ActualizarDeliveryAsync(string dni, Delivery deliveryModificado);
    Task ActualizarAdminAsync(string dni, Admin adminModificado);
    
    Task EliminarAsync(string dni);
    Task CambiarPasswordAsync(string email, string codigoOtp, string newPassword);
    Task DesbloquearCuentaAsync(string email);

    // Perfil de usuario (Mi cuenta)
    Task CambiarPasswordPerfilAsync(string dni, string passwordActual, string passwordNueva);
    Task<string> GuardarFotoPerfilAsync(string dni, Microsoft.AspNetCore.Http.IFormFile foto, string webRootPath);
}
