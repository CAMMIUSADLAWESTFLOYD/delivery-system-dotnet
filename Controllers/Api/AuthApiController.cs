using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delivery_system_dotnet.DTOs.Request;
using delivery_system_dotnet.DTOs.Response;
using delivery_system_dotnet.Models;
using delivery_system_dotnet.Models.Enums;
using delivery_system_dotnet.Profiles;
using delivery_system_dotnet.Services;
using delivery_system_dotnet.Data;

namespace delivery_system_dotnet.Controllers.Api;

[ApiController]
[Route("api/v1/auth")]
public class AuthApiController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;
    private readonly ICodigoVerificacionService _otpService;
    private readonly IEmailService _emailService;
    private readonly IMapper _mapper;
    private readonly ApplicationDbContext _context;

    public AuthApiController(
        IUsuarioService usuarioService,
        ICodigoVerificacionService otpService,
        IEmailService emailService,
        IMapper mapper,
        ApplicationDbContext context)
    {
        _usuarioService = usuarioService;
        _otpService = otpService;
        _emailService = emailService;
        _mapper = mapper;
        _context = context;
    }

    [HttpPost("registro/cliente")]
    public async Task<IActionResult> RegistrarCliente([FromBody] RegistroClienteDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponseDto(false, "Datos inválidos."));
        }

        var cliente = _mapper.Map<Client>(dto);
        var registrado = await _usuarioService.RegistrarClienteAsync(cliente, dto.Password);
        var response = _mapper.Map<UsuarioResponseDto>(registrado);

        return StatusCode(201, new ApiResponseDto(true, "Cliente registrado exitosamente.", response));
    }

    [HttpPost("registro/delivery")]
    public async Task<IActionResult> RegistrarDelivery([FromBody] RegistroDeliveryDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponseDto(false, "Datos inválidos."));
        }

        var delivery = _mapper.Map<Delivery>(dto);
        var registrado = await _usuarioService.RegistrarDeliveryAsync(delivery, dto.Password);
        var response = _mapper.Map<UsuarioResponseDto>(registrado);

        return StatusCode(201, new ApiResponseDto(true, "Repartidor registrado exitosamente.", response));
    }

    [HttpPost("registro/admin")]
    public async Task<IActionResult> RegistrarAdmin([FromBody] RegistroAdminDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponseDto(false, "Datos inválidos."));
        }

        var admin = _mapper.Map<Admin>(dto);
        var registrado = await _usuarioService.RegistrarAdminAsync(admin, dto.Password);
        var response = _mapper.Map<UsuarioResponseDto>(registrado);

        return StatusCode(201, new ApiResponseDto(true, "Administrador registrado exitosamente.", response));
    }

    [HttpPost("enviar-codigo-login")]
    public async Task<IActionResult> EnviarCodigoLogin([FromBody] LoginRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponseDto(false, "Datos de inicio de sesión inválidos."));
        }

        var usuario = await _usuarioService.AutenticarAsync(dto.Email, dto.Password);
        var codigo = await _otpService.GenerarCodigoOtpAsync(usuario, TipoCodigo.Login);
        await _emailService.EnviarCodigoOtpAsync(usuario.Mail, usuario.Name, codigo, "Inicio de Sesión (2FA)");

        return Ok(new ApiResponseDto(true, "Se ha enviado un código de verificación de 6 dígitos (2FA) a tu correo electrónico para completar el inicio de sesión."));
    }

    [HttpPost("verificar-codigo-login")]
    public async Task<IActionResult> VerificarCodigoLogin([FromBody] VerificarCodigoRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponseDto(false, "Datos inválidos."));
        }

        var usuario = await _usuarioService.ObtenerPorEmailAsync(dto.Email);
        if (usuario == null)
        {
            return NotFound(new ApiResponseDto(false, "Usuario no encontrado."));
        }

        bool otpValido = await _otpService.ValidarCodigoOtpAsync(usuario, dto.Codigo, TipoCodigo.Login);
        if (!otpValido)
        {
            return BadRequest(new ApiResponseDto(false, "El código OTP de inicio de sesión es inválido o ha expirado."));
        }

        var response = _mapper.Map<UsuarioResponseDto>(usuario);
        return Ok(new ApiResponseDto(true, "Código OTP verificado con éxito. Sesión iniciada.", response));
    }

    [HttpPost("solicitar-recuperacion")]
    public async Task<IActionResult> SolicitarRecuperacion([FromBody] SolicitarRecuperacionRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponseDto(false, "El correo electrónico es obligatorio."));
        }

        var usuario = await _usuarioService.ObtenerPorEmailAsync(dto.Email);
        if (usuario == null)
        {
            return NotFound(new ApiResponseDto(false, "Usuario no encontrado."));
        }

        var codigo = await _otpService.GenerarCodigoOtpAsync(usuario, TipoCodigo.Recuperacion);
        await _emailService.EnviarCodigoOtpAsync(usuario.Mail, usuario.Name, codigo, "Recuperación de Contraseña");

        return Ok(new ApiResponseDto(true, "Se ha enviado un código OTP a tu correo electrónico para restablecer tu contraseña."));
    }

    [HttpPost("verificar-codigo-recuperacion")]
    public async Task<IActionResult> VerificarCodigoRecuperacion([FromBody] VerificarCodigoRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponseDto(false, "Datos del código inválidos."));
        }

        var usuario = await _usuarioService.ObtenerPorEmailAsync(dto.Email);
        if (usuario == null)
        {
            return NotFound(new ApiResponseDto(false, "Usuario no encontrado."));
        }

        bool otpValido = await _context.CodigosVerificacion.AnyAsync(c =>
            c.UsuarioDni == usuario.Dni
            && c.Codigo == dto.Codigo
            && c.Tipo == TipoCodigo.Recuperacion
            && !c.Usado
            && c.FechaExpiracion >= DateTime.UtcNow);

        if (!otpValido)
        {
            return BadRequest(new ApiResponseDto(false, "El código OTP de recuperación es inválido o ha expirado."));
        }

        return Ok(new ApiResponseDto(true, "Código OTP de recuperación verificado con éxito. Ya puedes proceder a cambiar tu contraseña."));
    }

    [HttpPost("cambiar-password")]
    public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponseDto(false, "Datos del formulario inválidos."));
        }

        await _usuarioService.CambiarPasswordAsync(dto.Email, dto.Codigo, dto.NewPassword);
        return Ok(new ApiResponseDto(true, "Contraseña actualizada con éxito. Ya puedes iniciar sesión con tus nuevas credenciales."));
    }
}
