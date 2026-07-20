using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using delivery_system_dotnet.Interfaces;

namespace delivery_system_dotnet.Models;

public class OrderDetails : ICalculate
{
    [Key]
    public int Id { get; set; }

    public int Quantity { get; set; }
    public double SubTotal { get; set; }

    // SNAPSHOT del producto al momento de la compra (mundo real): el detalle
    // guarda su propia copia de nombre y precio. Antes se enlazaba la fila del
    // catálogo (Products.OrderDetailsId), lo que hacía que un pedido nuevo le
    // "robara" el producto al anterior y que borrar pedidos arrastrara
    // productos reales del catálogo.
    public int? ProductId { get; set; }
    public string? ProductName { get; set; }
    public double UnitPrice { get; set; }

    public string? OrderId { get; set; }

    [ForeignKey("OrderId")]
    public Order? Order { get; set; }

    // Relación legada (pedidos antiguos); los nuevos usan el snapshot.
    public ICollection<Products> Products { get; set; } = new List<Products>();

    public OrderDetails()
    {
    }

    // Constructor de copia
    public OrderDetails(OrderDetails oldOrderDetails)
    {
        this.Id = oldOrderDetails.Id;
        this.Quantity = oldOrderDetails.Quantity;
        this.SubTotal = oldOrderDetails.SubTotal;
        this.ProductId = oldOrderDetails.ProductId;
        this.ProductName = oldOrderDetails.ProductName;
        this.UnitPrice = oldOrderDetails.UnitPrice;
        this.OrderId = oldOrderDetails.OrderId;
        this.Order = oldOrderDetails.Order;
        this.Products = new List<Products>(oldOrderDetails.Products);
    }

    public double ICalculateSubTotal()
    {
        // Con snapshot: precio congelado al momento de la compra.
        if (UnitPrice > 0)
        {
            SubTotal = UnitPrice * Quantity;
            return SubTotal;
        }
        // Compatibilidad con pedidos antiguos (relación legada al catálogo).
        double sum = 0;
        foreach (var product in Products)
        {
            sum += product.Price * Quantity;
        }
        SubTotal = sum;
        return sum;
    }

    public double ICalculateIVA(double valuePay) => throw new NotSupportedException();
    public double ICalculateTotal(double price) => throw new NotSupportedException();
    public double ICalculateDeliveryFee(double distance) => throw new NotSupportedException();
}
