using System.Collections.Generic;
using System.Linq;

namespace delivery_system_dotnet.Models;

// ViewModel de presentación para el carrito de compras basado en sesión.
// No es una entidad persistida: acompaña a la clase de dominio Cart (UML) y
// se usa para renderizar ViewCart.cshtml con los precios y totales reales.
public class CartLineItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public double Price { get; set; }
    public int Quantity { get; set; }
    public string? ImagePath { get; set; }
    public bool Available { get; set; }
    public double SubTotal => Price * Quantity;
}

public class CartViewModel
{
    public List<CartLineItem> Items { get; set; } = new();

    public double Subtotal => Items.Sum(i => i.SubTotal);

    // Costo de envío base (coincide con Order.ICalculateDeliveryFee sobre distancia demo)
    public double DeliveryFee { get; set; }

    // IVA del 15% (ICalculate.iva) aplicado sobre Subtotal + envío
    public double Iva => (Subtotal + DeliveryFee) * (Interfaces.ICalculate.iva / 100.0);

    public double Total => Subtotal + DeliveryFee + Iva;

    public int TotalUnits => Items.Sum(i => i.Quantity);
}
