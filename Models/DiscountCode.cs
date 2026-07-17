using System.ComponentModel.DataAnnotations;

namespace delivery_system_dotnet.Models;

// Código promocional aplicable en el checkout. El porcentaje se descuenta
// del subtotal de productos (no del envío ni del IVA).
public class DiscountCode
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string Code { get; set; } = null!;

    // Porcentaje de descuento sobre el subtotal (1–100).
    [Range(1, 100)]
    public double Percent { get; set; }

    public bool Active { get; set; } = true;
}
