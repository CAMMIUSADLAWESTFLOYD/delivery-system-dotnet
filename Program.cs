using System;
using System.Linq;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using delivery_system_dotnet.Data;
using delivery_system_dotnet.Hubs;
using delivery_system_dotnet.Middleware;
using delivery_system_dotnet.Models;
using delivery_system_dotnet.Profiles;
using delivery_system_dotnet.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

// 1. Configurar DbContext con SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Registrar ManualMapper
builder.Services.AddSingleton<IMapper, ManualMapper>();

// 3. Registrar Servicios del Dominio
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<ICodigoVerificacionService, CodigoVerificacionService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<RealOrderManager>();
builder.Services.AddScoped<delivery_system_dotnet.Interfaces.IOrderManager, ProxyOrderManager>();

// 3b. Seguimiento en tiempo real: SignalR (WebSockets) + servicio singleton que
// calcula rutas reales con OSRM/OpenStreetMap y difunde la posición del repartidor.
builder.Services.AddSignalR();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IDeliveryTrackingService, DeliveryTrackingService>();

// 4. Configurar Autenticación por Cookies (para MVC)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
    });

// 5. Configurar Sesión (para guardar estados temporales de OTP)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Registrar controladores con vistas. El binder invariante de double garantiza
// que las coordenadas del PinSelector no se corrompan por la cultura regional
// del servidor (coma decimal), causa raíz de los errores de snapping de rutas.
builder.Services.AddControllersWithViews(options =>
{
    options.ModelBinderProviders.Insert(0, new delivery_system_dotnet.Binders.InvariantDoubleBinderProvider());
});

var app = builder.Build();

// 6. Aplicar Migraciones Automáticas y Sembrar Datos de Prueba
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        
        context.Database.Migrate();

        if (!context.Persons.Any())
        {
            // Seed Admin
            context.Admins.Add(new Admin
            {
                Dni = "ADMIN-1",
                Name = "Administrador Sistema",
                Mail = "admin@mail.com",
                Password = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Phone = "555-0101",
                Address = "Sede Central",
                DateBirth = new DateTime(1985, 5, 12),
                Gender = true,
                Succursal = "Principal",
                Rol = "Admin",
                Salary = 4500.00
            });

            // Seed Cliente (con ubicación real en Ibarra para el mapa de prueba)
            context.Clients.Add(new Client
            {
                Dni = "CLIENT-1",
                Name = "Juan Pérez",
                Mail = "cliente@mail.com",
                Password = BCrypt.Net.BCrypt.HashPassword("cliente123"),
                Phone = "555-0202",
                Address = "Av. Atahualpa y Río Chinchipe, Ibarra",
                DateBirth = new DateTime(1992, 8, 20),
                Gender = true,
                PayMethod = 1,
                Latitude = 0.3390,
                Longitude = -78.1140
            });

            // Seed Delivery
            context.Deliveries.Add(new Delivery
            {
                Dni = "DELIV-1",
                Name = "Carlos Gómez",
                Mail = "delivery@mail.com",
                Password = BCrypt.Net.BCrypt.HashPassword("delivery123"),
                Phone = "555-0303",
                Address = "Ruta Delivery",
                DateBirth = new DateTime(1995, 12, 1),
                Gender = true,
                TypeVehicle = "Motocicleta Honda Cargo",
                LicensePlate = "MOTO-456",
                Status = true,
                Rating = 4.8,
                Comission = 150.00,
                Latitude = 0.3480,
                Longitude = -78.1210
            });

            // Las cuentas sembradas deben quedar ACTIVAS (la propiedad sombra 'Activo'
            // por defecto sería false en el INSERT y bloquearía el inicio de sesión).
            foreach (var e in context.ChangeTracker.Entries<Person>())
                e.Property("Activo").CurrentValue = true;

            context.SaveChanges();
            Console.WriteLine(">>> Cuentas por defecto (admin/cliente/delivery) creadas. <<<");
        }

        // Garantiza que el administrador inicial del seed siempre pueda iniciar sesión
        // (requisito: administrador inicial creado por Seed), incluso en BDs existentes.
        var seedAdmin = context.Persons.FirstOrDefault(p => p.Mail == "admin@mail.com");
        if (seedAdmin != null && !context.Entry(seedAdmin).Property<bool>("Activo").CurrentValue)
        {
            context.Entry(seedAdmin).Property<bool>("Activo").CurrentValue = true;
            context.SaveChanges();
        }

        // Catálogo de restaurantes y productos: se siembra SOLO si la base está
        // vacía. Así los cambios del administrador (crear/editar restaurantes,
        // productos y categorías) sobreviven a los reinicios de la app.
        // Para forzar una resiembra completa: DELETE FROM Products; DELETE FROM
        // Restaurants; y reinicia la aplicación.
        if (!context.Restaurants.Any())
        {
            ResetDatabaseCompletely(context);
        }

        // Códigos de descuento de ejemplo (una sola vez; el admin puede
        // gestionar la tabla DiscountCodes directamente).
        if (!context.DiscountCodes.Any())
        {
            context.DiscountCodes.AddRange(
                new DiscountCode { Code = "FLASH10", Percent = 10, Active = true },
                new DiscountCode { Code = "IBARRA5", Percent = 5, Active = true },
                new DiscountCode { Code = "BIENVENIDO15", Percent = 15, Active = true });
            context.SaveChanges();
            Console.WriteLine(">>> Códigos de descuento sembrados: FLASH10, IBARRA5, BIENVENIDO15 <<<");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($">>> Error al migrar/sembrar la base de datos: {ex.Message} <<<");
    }
}

// 7. Configurar Middleware Global de Excepciones
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}


// Importante para servir archivos estáticos (CSS, JS)
app.UseStaticFiles();

// Las páginas HTML son dinámicas (carrito, listados, sesión): impedir que el
// navegador las reutilice de su caché heurística. Los estáticos no se ven
// afectados (UseStaticFiles responde antes y site.css ya se versiona por hash).
app.Use(async (ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        if ((ctx.Response.ContentType ?? "").Contains("text/html"))
        {
            ctx.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        }
        return Task.CompletedTask;
    });
    await next();
});

