using System;
using delivery_system_dotnet.Interfaces;

namespace delivery_system_dotnet.Models;

public class Delivery : Person, ICalculate
{
    public string TypeVehicle { get; set; } = null!;
    public string LicensePlate { get; set; } = null!;
    public bool Status { get; set; } = true;
    // Un repartidor nuevo arranca SIN calificación (0). Su nota real se calcula
    // como el promedio de las estrellas que le den los clientes al entregar.
    public double Rating { get; set; } = 0.0;
    public double Comission { get; set; }

    // Ubicación actual del repartidor (para el mapa de seguimiento). Nullable.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? LastLocationUpdate { get; set; }

    public Delivery()
    {
    }

    // Constructor de copia
    public Delivery(Delivery oldDelivery)
    {
        this.dni = oldDelivery.Dni;
        this.name = oldDelivery.Name;
        this.dateBirth = oldDelivery.DateBirth;
        this.address = oldDelivery.Address;
        this.mail = oldDelivery.Mail;
        this.gender = oldDelivery.Gender;
        this.phone = oldDelivery.Phone;
        this.password = oldDelivery.Password;
        this.TypeVehicle = oldDelivery.TypeVehicle;
        this.LicensePlate = oldDelivery.LicensePlate;
        this.Status = oldDelivery.Status;
        this.Rating = oldDelivery.Rating;
        this.Comission = oldDelivery.Comission;
        this.Latitude = oldDelivery.Latitude;
        this.Longitude = oldDelivery.Longitude;
        this.LastLocationUpdate = oldDelivery.LastLocationUpdate;
    }

    public double CalculateComission(double commissionVal)
    {
        this.Comission = commissionVal;
        return this.Comission;
    }

    public void AddNewProduct(Products product, int quantity)
    {
        // El inventario por stock fue retirado del módulo de productos;
        // reponer un producto equivale a marcarlo disponible nuevamente.
        product.Available = true;
    }

    public double ICalculateDeliveryFee(double distance)
    {
        return distance * 2.5;
    }

    public double ICalculateIVA(double valuePay) => throw new NotSupportedException();
    public double ICalculateTotal(double price) => throw new NotSupportedException();
    public double ICalculateSubTotal() => throw new NotSupportedException();
}
