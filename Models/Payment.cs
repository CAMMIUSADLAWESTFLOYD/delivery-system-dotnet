using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace delivery_system_dotnet.Models;

public class Payment
{
    [Key]
    public string OrderId { get; set; } = null!;

    [ForeignKey("OrderId")]
    public Order? Order { get; set; }

    // Tarjeta | Transferencia | Efectivo
    public string PaymentMethod { get; set; } = null!;

    // Tarjeta: marca y últimos 4 dígitos (p. ej. "VISA ****1111"); NUNCA se
    // almacena el número completo ni el CVV (práctica PCI del mundo real).
    // Transferencia: número de comprobante que reporta el cliente.
    [MaxLength(60)]
    public string? Reference { get; set; }

    // Tarjeta: true al aprobarse el cobro simulado en el checkout.
    // Transferencia: false hasta que el ADMINISTRADOR verifique el comprobante.
    // Efectivo: false hasta que el repartidor registra el cobro al entregar.
    public bool Verified { get; set; }

    public System.DateTime? VerifiedAt { get; set; }

    // Reembolso: cuando un pedido YA pagado (tarjeta o transferencia verificada)
    // se cancela, queda pendiente de reembolso hasta que el admin lo procesa.
    public bool Refunded { get; set; }

    public System.DateTime? RefundedAt { get; set; }
}
