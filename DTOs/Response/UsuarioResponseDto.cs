namespace delivery_system_dotnet.DTOs.Response;

public class UsuarioResponseDto
{
    public string Dni { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Mail { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string Rol { get; set; } = null!;
    public bool Gender { get; set; }
    public string Address { get; set; } = null!;

    // Specific properties mapped for easier JSON consumption
    public string? TypeVehicle { get; set; }
    public string? LicensePlate { get; set; }
    public double? Salary { get; set; }
    public string? Succursal { get; set; }
    public int? PayMethod { get; set; }
}
