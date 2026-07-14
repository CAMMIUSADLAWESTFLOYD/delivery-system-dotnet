using System.ComponentModel.DataAnnotations;

namespace delivery_system_dotnet.DTOs.Request;

public class SolicitarRecuperacionRequestDto
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress]
    public string Email { get; set; } = null!;
}
