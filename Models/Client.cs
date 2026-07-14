using System;
using delivery_system_dotnet.Interfaces;

namespace delivery_system_dotnet.Models;

public class Client : Person, ICalculate
{
    public int PayMethod { get; set; }
    public DateTime RegisterDate { get; set; } = DateTime.UtcNow;
    public DateTime DateOrder { get; set; } = DateTime.UtcNow;

    // Ubicación real del cliente (capturada por navigator.geolocation). Nullable.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public Client()
    {
    }

    // Constructor de copia
    public Client(Client oldClient)
    {
        this.dni = oldClient.Dni;
        this.name = oldClient.Name;
        this.dateBirth = oldClient.DateBirth;
        this.address = oldClient.Address;
        this.mail = oldClient.Mail;
        this.gender = oldClient.Gender;
        this.phone = oldClient.Phone;
        this.password = oldClient.Password;
        this.PayMethod = oldClient.PayMethod;
        this.RegisterDate = oldClient.RegisterDate;
        this.DateOrder = oldClient.DateOrder;
        this.Latitude = oldClient.Latitude;
        this.Longitude = oldClient.Longitude;
    }

    public int RateServices()
    {
        return 5;
    }

    public int CalculateNumbersOrder()
    {
        return 1;
    }

    public double Pay(double value)
    {
        return value;
    }

    public double ICalculateIVA(double valuePay)
    {
        return valuePay * (ICalculate.iva / 100.0);
    }

    public double ICalculateTotal(double price) => throw new NotSupportedException();
    public double ICalculateDeliveryFee(double distance) => throw new NotSupportedException();
    public double ICalculateSubTotal() => throw new NotSupportedException();
}
