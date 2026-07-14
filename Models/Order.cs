using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using delivery_system_dotnet.Interfaces;

namespace delivery_system_dotnet.Models;

public class Order : ICalculate
{
    [Key]
    public string Id { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string DestinyAddres { get; set; } = null!;

    public int? RestaurantId { get; set; }
    
    [ForeignKey("RestaurantId")]
    public Restaurant? Restaurant { get; set; }

    public double Total { get; set; }
    public bool PaymentStatus { get; set; }

    public string? ClientDni { get; set; }
    
    [ForeignKey("ClientDni")]
    public Client? Client { get; set; }

    public string? DeliveryDni { get; set; }
    
    [ForeignKey("DeliveryDni")]
    public Delivery? Delivery { get; set; }

    public double Distance { get; set; }
    public DateTime DateOrder { get; set; } = DateTime.UtcNow;

    // Coordenadas reales del destino de entrega (capturadas por geolocalización
    // al momento del pedido). Nullable para no romper pedidos existentes.
    public double? DestLatitude { get; set; }
    public double? DestLongitude { get; set; }

    // Posición ACTUAL del repartidor para este pedido (se actualiza durante la
    // entrega y permite ver el recorrido en tiempo real). Nullable/aditivo.
    public double? CurrentLatitude { get; set; }
    public double? CurrentLongitude { get; set; }

    // Coordenadas fijas solicitadas por el Arquitecto para el fix final
    public double ClienteLatitud { get; set; }
    public double ClienteLongitud { get; set; }

    // Calificación (1-5) que el cliente dejó al repartidor tras la entrega.
    // Null mientras el pedido no ha sido calificado; se usa además para
    // impedir calificar dos veces el mismo pedido.
    public int? Rating { get; set; }

    public ICollection<OrderDetails> OrderDetails { get; set; } = new List<OrderDetails>();

    public Payment? Payment { get; set; }

    public Order()
    {
    }

    // Constructor de copia
    public Order(Order oldOrder)
    {
        this.Id = oldOrder.Id;
        this.Status = oldOrder.Status;
        this.DestinyAddres = oldOrder.DestinyAddres;
        this.RestaurantId = oldOrder.RestaurantId;
        this.Restaurant = oldOrder.Restaurant != null ? new Restaurant(oldOrder.Restaurant) : null;
        this.Total = oldOrder.Total;
        this.PaymentStatus = oldOrder.PaymentStatus;
        this.ClientDni = oldOrder.ClientDni;
        this.Client = oldOrder.Client != null ? new Client(oldOrder.Client) : null;
        this.DeliveryDni = oldOrder.DeliveryDni;
        this.Delivery = oldOrder.Delivery != null ? new Delivery(oldOrder.Delivery) : null;
        this.Distance = oldOrder.Distance;
        this.DateOrder = oldOrder.DateOrder;
        this.DestLatitude = oldOrder.DestLatitude;
        this.DestLongitude = oldOrder.DestLongitude;
        this.CurrentLatitude = oldOrder.CurrentLatitude;
        this.CurrentLongitude = oldOrder.CurrentLongitude;
        this.Rating = oldOrder.Rating;
        this.Payment = oldOrder.Payment;
        
        this.OrderDetails = new List<OrderDetails>();
        foreach (var detail in oldOrder.OrderDetails)
        {
            this.OrderDetails.Add(new OrderDetails(detail));
        }
    }

    public double ICalculateDeliveryFee(double distanceVal)
    {
        return distanceVal * 1.5;
    }

    public double ICalculateTotal(double price)
    {
        double fee = ICalculateDeliveryFee(Distance);
        double beforeIva = price + fee;
        double ivaVal = ICalculateIVA(beforeIva);
        Total = beforeIva + ivaVal;
        return Total;
    }

    public void AddNewProduct(Products product, int quantity)
    {
        var detail = new OrderDetails
        {
            Quantity = quantity,
            OrderId = this.Id,
            Order = this
        };
        detail.Products.Add(product);
        detail.ICalculateSubTotal();
        
        OrderDetails.Add(detail);
        
        // Recalculate Total based on all subTotals
        double sum = 0;
        foreach (var item in OrderDetails)
        {
            sum += item.SubTotal;
        }
        ICalculateTotal(sum);
    }

    public double ICalculateIVA(double valuePay)
    {
        return valuePay * (ICalculate.iva / 100.0);
    }

    public double ICalculateSubTotal() => throw new NotSupportedException();
}
