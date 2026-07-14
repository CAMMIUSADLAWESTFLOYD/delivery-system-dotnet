using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delivery_system_dotnet.Data;

namespace delivery_system_dotnet.Controllers;

/// <summary>
/// Controlador temporal de diagnóstico. NO requiere autenticación.
/// Se eliminará una vez confirmado que la DB está poblada.
/// </summary>
public class DiagController : Controller
{
    private readonly ApplicationDbContext _context;

    public DiagController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Check()
    {
        var restaurantCount = _context.Restaurants.Count();
        var productCount = _context.Products.Count();
        var firstProducts = _context.Products
            .Include(p => p.Restaurant)
            .Take(5)
            .Select(p => new {
                p.Id,
                p.Name,
                p.Price,
                p.Available,
                p.Category,
                p.RestaurantId,
                RestaurantIsActive = p.Restaurant != null ? p.Restaurant.IsActive : false
            })
            .ToList();

        return Json(new {
            RestaurantCount = restaurantCount,
            ProductCount = productCount,
            FirstProducts = firstProducts
        });
    }
}
