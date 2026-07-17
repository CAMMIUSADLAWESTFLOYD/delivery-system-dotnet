# FlashBite Delivery — Diagrama de Clases

Diagrama del modelo de dominio y de los patrones de diseño del sistema
(**State** para el ciclo de vida del pedido y **Proxy** para el control de
acceso a los cambios de estado). Renderizable con Mermaid (GitHub, VS Code
con la extensión Mermaid, o https://mermaid.live).

## Modelo de dominio

```mermaid
classDiagram
    direction TB

    class Person {
        <<abstract>>
        +string Dni
        +string Name
        +string Mail
        +string Phone
        +string Address
        +DateTime DateBirth
        +bool Gender
        #string password
        +SetPassword(hash)
        +GetPassword() string
    }

    class Client {
        +int PayMethod
        +double? Latitude
        +double? Longitude
    }

    class Admin {
        +string Rol
        +string Succursal
        +ComparePassword(pwd) bool
    }

    class Delivery {
        +string TypeVehicle
        +string LicensePlate
        +bool Status  %% disponibilidad
        +double Rating
        +double Comission
        +double? Latitude
        +double? Longitude
        +CalculateComission(val) double
    }

    class Restaurant {
        +int Id
        +string Name
        +string Description
        +string Address
        +string Phone
        +string Category
        +double Latitude
        +double Longitude
        +string ImagePath
        +TimeSpan OpeningHour
        +TimeSpan ClosingHour
        +bool IsActive
        +OrderAcceptation(order) bool
    }

    class Products {
        +int Id
        +int RestaurantId
        +string Name
        +string Description
        +double Price
        +string Category
        +string ImagePath
        +bool Available
    }

    class Order {
        +string Id
        +string Status  %% respaldado por el patrón State
        +string DestinyAddres
        +double Total
        +double Distance  %% km reales (Haversine/OSRM)
        +bool PaymentStatus
        +double? DeliveryFee  %% Distance × $1.50/km
        +string? DiscountCode
        +double? DiscountAmount
        +int? Rating  %% calificación del cliente al repartidor
        +string? CancelRequestReason
        +DateTime DateOrder
        +double? DestLatitude
        +double? DestLongitude
        +double? CurrentLatitude
        +double? CurrentLongitude
        +ICalculateTotal(price) double
        +ICalculateDeliveryFee(km) double
        +AddNewProduct(product, qty)
    }

    class OrderDetails {
        +int Id
        +int Quantity
        +double SubTotal
        +ICalculateSubTotal() double
    }

    class Payment {
        +string OrderId
        +string PaymentMethod  %% Tarjeta | Transferencia | Efectivo
    }

    class DiscountCode {
        +int Id
        +string Code
        +double Percent
        +bool Active
    }

    class ICalculate {
        <<interface>>
        +iva: 15
        +ICalculateTotal(price) double
        +ICalculateIVA(value) double
        +ICalculateDeliveryFee(distance) double
        +ICalculateSubTotal() double
    }

    Person <|-- Client
    Person <|-- Admin
    Person <|-- Delivery

    Restaurant "1" o-- "0..*" Products : catálogo (RestaurantId)
    Order "0..*" --> "0..1" Restaurant : RestaurantId
    Order "0..*" --> "0..1" Client : ClientDni
    Order "0..*" --> "0..1" Delivery : DeliveryDni
    Order "1" *-- "0..*" OrderDetails
    OrderDetails "0..1" o-- "0..*" Products
    Order "1" -- "0..1" Payment
    Order ..> DiscountCode : valida código

    ICalculate <|.. Order
    ICalculate <|.. OrderDetails
    ICalculate <|.. Delivery
```

## Patrón State — ciclo de vida del pedido

`Order.Status` (BD, en español) está respaldado por una máquina de estados en
`Models/OrderStates/`. `RealOrderManager` valida cada transición contra estas
clases antes de persistir: una transición inválida lanza excepción y el pedido
no cambia.

```mermaid
classDiagram
    direction TB

    class OrderState {
        <<abstract>>
        +string Name
        +string[] DbStatuses
        +bool IsTerminal
        +CanTransitionTo(target, role) bool
        +FromDbStatus(status)$ OrderState
        +ValidateTransition(actual, nuevo, rol)$ OrderState
    }

    class PendingState {
        Pendiente, PendienteRepartidor
    }
    class PaidState {
        Aceptado, Preparando
    }
    class ShippedState {
        En Camino
    }
    class DeliveredState {
        Entregado (terminal)
    }
    class CancelledState {
        Cancelado (terminal)
    }
    class CancelRequestedState {
        CancelacionSolicitada
    }
    class ReportedState {
        Reportado
    }

    OrderState <|-- PendingState
    OrderState <|-- PaidState
    OrderState <|-- ShippedState
    OrderState <|-- DeliveredState
    OrderState <|-- CancelledState
    OrderState <|-- CancelRequestedState
    OrderState <|-- ReportedState
```

### Transiciones permitidas

```mermaid
stateDiagram-v2
    [*] --> Pending : Checkout del cliente
    Pending --> Paid : Delivery acepta / Admin
    Pending --> Cancelled : Cliente / Admin
    Paid --> Shipped : Delivery "En Camino" / Admin
    Paid --> CancelRequested : Delivery solicita
    Paid --> Reported : incidencia
    Paid --> Cancelled : Admin
    Shipped --> Delivered : Delivery / Admin / Sistema (llegada)
    Shipped --> CancelRequested : Delivery solicita
    Shipped --> Reported : incidencia
    Shipped --> Cancelled : Admin
    CancelRequested --> Cancelled : Admin aprueba
    CancelRequested --> Shipped : Admin reanuda
    Reported --> Shipped : Admin reanuda
    Reported --> Cancelled : Admin
    Delivered --> [*]
    Cancelled --> [*]
```

## Patrón Proxy — control de acceso a cambios de estado

```mermaid
classDiagram
    direction LR

    class IOrderManager {
        <<interface>>
        +ChangeStatus(orderId, userRole, newStatus) int
    }

    class ProxyOrderManager {
        -RealOrderManager real
        +ChangeStatus(...) int
        %% valida el ROL antes de delegar
    }

    class RealOrderManager {
        -ApplicationDbContext context
        -IDeliveryTrackingService tracking
        +ChangeStatus(...) int
        %% valida la TRANSICIÓN con OrderState y persiste
    }

    class DeliveryTrackingService {
        <<singleton>>
        +OnOrderStatusChanged(orderId, status)
        +OnRiderAssigned(orderId)
        %% simulación del recorrido + SignalR
    }

    IOrderManager <|.. ProxyOrderManager
    IOrderManager <|.. RealOrderManager
    ProxyOrderManager o--> RealOrderManager : delega
    RealOrderManager ..> OrderState : valida transición
    RealOrderManager --> DeliveryTrackingService : notifica
```

## Servicios y controladores (vista de alto nivel)

```mermaid
classDiagram
    direction TB

    class AuthController {
        +Login() / Logout()
        +RegistroCliente() / RegistroDelivery()
        +RecuperarPassword() / RestablecerPassword()
        +VerificarCodigo() %% OTP 2FA
    }

    class ClienteController {
        +Carrito() / Checkout()
        +ValidarDescuento(code)
        +HistorialPedidos() / SeguimientoPedido()
        +CalificarServicio(orderId, rating)
        +CancelarPedido(orderId)
    }

    class DeliveryController {
        +Dashboard() %% mapa + disponibilidad
        +GestionarEntregas() / VerMapa(orderId)
        +AceptarPedido() / RechazarPedido()
        +UpdateDeliveryStatus()
        +SolicitarCancelacion(orderId, motivo)
        +ReporteGanancias() %% PDF
        +CambiarEstado(disponible)
    }

    class AdminController {
        +Dashboard() / GenerarReporte()
        +ReportePedido(orderId) %% PDF
        +GestionarRestaurantes() + CRUD
        +GestionarProductos() + CRUD
        +GestionarRepartidores() %% aprobación
        +GestionarClientes()
        +MonitorearPedidos() / ResolverCancelacion()
    }

    class UsuarioService {
        +RegistrarClienteAsync()
        +RegistrarDeliveryAsync() %% queda pendiente de aprobación
        +AutenticarAsync()
        +DesbloquearCuentaAsync()
    }

    class EmailService {
        +EnviarCodigoOtpAsync()
    }

    ClienteController --> IOrderManager
    DeliveryController --> IOrderManager
    AdminController --> IOrderManager
    AuthController --> UsuarioService
    AuthController --> EmailService
```
