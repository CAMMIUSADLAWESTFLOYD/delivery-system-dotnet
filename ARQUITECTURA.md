# FlashBite Delivery — Guía de Arquitectura

Guía para entender y explicar el proyecto completo: qué hace cada carpeta, cómo
viaja una petición, qué patrones de diseño se usan y cómo funcionan los flujos
principales del negocio.

---

## 1. ¿Qué es?

Un sistema de delivery de comida (estilo PedidosYa/Uber Eats) para la ciudad de
Ibarra, con **tres roles**: Cliente, Repartidor y Administrador.

**Stack:**

| Capa | Tecnología |
|------|-----------|
| Backend | ASP.NET Core MVC (.NET 10) |
| Vistas | Razor (`.cshtml`) + Bootstrap 5 |
| Base de datos | SQL Server + Entity Framework Core (Code First + Migraciones) |
| Tiempo real | SignalR (seguimiento en vivo + notificaciones) |
| Mapas | Leaflet + OpenStreetMap; rutas reales por calles con OSRM |
| Autenticación | Cookies con Claims (rol dentro del claim) |

---

## 2. Mapa del proyecto (dónde está cada cosa)

```
Controllers/      → Reciben la petición, orquestan, devuelven la vista
  AuthController         Login, registro, recuperación de contraseña (OTP)
  ClienteController      Catálogo, carrito, checkout, seguimiento, factura
  DeliveryController     Panel del repartidor, aceptar/rechazar, entregar
  AdminController        Gestión de productos, restaurantes, usuarios, pagos
  Api/                   Endpoints JSON (Auth, Orders, Products)

Models/           → Entidades del dominio + ViewModels
  Person.cs              Clase base (herencia TPH)
  Client / Delivery / Admin.cs   Los 3 tipos de usuario
  Order.cs, OrderDetails.cs, Payment.cs
  Products.cs, Restaurant.cs, DiscountCode.cs
  OrderStates/OrderState.cs      ← PATRÓN STATE (máquina de estados)
  *ViewModel.cs          Modelos solo de presentación (no se guardan en BD)

Services/         → Lógica de negocio (el corazón, fuera de los controladores)
  RealOrderManager.cs        Cambia el estado del pedido (valida con el State)
  ProxyOrderManager.cs       ← PATRÓN PROXY (control de acceso por rol)
  RiderAssignmentService.cs  Asigna el repartidor más cercano (Haversine)
  DeliveryTrackingService.cs Simulación de la entrega en vivo (ruta + posición)
  NotificationService.cs     Notificaciones (toasts) por SignalR
  UsuarioService.cs          Registro, login, perfiles, contraseñas (BCrypt)
  EmailService / CodigoVerificacionService   Correos y códigos OTP

Data/             → ApplicationDbContext (EF Core) + script de datos semilla
Hubs/             → TrackingHub (canal SignalR)
Interfaces/       → Contratos: IOrderManager, ICalculate (IVA, tarifas)
Middleware/       → ExceptionHandlingMiddleware (captura errores globales)
Binders/          → InvariantDoubleModelBinder (decimales con punto o coma)
Views/            → Razor: Auth, Cliente, Delivery, Admin, Shared(_Layout)
Migrations/       → Historial de cambios de la base de datos (EF)
```

---

## 3. Cómo viaja una petición (flujo MVC)

```
Navegador
   ↓  (POST /Cliente/Checkout)
Middleware  → manejo de errores, cabeceras de seguridad, sesión, auth, CSRF
   ↓
Controller  → ClienteController.Checkout(...)   [valida entrada]
   ↓
Services    → RiderAssignmentService, NotificationService  [reglas de negocio]
   ↓
DbContext   → EF Core traduce LINQ a SQL parametrizado
   ↓
SQL Server
   ↓
View (.cshtml) → HTML renderizado de vuelta al navegador
```

**La idea clave a explicar:** el controlador **no** contiene la lógica de
negocio; solo coordina. Las reglas viven en `Services/`, y las reglas del ciclo
de vida del pedido viven en `Models/OrderStates/`. Esto es separación de
responsabilidades.

---

## 4. Patrones de diseño usados

