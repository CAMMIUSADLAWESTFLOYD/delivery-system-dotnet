using System.Threading.Tasks;
using delivery_system_dotnet.Models;
using delivery_system_dotnet.Models.Enums;

namespace delivery_system_dotnet.Services;

public interface ICodigoVerificacionService
{
    Task<string> GenerarCodigoOtpAsync(Person usuario, TipoCodigo tipo);
    Task<bool> ValidarCodigoOtpAsync(Person usuario, string codigo, TipoCodigo tipo);
}
