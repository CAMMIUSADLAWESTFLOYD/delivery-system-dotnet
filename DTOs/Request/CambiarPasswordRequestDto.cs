using System.ComponentModel.DataAnnotations;

namespace delivery_system_dotnet.DTOs.Request;

public class CambiarPasswordRequestDto
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "El código OTP es obligatorio.")]
    [StringLength(6, MinimumLength = 6)]
    public string Codigo { get; set; } = null!;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    public string NewPassword { get; set; } = null!;
}
