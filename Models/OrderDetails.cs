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

    public string? OrderId { get; set; }
    
    [ForeignKey("OrderId")]
    public Order? Order { get; set; }

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
        this.OrderId = oldOrderDetails.OrderId;
        this.Order = oldOrderDetails.Order;
        this.Products = new List<Products>(oldOrderDetails.Products);
    }

    public double ICalculateSubTotal()
    {
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
