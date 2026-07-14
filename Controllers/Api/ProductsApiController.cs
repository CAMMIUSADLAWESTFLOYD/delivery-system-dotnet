using System.Linq;
using Microsoft.AspNetCore.Mvc;
using delivery_system_dotnet.Data;
using delivery_system_dotnet.DTOs.Response;

namespace delivery_system_dotnet.Controllers.Api;

[ApiController]
[Route("api/v1/products")]
public class ProductsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProductsApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult GetProducts()
    {
        var list = _context.Products.ToList();
        return Ok(new ApiResponseDto(true, "Productos obtenidos con éxito.", list));
    }
}
