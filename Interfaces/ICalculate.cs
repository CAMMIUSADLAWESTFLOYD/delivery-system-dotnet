using System;

namespace delivery_system_dotnet.Interfaces;

public interface ICalculate
{
    public const int iva = 15;

    double ICalculateIVA(double valuePay);
    double ICalculateTotal(double price);
    double ICalculateDeliveryFee(double distance);
    double ICalculateSubTotal();
}
