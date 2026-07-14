using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace delivery_system_dotnet.Models;

// Producto del catálogo. Estructura limpia: clave primaria numérica (Id) y
// relación con su restaurante EXCLUSIVAMENTE mediante RestaurantId.
public class Products
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int RestaurantId { get; set; }

    [ForeignKey("RestaurantId")]
    public Restaurant? Restaurant { get; set; }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public double Price { get; set; }
    public string? Category { get; set; }
    public string? ImagePath { get; set; }
    public bool Available { get; set; } = true;

    // Relación con el detalle de pedido (módulo de Pedidos, se conserva intacta):
    // OrderDetails 0..1 --- 0..* Products
    public int? OrderDetailsId { get; set; }
    public OrderDetails? OrderDetails { get; set; }

    public Products()
    {
    }

    // Constructor de copia
    public Products(Products oldProducts)
    {
        this.Id = oldProducts.Id;
        this.RestaurantId = oldProducts.RestaurantId;
        this.Restaurant = oldProducts.Restaurant;
        this.Name = oldProducts.Name;
        this.Description = oldProducts.Description;
        this.Price = oldProducts.Price;
        this.Category = oldProducts.Category;
        this.ImagePath = oldProducts.ImagePath;
        this.Available = oldProducts.Available;
        this.OrderDetailsId = oldProducts.OrderDetailsId;
        this.OrderDetails = oldProducts.OrderDetails;
    }
}
