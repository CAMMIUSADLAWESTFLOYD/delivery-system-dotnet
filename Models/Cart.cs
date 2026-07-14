using System;
using System.Collections.Generic;
using delivery_system_dotnet.Interfaces;

namespace delivery_system_dotnet.Models;

public class Cart : ICalculate
{
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public double CurrentTotal { get; set; }

    private readonly List<(Products Product, int Quantity)> _items = new();

    public void AddNewProduct(Products product, int quantity)
    {
        _items.Add((product, quantity));
        CurrentTotal = ICalculateTotal(CurrentTotal);
    }

    public double ICalculateTotal(double price)
    {
        double sum = 0;
        foreach (var item in _items)
        {
            sum += item.Product.Price * item.Quantity;
        }
        return sum;
    }

    public double ICalculateIVA(double valuePay) => throw new NotSupportedException();
    public double ICalculateDeliveryFee(double distance) => throw new NotSupportedException();
    public double ICalculateSubTotal() => throw new NotSupportedException();
}