### 4.1 State (Estado) — `Models/OrderStates/OrderState.cs`
Cada estado del pedido es una **clase** que sabe a qué otros estados puede pasar
y **qué rol** puede hacerlo.

```
Pending (Pendiente/PendienteRepartidor)
   → Paid        (lo acepta Delivery o Admin)
   → Cancelled   (Client o Admin)

Paid (Aceptado/Preparando)
   → Shipped (En Camino) | Cancelled (Admin) | CancelRequested | Reported

Shipped (En Camino)
   → Delivered | Cancelled (Admin) | CancelRequested | Reported

Delivered / Cancelled  = estados TERMINALES (no salen de ahí)
```

`RealOrderManager` llama a `OrderState.ValidateTransition(...)` **antes** de
guardar. Si la transición es inválida, lanza excepción y el pedido no se toca.
**Ventaja a defender:** las reglas del ciclo de vida están en **un solo lugar**,
no repartidas por los controladores.

### 4.2 Proxy — `Services/ProxyOrderManager.cs`
Envuelve a `RealOrderManager` y hace **control de acceso** antes de delegar:

1. Si el rol no es `Admin` ni `Delivery` → lanza `UnauthorizedAccessException`.
2. Si es `Delivery`, solo permite un conjunto acotado de estados
   (`Aceptado`, `En Camino`, `Entregado`, `Reportado`, `CancelacionSolicitada`).
3. Si pasa ambos filtros, **delega** en `_realOrderManager.ChangeStatus(...)`.

El controlador pide `IOrderManager` y recibe el proxy sin enterarse (DI).

**Doble barrera (importante al explicar):** el *Proxy* responde a **"¿este rol
puede hacer esto?"**, y el *State* responde a **"¿este pedido puede pasar de
este estado a ese otro?"**. Son controles distintos y complementarios.

### 4.3 Inyección de Dependencias (DI)
Todo se registra en `Program.cs` y se recibe por constructor:
```csharp
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IOrderManager, ProxyOrderManager>();
builder.Services.AddSingleton<IDeliveryTrackingService, DeliveryTrackingService>();
```
Los controladores dependen de **interfaces**, no de clases concretas.

### 4.4 Herencia TPH (Table Per Hierarchy)
`Person` es la clase base; `Client`, `Delivery` y `Admin` heredan de ella.
EF Core guarda los tres en **una sola tabla** `Persons` con una columna
`Discriminator` que dice de qué tipo es cada fila.

### 4.5 ViewModel
`CartViewModel`, `ProductImageViewModel`, etc. son objetos **solo para la vista**
(no se persisten). Evitan mandar entidades de BD directamente al HTML.

### 4.6 Middleware
`ExceptionHandlingMiddleware` captura errores no controlados en un punto único.

---

## 5. Flujos principales del negocio

### 5.1 Ciclo de vida de un pedido
1. **Cliente** arma el carrito (guardado en *sesión*, no en BD).
2. **Checkout**: valida pago, horario del restaurante, calcula distancia y total.
3. Nace el pedido como `Pendiente`.
4. **Asignación automática** → pasa a `PendienteRepartidor`.
5. **Repartidor acepta** → `Aceptado` (arranca la simulación del recorrido).
6. Llega al local → `Preparando` (espera) → sale → `En Camino`.
7. Repartidor confirma cobro → `Entregado` (terminal).

### 5.2 Asignación automática del repartidor — `RiderAssignmentService`
El cliente **ya no elige** repartidor. El sistema busca al que esté:
**disponible + aprobado + sin entrega activa**, y de esos elige el **más cercano
al restaurante** usando la fórmula de **Haversine** (distancia entre dos
coordenadas sobre la esfera terrestre).

Se reasigna solo cuando: alguien rechaza, un repartidor se pone disponible, o
termina una entrega.

### 5.3 Seguimiento en vivo — `DeliveryTrackingService`
- Pide la **ruta real por calles** al servicio OSRM (OpenStreetMap).
- Calcula cuánto dura el tramo: `distancia / velocidad`, acotado entre un
  mínimo y un máximo para que sea observable.
- Cada 0,5 s interpola la posición sobre la ruta y la difunde por **SignalR**
  al mapa del cliente (sin que el navegador tenga que preguntar).

