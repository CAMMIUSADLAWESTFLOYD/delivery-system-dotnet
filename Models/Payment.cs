using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace delivery_system_dotnet.Models;

public class Payment
{
    [Key]
    public string OrderId { get; set; } = null!;

    [ForeignKey("OrderId")]
    public Order? Order { get; set; }

    public string PaymentMethod { get; set; } = null!;
}
