using System.Threading.Tasks;

namespace delivery_system_dotnet.Services;

public interface IEmailService
{
    Task EnviarCodigoOtpAsync(string destinatariolEmail, string nombreDestinatario, string codigoOtp, string tipoOperacion);
}