Constantes ajustables (velocidad del repartidor):
```csharp
private const double SpeedKmh = 14.0;        // ↑ = más rápido
private const double MinLegSeconds = 120.0;  // ↑ = más lento (tramos cortos)
private const double MaxLegSeconds = 420.0;  // ↑ = más lento (tramos largos)
private const double PrepSeconds = 45.0;     // espera en el restaurante
```

### 5.4 Notificaciones en tiempo real — `NotificationService` + `TrackingHub`
Se emiten a **grupos** de SignalR: `user-{dni}` (personal) y `role-{rol}`
(todos los admins, por ejemplo). El `_Layout` abre la conexión y muestra un
*toast*. La identidad del grupo sale de la **cookie**, nunca de un parámetro
del cliente (nadie puede espiar el canal de otro).

### 5.5 Pagos — `Payment.cs`
| Método | Cómo se valida |
|--------|----------------|
| Tarjeta | Algoritmo de **Luhn**; se guarda solo `VISA ****1111`, nunca el número |
| Transferencia | El cliente sube el comprobante; el **admin lo verifica** |
| Efectivo | Se cobra contra entrega |

**Reembolsos:** si un pedido pagado se cancela, aparece en "Reembolsos
pendientes" del admin, que lo marca como devuelto.

### 5.6 Snapshot en los detalles del pedido — `OrderDetails.cs`
Guarda `ProductName` y `UnitPrice` **copiados** al momento de comprar. Si mañana
sube el precio del producto, la factura vieja **no cambia**. Es la práctica
correcta en facturación.

---

## 6. Seguridad implementada

| Amenaza | Protección |
|---------|-----------|
| **Inyección SQL** | EF Core parametriza todo (LINQ). No hay SQL con datos de usuario |
| **XSS** | Razor codifica la salida por defecto; los `Html.Raw` van encodeados |
| **CSRF** | Token antiforgery validado globalmente en todo POST |
| **Acceso a datos ajenos (IDOR)** | Cada acción filtra por dueño (`ClientDni` / `DeliveryDni`) |
| **Contraseñas** | Hash con **BCrypt** (nunca en texto plano) |
| **Autorización** | `[Authorize(Roles="...")]`; el rol sale de los *claims*, no del request |
| **Cabeceras** | `nosniff`, `X-Frame-Options`, `Referrer-Policy` |

**Regla de oro del proyecto:** toda acción que recibe un `orderId` debe filtrar
el pedido por su dueño. Nunca confiar en que el Id venga "del formulario".

---

## 7. Cómo ejecutarlo

```bash
dotnet build                       # compilar
# luego ejecutar bin/Debug/net10.0/delivery-system-dotnet.exe   (puerto 5000)
```

> Tras cambiar **cualquier** archivo `.cs` o `.cshtml` hay que **cerrar el .exe,
> recompilar y volver a abrirlo**. Las vistas se compilan en el build.

**Cuentas de prueba:**

| Rol | Correo | Contraseña |
|-----|--------|-----------|
| Admin | admin@mail.com | admin123 |
| Cliente | cliente@mail.com | cliente123 |
| Repartidor | delivery@mail.com | delivery123 |

---

## 8. Preguntas típicas y cómo responderlas

**¿Por qué el patrón State y no un `if`/`switch`?**
Porque las reglas de transición quedan centralizadas y tipadas. Añadir un estado
nuevo es crear una clase, sin tocar los controladores. Evita estados imposibles.

**¿Dónde está la lógica de negocio?**
En `Services/`. Los controladores solo validan la entrada y orquestan.

**¿Cómo sabe el sistema qué repartidor asignar?**
`RiderAssignmentService` filtra disponibles/aprobados/libres y ordena por
distancia Haversine al restaurante; toma el primero.

**¿Cómo se mueve el repartidor en el mapa?**
El servidor calcula la ruta real (OSRM) e interpola la posición por tiempo;
la difunde por SignalR. El navegador solo pinta lo que recibe.

**¿Cómo evitas que un usuario vea o modifique el pedido de otro?**
Cada consulta filtra por el DNI del usuario autenticado, tomado de la cookie.

**¿Por qué guardas el nombre y precio del producto en OrderDetails?**
Para que las facturas históricas no cambien si el catálogo cambia (snapshot).
