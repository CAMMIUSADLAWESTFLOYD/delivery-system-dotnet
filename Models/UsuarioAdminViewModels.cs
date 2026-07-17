namespace delivery_system_dotnet.Models;

// Filas de las pantallas de administración de usuarios. "Activo" es la
// propiedad sombra de Person (aprobación/suspensión de la cuenta).
public class RepartidorItemViewModel
{
    public Delivery Delivery { get; set; } = null!;
    public bool Activo { get; set; }
    public int EntregasCompletadas { get; set; }
    public double Ganancias { get; set; }
}

public class ClienteItemViewModel
{
    public Client Client { get; set; } = null!;
    public bool Activo { get; set; }
    public int Pedidos { get; set; }
    public double TotalGastado { get; set; }
}
