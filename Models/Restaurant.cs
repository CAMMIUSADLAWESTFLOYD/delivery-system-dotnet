using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace delivery_system_dotnet.Models;

// Restaurante. Estructura limpia: clave primaria numérica (Id); los productos
// se relacionan mediante Product.RestaurantId (nunca por nombre).
public class Restaurant
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Address { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string? Category { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? ImagePath { get; set; }
    public TimeSpan OpeningHour { get; set; }
    public TimeSpan ClosingHour { get; set; }
    public bool IsActive { get; set; } = true;

    // Reputación del negocio: promedio de las calificaciones (1-5) que los
    // clientes dejan al calificar sus pedidos, y cuántas ha recibido.
    // Cada nueva calificación recalcula el promedio (ver CalificarServicio).
    public double Rating { get; set; } = 5.0;
    public int RatingCount { get; set; } = 0;

    // Restaurant 1 --- 0..* Products (vía RestaurantId)
    public ICollection<Products> Products { get; set; } = new List<Products>();

    public Restaurant()
    {
    }

    // Constructor de copia
    public Restaurant(Restaurant oldRestaurant)
    {
        this.Id = oldRestaurant.Id;
        this.Name = oldRestaurant.Name;
        this.Description = oldRestaurant.Description;
        this.Address = oldRestaurant.Address;
        this.Phone = oldRestaurant.Phone;
        this.Category = oldRestaurant.Category;
        this.Latitude = oldRestaurant.Latitude;
        this.Longitude = oldRestaurant.Longitude;
        this.ImagePath = oldRestaurant.ImagePath;
        this.OpeningHour = oldRestaurant.OpeningHour;
        this.ClosingHour = oldRestaurant.ClosingHour;
        this.IsActive = oldRestaurant.IsActive;
        this.Rating = oldRestaurant.Rating;
        this.RatingCount = oldRestaurant.RatingCount;
    }

    public bool OrderAcceptation(Order newOrder)
    {
        return newOrder.Status != "Cancelado";
    }
}
