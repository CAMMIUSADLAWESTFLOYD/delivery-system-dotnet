using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace delivery_system_dotnet.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task EnviarCodigoOtpAsync(string destinatarioEmail, string nombreDestinatario, string codigoOtp, string tipoOperacion)
    {
        // Soporta tanto la nomenclatura de SmtpSettings como el fallback de Email (especialmente para secretos de usuario)
        var server = !string.IsNullOrEmpty(_configuration["SmtpSettings:Server"]) ? _configuration["SmtpSettings:Server"] : _configuration["Email:Host"];
        var portStr = !string.IsNullOrEmpty(_configuration["SmtpSettings:Port"]) ? _configuration["SmtpSettings:Port"] : _configuration["Email:Port"];
        var senderName = !string.IsNullOrEmpty(_configuration["SmtpSettings:SenderName"]) ? _configuration["SmtpSettings:SenderName"] : "FlashBite Delivery";
        var senderEmail = !string.IsNullOrEmpty(_configuration["SmtpSettings:SenderEmail"]) ? _configuration["SmtpSettings:SenderEmail"] : _configuration["Email:Username"];
        var username = !string.IsNullOrEmpty(_configuration["SmtpSettings:Username"]) ? _configuration["SmtpSettings:Username"] : _configuration["Email:Username"];
        var password = !string.IsNullOrEmpty(_configuration["SmtpSettings:Password"]) ? _configuration["SmtpSettings:Password"] : _configuration["Email:Password"];
        var enableSslStr = !string.IsNullOrEmpty(_configuration["SmtpSettings:EnableSsl"]) ? _configuration["SmtpSettings:EnableSsl"] : (_configuration["Email:EnableSsl"] ?? "true");

        // Log de depuración para ver qué valores de configuración SMTP se leyeron exactamente
        Console.WriteLine($"\n--- [SMTP DEBUG] Intentando envío de OTP para {destinatarioEmail} ({tipoOperacion}) ---");
        Console.WriteLine($"Servidor (Host): '{(string.IsNullOrEmpty(server) ? "VACÍO / NO DETECTADO" : server)}'");
        Console.WriteLine($"Puerto (Port): '{(string.IsNullOrEmpty(portStr) ? "VACÍO / POR DEFECTO (587)" : portStr)}'");
        Console.WriteLine($"Correo Emisor (Sender): '{(string.IsNullOrEmpty(senderEmail) ? "VACÍO / NO DETECTADO" : senderEmail)}'");
        Console.WriteLine($"Usuario (Username): '{(string.IsNullOrEmpty(username) ? "VACÍO / NO DETECTADO" : username)}'");
        Console.WriteLine($"Contraseña (Password): '{(string.IsNullOrEmpty(password) ? "VACÍO / NO DETECTADA" : "CONFIGURADA (" + password.Length + " caracteres)")}'");
        Console.WriteLine($"-------------------------------------------------------------------------\n");

        // Si no está configurado el servidor SMTP, registrar el OTP en consola y saltar
        if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("==========================================================================");
            _logger.LogWarning("SMTP NO CONFIGURADO TOTALMENTE. Por favor configure user-secrets o appsettings.json.");
            _logger.LogWarning($"CÓDIGO OTP ({tipoOperacion}) PARA {nombreDestinatario} ({destinatarioEmail}): [ {codigoOtp} ]");
            _logger.LogWarning("==========================================================================");
            Console.WriteLine($"\n>>> [DEVELOPMENT ONLY] OTP Code for {destinatarioEmail} ({tipoOperacion}): {codigoOtp} <<<\n");
            return;
        }

        int.TryParse(portStr, out int port);
        if (port == 0) port = 587;
        bool.TryParse(enableSslStr, out bool enableSsl);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, senderEmail));
        message.To.Add(new MailboxAddress(nombreDestinatario, destinatarioEmail));
        message.Subject = $"Código de Verificación FlashBite - {tipoOperacion}";

        // Crear plantilla HTML en blanco y rojo (FlashBite style)
        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = $@"
            <div style=""font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 8px; overflow: hidden;"">
                <div style=""background-color: #d90429; padding: 20px; text-align: center;"">
                    <h1 style=""color: #ffffff; margin: 0; font-size: 28px; letter-spacing: 1px;"">FlashBite</h1>
                </div>
                <div style=""padding: 30px; background-color: #ffffff; color: #2b2d42;"">
                    <h2 style=""color: #d90429; margin-top: 0;"">Código de Verificación ({tipoOperacion})</h2>
                    <p style=""font-size: 16px; line-height: 1.5;"">Hola, <strong>{nombreDestinatario}</strong>.</p>
                    <p style=""font-size: 16px; line-height: 1.5;"">Has solicitado un código OTP para continuar con tu proceso en el sistema de Delivery FlashBite.</p>
                    <div style=""text-align: center; margin: 30px 0;"">
                        <div style=""display: inline-block; background-color: #f8f9fa; border: 2px dashed #d90429; border-radius: 6px; padding: 15px 30px; font-size: 32px; font-weight: bold; letter-spacing: 4px; color: #d90429;"">
                            {codigoOtp}
                        </div>
                    </div>
                    <p style=""font-size: 14px; color: #8d99ae; line-height: 1.5;"">Este código expirará en 10 minutos y puede usarse una sola vez. Si no solicitaste este código, por favor ignora este correo.</p>
                </div>
                <div style=""background-color: #f8f9fa; padding: 15px; text-align: center; font-size: 12px; color: #8d99ae; border-top: 1px solid #e0e0e0;"">
                    Este es un correo automático. Por favor no respondas a este mensaje.<br>
                    &copy; 2026 FlashBite Delivery System. Todos los derechos reservados.
                </div>
            </div>"
        };

        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            // Usar SecureSocketOptions adecuado
            var secureSocketOption = enableSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            if (port == 587) secureSocketOption = SecureSocketOptions.StartTls;

            await client.ConnectAsync(server, port, secureSocketOption);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            
            _logger.LogInformation($"Correo enviado correctamente a {destinatarioEmail} con el código OTP.");
            Console.WriteLine($"\n>>> [SMTP SUCCESS] Correo OTP enviado exitosamente a {destinatarioEmail} via SMTP ({server}:{port}) <<<\n");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Fallo al enviar correo a {destinatarioEmail}. Caída en envío SMTP.");
            // En desarrollo, mostramos el código OTP por si falló el SMTP real
            _logger.LogWarning($"[FALLBACK DE SEGURIDAD] OTP código en consola: {codigoOtp}");
            Console.WriteLine($"\n>>> [SMTP ERROR] Failed to send email to {destinatarioEmail}: {ex.Message} <<<");
            Console.WriteLine($">>> [SMTP EXCEPTION DETAILS]\n{ex}\n<<<");
            Console.WriteLine($">>> [SMTP ERROR FALLBACK] OTP Code for {destinatarioEmail}: {codigoOtp} <<<\n");
            // No bloqueamos el flujo de desarrollo, solo registramos.
        }
    }
}
