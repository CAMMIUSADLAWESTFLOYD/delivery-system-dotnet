using System;
using System.ComponentModel.DataAnnotations;

namespace delivery_system_dotnet.DTOs.Request;

public class RegistroDeliveryDto
{
    [Required(ErrorMessage = "El DNI es obligatorio.")]
    public string Dni { get; set; } = null!;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100)]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
    public DateTime DateBirth { get; set; }

    [Required(ErrorMessage = "La dirección es obligatoria.")]
    [StringLength(250)]
    public string Address { get; set; } = null!;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress]
    [StringLength(150)]
    public string Mail { get; set; } = null!;

    [Required(ErrorMessage = "El género es obligatorio.")]
    public bool Gender { get; set; }

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [StringLength(20)]
    public string Phone { get; set; } = null!;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(6)]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "El tipo de vehículo es obligatorio.")]
    [StringLength(100)]
    public string TypeVehicle { get; set; } = null!;

    [Required(ErrorMessage = "La placa es obligatoria.")]
    [StringLength(50)]
    public string LicensePlate { get; set; } = null!;
}