app.UseRouting();

// Habilitar la sesión antes de Autenticación y Autorización
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// Mapear rutas de controladores
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

// Hub de SignalR para el seguimiento del repartidor en tiempo real
app.MapHub<TrackingHub>("/hubs/tracking");

app.Run();


// ============================================================================
//  Seeder de catálogo (Reset Total con Transacciones e Integridad Referencial).
//  - Elimina físicamente usando SQL crudo en orden de dependencias.
//  - Inserta Restaurantes, obtiene IDs reales, y los asocia a Productos.
//  - Usa transacciones para asegurar operaciones atómicas.
// ============================================================================
static void ResetDatabaseCompletely(ApplicationDbContext context)
{
    using var transaction = context.Database.BeginTransaction();
    try
    {
        // 1. Limpieza Destructiva (Orden Topológico Inverso)
        context.Database.ExecuteSqlRaw("DELETE FROM OrderDetails");
        context.Database.ExecuteSqlRaw("DELETE FROM Payments");
        context.Database.ExecuteSqlRaw("DELETE FROM Orders");
        context.Database.ExecuteSqlRaw("DELETE FROM Products");
        context.Database.ExecuteSqlRaw("DELETE FROM Restaurants");
        context.SaveChanges();

        // 2. Inserción de Restaurantes reales de Ibarra, Imbabura, Ecuador.
        //    Imágenes: URLs verificadas de Unsplash/Pexels (libres de uso).
        var estelita = new Restaurant { Name = "La Estelita", Description = "Restaurante mirador con la mejor vista de Ibarra y la laguna de Yahuarcocha. Cocina de autor con ingredientes de la sierra.", Address = "Km 5 vía a Yuracrucito, mirador de Yahuarcocha, Ibarra", Phone = "062951190", Category = "Gourmet", Latitude = 0.3672, Longitude = -78.0958, ImagePath = "/images/catalog/u1559339352-11d035aa65de-1200.jpg", OpeningHour = new TimeSpan(12, 0, 0), ClosingHour = new TimeSpan(22, 0, 0), IsActive = true };
        var casona = new Restaurant { Name = "La Casona Ibarreña", Description = "Comida típica de la sierra en una casona del centro histórico: hornado, llapingachos y fritada de tradición.", Address = "Bolívar 7-59 y Pedro Moncayo, centro histórico, Ibarra", Phone = "062958721", Category = "Comida Típica", Latitude = 0.3496, Longitude = -78.1195, ImagePath = "/images/catalog/u1552566626-52f8b828add9-1200.jpg", OpeningHour = new TimeSpan(9, 0, 0), ClosingHour = new TimeSpan(21, 0, 0), IsActive = true };
        var ajavi = new Restaurant { Name = "Hostería Ajaví", Description = "Restaurante de la tradicional Hostería Ajaví: cocina internacional, pizzas artesanales y pastas en ambiente familiar.", Address = "Av. Mariano Acosta 16-38, Ibarra", Phone = "062955221", Category = "Internacional", Latitude = 0.3438, Longitude = -78.129, ImagePath = "/images/catalog/u1517248135467-4c7edcad34c4-1200.jpg", OpeningHour = new TimeSpan(7, 0, 0), ClosingHour = new TimeSpan(22, 30, 0), IsActive = true };
        var porton = new Restaurant { Name = "El Portón", Description = "Sabores ibarreños en un patio tradicional: hornado, carne colorada y canelazo.", Address = "Pedro Moncayo 4-26 y Sucre, Ibarra", Phone = "062609431", Category = "Comida Típica", Latitude = 0.3512, Longitude = -78.1178, ImagePath = "/images/catalog/u1555396273-367ea4eb4db5-1200.jpg", OpeningHour = new TimeSpan(10, 0, 0), ClosingHour = new TimeSpan(21, 0, 0), IsActive = true };
        var choza = new Restaurant { Name = "La Choza", Description = "Asados al carbón, pollos al horno y platos criollos en un ambiente campestre.", Address = "Av. Teodoro Gómez de la Torre 9-45, Ibarra", Phone = "062640173", Category = "Asados", Latitude = 0.3446, Longitude = -78.1226, ImagePath = "/images/catalog/u1521017432531-fbd92d768814-1200.jpg", OpeningHour = new TimeSpan(11, 0, 0), ClosingHour = new TimeSpan(21, 30, 0), IsActive = true };
        var rosalia = new Restaurant { Name = "Helados de Paila Rosalía Suárez", Description = "La heladería más tradicional de Ibarra, desde 1896: helados de paila batidos a mano en bronce sobre hielo.", Address = "Olmedo 7-79 y Oviedo, Ibarra", Phone = "062958778", Category = "Heladería y Postres", Latitude = 0.3507, Longitude = -78.1198, ImagePath = "/images/catalog/u1516559828984-fb3b99548b21-1200.jpg", OpeningHour = new TimeSpan(8, 0, 0), ClosingHour = new TimeSpan(19, 30, 0), IsActive = true };
        var cafeRio = new Restaurant { Name = "Café Río", Description = "Cafetería junto al río Tahuando: café de Imbabura, hamburguesas y postres caseros.", Address = "Av. 17 de Julio y Aurelio Espinosa Pólit, El Olivo, Ibarra", Phone = "0998214567", Category = "Cafetería", Latitude = 0.3572, Longitude = -78.1103, ImagePath = "/images/catalog/u1554118811-1e0d58224f24-1200.jpg", OpeningHour = new TimeSpan(7, 30, 0), ClosingHour = new TimeSpan(20, 30, 0), IsActive = true };
        var manabita = new Restaurant { Name = "Rincón Manabita", Description = "Auténtico sabor manabita: ceviches, encebollados y mariscos frescos todos los días.", Address = "Av. Pérez Guerrero 6-54 y Bolívar, Ibarra", Phone = "0991483920", Category = "Mariscos", Latitude = 0.3489, Longitude = -78.1211, ImagePath = "/images/catalog/u1615141982883-c7ad0e69fd62-1200.jpg", OpeningHour = new TimeSpan(8, 0, 0), ClosingHour = new TimeSpan(17, 0, 0), IsActive = true };
        var fogon = new Restaurant { Name = "El Fogón Costeño", Description = "Cocina de la costa ecuatoriana: encocados, arroces marineros y camarones con sazón esmeraldeña.", Address = "Av. Jaime Rivadeneira 5-70 y Oviedo, Ibarra", Phone = "0984012776", Category = "Mariscos", Latitude = 0.3524, Longitude = -78.1249, ImagePath = "/images/catalog/u1535399831218-d5bd36d1a6b3-1200.jpg", OpeningHour = new TimeSpan(9, 0, 0), ClosingHour = new TimeSpan(18, 0, 0), IsActive = true };
        var parrilla = new Restaurant { Name = "La Parrilla de Ibarra", Description = "Cortes a la parrilla, costillas BBQ y pollos a la brasa: carbón encendido todos los días.", Address = "Av. Cristóbal de Troya 10-15, Ibarra", Phone = "0996305181", Category = "Parrilla", Latitude = 0.3568, Longitude = -78.1247, ImagePath = "/images/catalog/p37069415-1200.jpg", OpeningHour = new TimeSpan(12, 0, 0), ClosingHour = new TimeSpan(22, 30, 0), IsActive = true };

        var restaurantes = new List<Restaurant> { estelita, casona, ajavi, porton, choza, rosalia, cafeRio, manabita, fogon, parrilla };
        context.Restaurants.AddRange(restaurantes);

        // Guardamos para obtener los Ids generados
        context.SaveChanges();

        // 3. Productos por restaurante (8-15 por local, sin repetidos)
        var productos = new List<Products>();

        // 1. La Estelita
        productos.Add(new Products { Name = "Lomo Fino a la Pimienta", Description = "Medallón de lomo fino en salsa de pimienta verde, acompañado de papas rústicas.", RestaurantId = estelita.Id, Category = "Carnes", Price = 14.50, ImagePath = "/images/catalog/u1600891964092-4316c288032e-800.jpg", Available = true });
        productos.Add(new Products { Name = "Trucha a la Plancha", Description = "Filete de trucha de la sierra a la plancha sobre ensalada fresca de la huerta.", RestaurantId = estelita.Id, Category = "Pescados", Price = 11.90, ImagePath = "/images/catalog/u1519708227418-c8fd9a32b7a2-800.jpg", Available = true });
        productos.Add(new Products { Name = "Filete de Trucha sobre Arroz", Description = "Filete dorado servido sobre arroz cremoso de hierbas con reducción de la casa.", RestaurantId = estelita.Id, Category = "Pescados", Price = 12.50, ImagePath = "/images/catalog/u1580476262798-bddd9f4b7369-800.jpg", Available = true });
        productos.Add(new Products { Name = "Ensalada César con Pollo", Description = "Lechugas frescas, pollo grillado, crotones y aderezo césar artesanal.", RestaurantId = estelita.Id, Category = "Ensaladas", Price = 7.50, ImagePath = "/images/catalog/u1546069901-ba9599a7e63c-800.jpg", Available = true });
        productos.Add(new Products { Name = "Langostinos a la Plancha", Description = "Langostinos enteros a la plancha con mantequilla de ajo y limón.", RestaurantId = estelita.Id, Category = "Mariscos", Price = 16.50, ImagePath = "/images/catalog/u1559742811-822873691df8-800.jpg", Available = true });
        productos.Add(new Products { Name = "Cheesecake de Mora", Description = "Cheesecake horneado con salsa de mora de Imbabura.", RestaurantId = estelita.Id, Category = "Postres", Price = 5.50, ImagePath = "/images/catalog/u1533134242443-d4fd215305ad-800.jpg", Available = true });
        productos.Add(new Products { Name = "Copa de Helado con Caramelo", Description = "Helado artesanal bañado en salsa de caramelo con barquillo.", RestaurantId = estelita.Id, Category = "Postres", Price = 4.90, ImagePath = "/images/catalog/u1551024506-0bccd828d307-800.jpg", Available = true });
        productos.Add(new Products { Name = "Limonada de Hierbabuena", Description = "Limonada natural con hierbabuena fresca y hielo.", RestaurantId = estelita.Id, Category = "Bebidas", Price = 2.90, ImagePath = "/images/catalog/u1621263764928-df1444c5e859-800.jpg", Available = true });

        // 2. La Casona Ibarreña
        productos.Add(new Products { Name = "Hornado Pastuso", Description = "Hornado jugoso con tortillas de papa, mote y ají de chochos.", RestaurantId = casona.Id, Category = "Comida Típica", Price = 8.50, ImagePath = "/images/catalog/p36850055-800.jpg", Available = true });
        productos.Add(new Products { Name = "Llapingachos con Chorizo", Description = "Tortillas de papa doradas en tiesto con chorizo, huevo frito y ensalada.", RestaurantId = casona.Id, Category = "Comida Típica", Price = 6.50, ImagePath = "/images/catalog/p17091787-800.jpg", Available = true });
        productos.Add(new Products { Name = "Fritada de la Casona", Description = "Fritada dorada en paila de bronce, con mote, maduro y encurtido.", RestaurantId = casona.Id, Category = "Comida Típica", Price = 8.00, ImagePath = "/images/catalog/p34837746-800.jpg", Available = true });
        productos.Add(new Products { Name = "Empanadas de Viento (3 und.)", Description = "Empanadas crocantes de queso espolvoreadas con azúcar.", RestaurantId = casona.Id, Category = "Comida Típica", Price = 2.50, ImagePath = "/images/catalog/u1673716575331-60b042589b8d-800.jpg", Available = true });
        productos.Add(new Products { Name = "Caldo de Gallina Criolla", Description = "Caldo sustancioso de gallina de campo con papa y cilantro.", RestaurantId = casona.Id, Category = "Sopas", Price = 4.50, ImagePath = "/images/catalog/u1603105037880-880cd4edfb0d-800.jpg", Available = true });
        productos.Add(new Products { Name = "Seco de Chivo", Description = "Estofado tradicional de chivo en salsa de cerveza y naranjilla, con arroz amarillo.", RestaurantId = casona.Id, Category = "Comida Típica", Price = 7.50, ImagePath = "/images/catalog/u1565557623262-b51c2513a641-800.jpg", Available = true });
        productos.Add(new Products { Name = "Bandeja Típica Casona", Description = "Degustación de tortillas, mote, tostado, empanada y ají de la casa.", RestaurantId = casona.Id, Category = "Comida Típica", Price = 10.50, ImagePath = "/images/catalog/p32921658-800.jpg", Available = true });
        productos.Add(new Products { Name = "Locro de Papa con Aguacate", Description = "Locro cremoso de papa chola con queso y aguacate.", RestaurantId = casona.Id, Category = "Sopas", Price = 4.00, ImagePath = "/images/catalog/u1613844237701-8f3664fc2eff-800.jpg", Available = true });
        productos.Add(new Products { Name = "Torta de Tres Leches", Description = "Bizcocho bañado en tres leches con crema y frutilla.", RestaurantId = casona.Id, Category = "Postres", Price = 4.50, ImagePath = "/images/catalog/u1641848421644-a1603f016f51-800.jpg", Available = true });
        productos.Add(new Products { Name = "Flan de Caramelo", Description = "Flan casero de vainilla con caramelo dorado.", RestaurantId = casona.Id, Category = "Postres", Price = 3.50, ImagePath = "/images/catalog/u1780798465831-de1e4d58ee46-800.jpg", Available = true });
        productos.Add(new Products { Name = "Batido de Mora", Description = "Batido cremoso de mora andina con leche.", RestaurantId = casona.Id, Category = "Bebidas", Price = 2.50, ImagePath = "/images/catalog/u1553530666-ba11a7da3888-800.jpg", Available = true });

        // 3. Hostería Ajaví
        productos.Add(new Products { Name = "Corvina a la Plancha", Description = "Filete de corvina a la plancha con vegetales salteados.", RestaurantId = ajavi.Id, Category = "Pescados", Price = 10.90, ImagePath = "/images/catalog/u1467003909585-2f8a72700288-800.jpg", Available = true });
        productos.Add(new Products { Name = "Espagueti a la Boloñesa", Description = "Pasta con salsa boloñesa de carne cocinada a fuego lento.", RestaurantId = ajavi.Id, Category = "Pastas", Price = 8.50, ImagePath = "/images/catalog/u1598866594230-a7c12756260f-800.jpg", Available = true });
        productos.Add(new Products { Name = "Fettuccine con Lomo Saltado", Description = "Fettuccine salteado con tiras de lomo y champiñones.", RestaurantId = ajavi.Id, Category = "Pastas", Price = 9.50, ImagePath = "/images/catalog/u1551183053-bf91a1d81141-800.jpg", Available = true });
        productos.Add(new Products { Name = "Penne en Salsa Pomodoro", Description = "Penne al dente en salsa pomodoro con parmesano.", RestaurantId = ajavi.Id, Category = "Pastas", Price = 7.90, ImagePath = "/images/catalog/u1621996346565-e3dbc646d9a9-800.jpg", Available = true });
        productos.Add(new Products { Name = "Pizza Margarita", Description = "Salsa de tomate, mozzarella y albahaca fresca.", RestaurantId = ajavi.Id, Category = "Pizzas", Price = 8.90, ImagePath = "/images/catalog/u1574071318508-1cdbab80d002-800.jpg", Available = true });
        productos.Add(new Products { Name = "Pizza Pepperoni", Description = "Mozzarella y doble porción de pepperoni.", RestaurantId = ajavi.Id, Category = "Pizzas", Price = 10.50, ImagePath = "/images/catalog/u1604382354936-07c5d9983bd3-800.jpg", Available = true });
        productos.Add(new Products { Name = "Pizza Cuatro Quesos", Description = "Mozzarella, cheddar, parmesano y queso azul.", RestaurantId = ajavi.Id, Category = "Pizzas", Price = 11.50, ImagePath = "/images/catalog/u1513104890138-7c749659a591-800.jpg", Available = true });
        productos.Add(new Products { Name = "Pizza Pollo BBQ", Description = "Pollo desmechado, cebolla morada y salsa BBQ.", RestaurantId = ajavi.Id, Category = "Pizzas", Price = 11.90, ImagePath = "/images/catalog/u1565299624946-b28f40a0ae38-800.jpg", Available = true });
        productos.Add(new Products { Name = "Club Sánduche Ajaví", Description = "Triple piso con pollo, jamón, queso, tocino y papas.", RestaurantId = ajavi.Id, Category = "Sánduches", Price = 6.50, ImagePath = "/images/catalog/u1528735602780-2552fd46c7af-800.jpg", Available = true });
        productos.Add(new Products { Name = "Ensalada de la Huerta", Description = "Mix de vegetales frescos, palta y garbanzos con vinagreta.", RestaurantId = ajavi.Id, Category = "Ensaladas", Price = 5.90, ImagePath = "/images/catalog/u1512621776951-a57141f2eefd-800.jpg", Available = true });
        productos.Add(new Products { Name = "Pastel de Chocolate", Description = "Porción de torta húmeda de chocolate con ganache.", RestaurantId = ajavi.Id, Category = "Postres", Price = 4.50, ImagePath = "/images/catalog/u1578985545062-69928b1d9587-800.jpg", Available = true });
        productos.Add(new Products { Name = "Agua Mineral", Description = "Agua mineral con o sin gas (500 ml).", RestaurantId = ajavi.Id, Category = "Bebidas", Price = 2.00, ImagePath = "/images/catalog/u1548839140-29a749e1cf4d-800.jpg", Available = true });

        // 4. El Portón
        productos.Add(new Products { Name = "Hornado Ibarreño", Description = "Hornado de chancho al horno de leña con mote y tortillas.", RestaurantId = porton.Id, Category = "Comida Típica", Price = 9.00, ImagePath = "/images/catalog/p36850029-800.jpg", Available = true });
        productos.Add(new Products { Name = "Carne Colorada del Portón", Description = "Carne colorada con achiote, papas cocinadas, maduro y encurtido.", RestaurantId = porton.Id, Category = "Comida Típica", Price = 9.50, ImagePath = "/images/catalog/u1628294895950-9805252327bc-800.jpg", Available = true });
        productos.Add(new Products { Name = "Patacones con Chicharrón", Description = "Canastas de patacón rellenas de chicharrón crocante.", RestaurantId = porton.Id, Category = "Comida Típica", Price = 5.50, ImagePath = "/images/catalog/p37121088-800.jpg", Available = true });
        productos.Add(new Products { Name = "Seco de Pollo Criollo", Description = "Pollo criollo guisado en salsa de cilantro con arroz.", RestaurantId = porton.Id, Category = "Comida Típica", Price = 6.90, ImagePath = "/images/catalog/u1541518763669-27fef04b14ea-800.jpg", Available = true });
        productos.Add(new Products { Name = "Choclo Asado con Queso", Description = "Choclo serrano asado al carbón con queso y mapahuira.", RestaurantId = porton.Id, Category = "Comida Típica", Price = 3.50, ImagePath = "/images/catalog/u1470119693884-47d3a1d1f180-800.jpg", Available = true });
        productos.Add(new Products { Name = "Chifles con Ají", Description = "Porción de chifles crocantes con ají de la casa.", RestaurantId = porton.Id, Category = "Entradas", Price = 2.50, ImagePath = "/images/catalog/u1528751014936-863e6e7a319c-800.jpg", Available = true });
        productos.Add(new Products { Name = "Crema de Tomate", Description = "Crema de tomate riñón con crotones y albahaca.", RestaurantId = porton.Id, Category = "Sopas", Price = 3.90, ImagePath = "/images/catalog/u1547592166-23ac45744acd-800.jpg", Available = true });
        productos.Add(new Products { Name = "Borrego Asado con Papas", Description = "Borrego asado a fuego lento con puré rústico.", RestaurantId = porton.Id, Category = "Carnes", Price = 9.90, ImagePath = "/images/catalog/p38330332-800.jpg", Available = true });
        productos.Add(new Products { Name = "Mousse de Frutilla", Description = "Mousse suave de frutilla con crema batida.", RestaurantId = porton.Id, Category = "Postres", Price = 3.50, ImagePath = "/images/catalog/u1488477181946-6428a0291777-800.jpg", Available = true });
        productos.Add(new Products { Name = "Jugo de Naranja", Description = "Jugo de naranja recién exprimido.", RestaurantId = porton.Id, Category = "Bebidas", Price = 2.50, ImagePath = "/images/catalog/u1600271886742-f049cd451bba-800.jpg", Available = true });
        productos.Add(new Products { Name = "Canelazo Tradicional", Description = "Bebida caliente de canela y naranjilla, típica de la sierra.", RestaurantId = porton.Id, Category = "Bebidas", Price = 3.00, ImagePath = "/images/catalog/u1576092768241-dec231879fc3-800.jpg", Available = true });

        // 5. La Choza
        productos.Add(new Products { Name = "Pollo Asado con Papas", Description = "Pollo dorado al horno de leña con papas y ají criollo.", RestaurantId = choza.Id, Category = "Pollo", Price = 8.90, ImagePath = "/images/catalog/u1598103442097-8b74394b95c6-800.jpg", Available = true });
        productos.Add(new Products { Name = "Pollo al Horno con Ensalada", Description = "Presas de pollo al horno con ensalada fresca y granada.", RestaurantId = choza.Id, Category = "Pollo", Price = 8.50, ImagePath = "/images/catalog/u1610057099431-d73a1c9d2f2f-800.jpg", Available = true });
        productos.Add(new Products { Name = "Gallina al Horno Criolla", Description = "Gallina de campo horneada entera, para compartir.", RestaurantId = choza.Id, Category = "Pollo", Price = 9.50, ImagePath = "/images/catalog/u1594221708779-94832f4320d1-800.jpg", Available = true });
        productos.Add(new Products { Name = "Carne Asada de Res", Description = "Corte de res asado al carbón, jugoso y ahumado.", RestaurantId = choza.Id, Category = "Carnes", Price = 8.50, ImagePath = "/images/catalog/u1529692236671-f1f6cf9683ba-800.jpg", Available = true });
        productos.Add(new Products { Name = "Chicharrón con Yuca", Description = "Chicharrón reventado con yucas doradas y ají.", RestaurantId = choza.Id, Category = "Comida Típica", Price = 7.50, ImagePath = "/images/catalog/p6697492-800.jpg", Available = true });
        productos.Add(new Products { Name = "Chaulafán Criollo", Description = "Arroz salteado con pollo, chancho, huevo y verduras.", RestaurantId = choza.Id, Category = "Comida Típica", Price = 6.50, ImagePath = "/images/catalog/u1603133872878-684f208fb84b-800.jpg", Available = true });
        productos.Add(new Products { Name = "Porción de Arroz", Description = "Porción de arroz graneado.", RestaurantId = choza.Id, Category = "Acompañamientos", Price = 2.00, ImagePath = "/images/catalog/u1516684732162-798a0062be99-800.jpg", Available = true });
        productos.Add(new Products { Name = "Papas Rústicas", Description = "Papas fritas rústicas con perejil y sal de ajo.", RestaurantId = choza.Id, Category = "Acompañamientos", Price = 2.50, ImagePath = "/images/catalog/u1630431341973-02e1b662ec35-800.jpg", Available = true });
        productos.Add(new Products { Name = "Hot Dog de La Choza", Description = "Hot dog con salsas de la casa y papitas al hilo.", RestaurantId = choza.Id, Category = "Snacks", Price = 3.00, ImagePath = "/images/catalog/u1619740455993-9e612b1af08a-800.jpg", Available = true });
        productos.Add(new Products { Name = "Batido de Frutilla", Description = "Batido espeso de frutilla con leche entera.", RestaurantId = choza.Id, Category = "Bebidas", Price = 2.90, ImagePath = "/images/catalog/u1579954115545-a95591f28bfc-800.jpg", Available = true });

        // 6. Helados de Paila Rosalía Suárez
        productos.Add(new Products { Name = "Helado de Paila (2 sabores)", Description = "Helado artesanal batido en paila de bronce; elige dos sabores.", RestaurantId = rosalia.Id, Category = "Postres", Price = 2.50, ImagePath = "/images/catalog/u1560008581-09826d1de69e-800.jpg", Available = true });
        productos.Add(new Products { Name = "Cono Artesanal de Frutas", Description = "Cono con helado de paila de frutas andinas.", RestaurantId = rosalia.Id, Category = "Postres", Price = 2.00, ImagePath = "/images/catalog/u1501443762994-82bd5dace89a-800.jpg", Available = true });
        productos.Add(new Products { Name = "Helado en Cono Clásico", Description = "Cono clásico de un sabor: mora, coco, guanábana o taxo.", RestaurantId = rosalia.Id, Category = "Postres", Price = 2.00, ImagePath = "/images/catalog/u1497034825429-c343d7c6a68f-800.jpg", Available = true });
        productos.Add(new Products { Name = "Copa Rosalía", Description = "Copa especial con tres sabores, crema, galleta y salsa de chocolate.", RestaurantId = rosalia.Id, Category = "Postres", Price = 4.50, ImagePath = "/images/catalog/u1563805042-7684c019e1cb-800.jpg", Available = true });
        productos.Add(new Products { Name = "Paleta Artesanal de Mora", Description = "Paleta helada de mora natural.", RestaurantId = rosalia.Id, Category = "Postres", Price = 2.00, ImagePath = "/images/catalog/u1488900128323-21503983a07e-800.jpg", Available = true });
        productos.Add(new Products { Name = "Milkshake de Chocolate", Description = "Batido helado de chocolate con crema y galleta.", RestaurantId = rosalia.Id, Category = "Bebidas", Price = 3.90, ImagePath = "/images/catalog/u1572490122747-3968b75cc699-800.jpg", Available = true });
        productos.Add(new Products { Name = "Malteada de Vainilla y Chocolate", Description = "Malteada cremosa de vainilla con hilos de chocolate.", RestaurantId = rosalia.Id, Category = "Bebidas", Price = 3.50, ImagePath = "/images/catalog/u1553787499-6f9133860278-800.jpg", Available = true });
        productos.Add(new Products { Name = "Ensalada de Frutas con Crema", Description = "Frutas frescas picadas con crema y helado de paila.", RestaurantId = rosalia.Id, Category = "Postres", Price = 3.50, ImagePath = "/images/catalog/u1490474418585-ba9bad8fd0ea-800.jpg", Available = true });
        productos.Add(new Products { Name = "Waffles con Helado", Description = "Waffles calientes con helado de paila y miel.", RestaurantId = rosalia.Id, Category = "Postres", Price = 4.50, ImagePath = "/images/catalog/u1562376552-0d160a2f238d-800.jpg", Available = true });
        productos.Add(new Products { Name = "Pastel de Frambuesa", Description = "Porción de pastel de vainilla con crema y frambuesas.", RestaurantId = rosalia.Id, Category = "Postres", Price = 4.00, ImagePath = "/images/catalog/u1565958011703-44f9829ba187-800.jpg", Available = true });
        productos.Add(new Products { Name = "Brownie de Chocolate", Description = "Brownie húmedo de chocolate con nueces.", RestaurantId = rosalia.Id, Category = "Postres", Price = 3.90, ImagePath = "/images/catalog/u1606313564200-e75d5e30476c-800.jpg", Available = true });
        productos.Add(new Products { Name = "Tarta de Chocolate", Description = "Tarta fina de chocolate oscuro con higos.", RestaurantId = rosalia.Id, Category = "Postres", Price = 4.20, ImagePath = "/images/catalog/u1626803775151-61d756612f97-800.jpg", Available = true });

        // 7. Café Río
        productos.Add(new Products { Name = "Café Americano", Description = "Café de altura de Imbabura, filtrado.", RestaurantId = cafeRio.Id, Category = "Bebidas Calientes", Price = 2.00, ImagePath = "/images/catalog/u1517578239113-b03992dcdd25-800.jpg", Available = true });
        productos.Add(new Products { Name = "Capuchino Clásico", Description = "Espresso con leche vaporizada y espuma cremosa.", RestaurantId = cafeRio.Id, Category = "Bebidas Calientes", Price = 2.50, ImagePath = "/images/catalog/u1572442388796-11668a67e53d-800.jpg", Available = true });
        productos.Add(new Products { Name = "Café Latte", Description = "Espresso suave con leche cremada y arte latte.", RestaurantId = cafeRio.Id, Category = "Bebidas Calientes", Price = 2.80, ImagePath = "/images/catalog/u1541167760496-1628856ab772-800.jpg", Available = true });
        productos.Add(new Products { Name = "Café con Leche", Description = "Café pasado con leche caliente, estilo tradicional.", RestaurantId = cafeRio.Id, Category = "Bebidas Calientes", Price = 2.20, ImagePath = "/images/catalog/u1509042239860-f550ce710b93-800.jpg", Available = true });
        productos.Add(new Products { Name = "Café Mocca", Description = "Espresso con chocolate y leche vaporizada.", RestaurantId = cafeRio.Id, Category = "Bebidas Calientes", Price = 3.00, ImagePath = "/images/catalog/u1512568400610-62da28bc8a13-800.jpg", Available = true });
        productos.Add(new Products { Name = "Chocolate Caliente con Galletas", Description = "Chocolate espeso acompañado de galletas artesanales.", RestaurantId = cafeRio.Id, Category = "Bebidas Calientes", Price = 2.80, ImagePath = "/images/catalog/u1544787219-7f47ccb76574-800.jpg", Available = true });
        productos.Add(new Products { Name = "Croissant de Mantequilla", Description = "Croissant hojaldrado horneado cada mañana.", RestaurantId = cafeRio.Id, Category = "Panadería", Price = 2.20, ImagePath = "/images/catalog/u1555507036-ab1f4038808a-800.jpg", Available = true });
        productos.Add(new Products { Name = "Hamburguesa Crispy de Pollo", Description = "Pollo crocante, col morada y salsa especial en pan brioche.", RestaurantId = cafeRio.Id, Category = "Hamburguesas", Price = 5.50, ImagePath = "/images/catalog/u1481070555726-e2fe8357725c-800.jpg", Available = true });
        productos.Add(new Products { Name = "Hamburguesa Clásica con Papas", Description = "Carne de res, queso y vegetales, con papas fritas.", RestaurantId = cafeRio.Id, Category = "Hamburguesas", Price = 6.50, ImagePath = "/images/catalog/u1541544741938-0af808871cc0-800.jpg", Available = true });
        productos.Add(new Products { Name = "Hamburguesa Doble Río", Description = "Doble carne, doble queso y tocino.", RestaurantId = cafeRio.Id, Category = "Hamburguesas", Price = 7.50, ImagePath = "/images/catalog/u1550547660-d9450f859349-800.jpg", Available = true });
        productos.Add(new Products { Name = "Tostadas Francesas", Description = "Tostadas francesas con banano, frutos rojos y miel.", RestaurantId = cafeRio.Id, Category = "Postres", Price = 4.50, ImagePath = "/images/catalog/u1484723091739-30a097e8f929-800.jpg", Available = true });
        productos.Add(new Products { Name = "Pancakes con Miel", Description = "Torre de pancakes esponjosos con miel de la casa.", RestaurantId = cafeRio.Id, Category = "Postres", Price = 4.00, ImagePath = "/images/catalog/u1567620905732-2d1ec7ab7445-800.jpg", Available = true });
        productos.Add(new Products { Name = "Cheesecake del Río", Description = "Cheesecake clásico horneado, textura sedosa.", RestaurantId = cafeRio.Id, Category = "Postres", Price = 4.50, ImagePath = "/images/catalog/u1524351199678-941a58a3df50-800.jpg", Available = true });

        // 8. Rincón Manabita
        productos.Add(new Products { Name = "Ceviche de Pescado", Description = "Ceviche de pescado fresco curtido en limón con cebolla paiteña.", RestaurantId = manabita.Id, Category = "Mariscos", Price = 8.50, ImagePath = "/images/catalog/u1562707774-553917f561df-800.jpg", Available = true });
        productos.Add(new Products { Name = "Ceviche Mixto", Description = "Pescado, camarón y calamar crocante con camote y maíz.", RestaurantId = manabita.Id, Category = "Mariscos", Price = 10.50, ImagePath = "/images/catalog/u1761314037289-a9d6760eb37d-800.jpg", Available = true });
        productos.Add(new Products { Name = "Ceviche de Camarón con Chifles", Description = "Ceviche de camarón estilo manabita acompañado de chifles.", RestaurantId = manabita.Id, Category = "Mariscos", Price = 9.50, ImagePath = "/images/catalog/p38330330-800.jpg", Available = true });
        productos.Add(new Products { Name = "Ceviche Manabita con Maduro", Description = "Ceviche tradicional servido con maduro asado.", RestaurantId = manabita.Id, Category = "Mariscos", Price = 10.00, ImagePath = "/images/catalog/p38347627-800.jpg", Available = true });
        productos.Add(new Products { Name = "Encebollado de Albacora", Description = "Encebollado espeso de albacora con yuca y cebolla curtida.", RestaurantId = manabita.Id, Category = "Sopas", Price = 5.50, ImagePath = "/images/catalog/p37387553-800.jpg", Available = true });
        productos.Add(new Products { Name = "Pescado Apanado", Description = "Dedos de pescado apanados con salsa tártara.", RestaurantId = manabita.Id, Category = "Mariscos", Price = 8.90, ImagePath = "/images/catalog/u1562967914-608f82629710-800.jpg", Available = true });
        productos.Add(new Products { Name = "Tallarín de Mariscos", Description = "Tallarín salteado con mix de mariscos frescos.", RestaurantId = manabita.Id, Category = "Mariscos", Price = 10.90, ImagePath = "/images/catalog/u1595295333158-4742f28fbd85-800.jpg", Available = true });
        productos.Add(new Products { Name = "Jugo Natural del Día", Description = "Jugo de fruta fresca de temporada.", RestaurantId = manabita.Id, Category = "Bebidas", Price = 2.50, ImagePath = "/images/catalog/u1613478223719-2ab802602423-800.jpg", Available = true });

        // 9. El Fogón Costeño
        productos.Add(new Products { Name = "Encocado de Camarón", Description = "Camarones en salsa de coco esmeraldeña con arroz blanco.", RestaurantId = fogon.Id, Category = "Mariscos", Price = 11.90, ImagePath = "/images/catalog/u1559847844-5315695dadae-800.jpg", Available = true });
        productos.Add(new Products { Name = "Arroz Marinero", Description = "Arroz costeño con camarón, calamar, concha y mejillones.", RestaurantId = fogon.Id, Category = "Mariscos", Price = 12.50, ImagePath = "/images/catalog/u1534080564583-6be75777b70a-800.jpg", Available = true });
        productos.Add(new Products { Name = "Camarones al Ajillo", Description = "Camarones salteados en mantequilla de ajo y hierbas.", RestaurantId = fogon.Id, Category = "Mariscos", Price = 10.90, ImagePath = "/images/catalog/u1625943553852-781c6dd46faa-800.jpg", Available = true });
        productos.Add(new Products { Name = "Ceviche del Fogón", Description = "Ceviche de pescado con camote y maíz tostado.", RestaurantId = fogon.Id, Category = "Mariscos", Price = 9.00, ImagePath = "/images/catalog/u1761314036709-f6f68a3d7cf1-800.jpg", Available = true });
        productos.Add(new Products { Name = "Camarones al Vapor", Description = "Camarones enteros al vapor con limón (porción).", RestaurantId = fogon.Id, Category = "Mariscos", Price = 11.50, ImagePath = "/images/catalog/u1565680018434-b513d5e5fd47-800.jpg", Available = true });
        productos.Add(new Products { Name = "Fideos Salteados con Camarón", Description = "Fideos al wok con camarón y vegetales.", RestaurantId = fogon.Id, Category = "Mariscos", Price = 9.90, ImagePath = "/images/catalog/u1563379926898-05f4575a45d8-800.jpg", Available = true });
        productos.Add(new Products { Name = "Combo Frito Costeño", Description = "Pollo frito, maduro y papas al estilo de la costa.", RestaurantId = fogon.Id, Category = "Pollo", Price = 7.50, ImagePath = "/images/catalog/p37324427-800.jpg", Available = true });
        productos.Add(new Products { Name = "Limonada Imperial", Description = "Limonada helada con hierbabuena.", RestaurantId = fogon.Id, Category = "Bebidas", Price = 3.00, ImagePath = "/images/catalog/u1599021456807-25db0f974333-800.jpg", Available = true });
        productos.Add(new Products { Name = "Cola Personal", Description = "Gaseosa personal bien fría.", RestaurantId = fogon.Id, Category = "Bebidas", Price = 2.00, ImagePath = "/images/catalog/u1554866585-cd94860890b7-800.jpg", Available = true });

        // 10. La Parrilla de Ibarra
        productos.Add(new Products { Name = "Parrillada Ibarreña (2 personas)", Description = "Res, cerdo, pollo y chorizo a la parrilla con papas y ají.", RestaurantId = parrilla.Id, Category = "Carnes", Price = 19.90, ImagePath = "/images/catalog/u1555939594-58d7cb561ad1-800.jpg", Available = true });
        productos.Add(new Products { Name = "Costillas BBQ", Description = "Costillar bañado en salsa BBQ, cocción lenta.", RestaurantId = parrilla.Id, Category = "Carnes", Price = 12.50, ImagePath = "/images/catalog/u1544025162-d76694265947-800.jpg", Available = true });
        productos.Add(new Products { Name = "Bife de Res a la Parrilla", Description = "Bife jugoso al carbón, término a elección.", RestaurantId = parrilla.Id, Category = "Carnes", Price = 11.90, ImagePath = "/images/catalog/u1558030006-450675393462-800.jpg", Available = true });
        productos.Add(new Products { Name = "Alitas BBQ (8 und.)", Description = "Alitas glaseadas en salsa BBQ con apio.", RestaurantId = parrilla.Id, Category = "Pollo", Price = 6.50, ImagePath = "/images/catalog/u1624726175512-19b9baf9fbd1-800.jpg", Available = true });
        productos.Add(new Products { Name = "Alitas Picantes (8 und.)", Description = "Alitas picantes estilo búfalo con salsa de la casa.", RestaurantId = parrilla.Id, Category = "Pollo", Price = 6.90, ImagePath = "/images/catalog/u1527477396000-e27163b481c2-800.jpg", Available = true });
        productos.Add(new Products { Name = "Pollo Frito Crocante", Description = "Presas de pollo frito crocante estilo sureño.", RestaurantId = parrilla.Id, Category = "Pollo", Price = 6.50, ImagePath = "/images/catalog/u1626082927389-6cd097cdc6ec-800.jpg", Available = true });
        productos.Add(new Products { Name = "Pollo Broaster con Papas", Description = "Presa de pollo broaster con papas fritas y consomé.", RestaurantId = parrilla.Id, Category = "Pollo", Price = 6.90, ImagePath = "/images/catalog/p16936595-800.jpg", Available = true });
        productos.Add(new Products { Name = "Pollo a la Brasa Entero", Description = "Pollo entero a la brasa, dorado y jugoso.", RestaurantId = parrilla.Id, Category = "Pollo", Price = 12.90, ImagePath = "/images/catalog/u1606728035253-49e8a23146de-800.jpg", Available = true });
        productos.Add(new Products { Name = "Papas Fritas con Queso", Description = "Papas fritas gratinadas con queso y especias.", RestaurantId = parrilla.Id, Category = "Acompañamientos", Price = 3.50, ImagePath = "/images/catalog/u1573080496219-bb080dd4f877-800.jpg", Available = true });
        productos.Add(new Products { Name = "Ensalada Fresca", Description = "Ensalada de vegetales frescos con vinagreta.", RestaurantId = parrilla.Id, Category = "Ensaladas", Price = 4.50, ImagePath = "/images/catalog/u1540189549336-e6e99c3679fe-800.jpg", Available = true });
        productos.Add(new Products { Name = "Jugo Verde Natural", Description = "Jugo verde de manzana, kiwi y hierbabuena.", RestaurantId = parrilla.Id, Category = "Bebidas", Price = 2.50, ImagePath = "/images/catalog/u1610970881699-44a5587cabec-800.jpg", Available = true });

        context.Products.AddRange(productos);
        context.SaveChanges();
        
        transaction.Commit();
        Console.WriteLine(">>> ResetDatabaseCompletely ejecutado: DB Limpia vía SQL y repoblada con Integridad Transaccional e IDs automáticos. <<<");
    }
    catch (Exception ex)
    {
        transaction.Rollback();
        Console.WriteLine($">>> Fallo al reiniciar base de datos. Rollback ejecutado. Error: {ex.Message} <<<");
        throw;
    }
}
