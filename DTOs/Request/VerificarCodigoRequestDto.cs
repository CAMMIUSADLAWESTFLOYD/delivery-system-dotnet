using System.ComponentModel.DataAnnotations;
using delivery_system_dotnet.Models.Enums;

namespace delivery_system_dotnet.DTOs.Request;

public class VerificarCodigoRequestDto
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "El código debe ser de 6 dígitos.")]
    public string Codigo { get; set; } = null!;

    [Required]
    public TipoCodigo Tipo { get; set; }
}
