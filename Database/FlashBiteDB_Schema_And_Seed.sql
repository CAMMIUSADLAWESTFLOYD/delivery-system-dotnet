/* ============================================================================
   FlashBiteDB - Sistema de Delivery de Comida
   Motor: Microsoft SQL Server (compatible con SSMS 18/19/20, SQL Server 2016+)
   Autor: Diseno de base de datos - Arquitectura relacional normalizada (3FN)
   Descripcion: Script unico, idempotente y ejecutable de principio a fin.
                Elimina y recrea la base de datos "FlashBiteDB", crea el
                esquema completo (tablas, llaves, restricciones, indices) y
                la puebla con datos de prueba realistas.
   Uso: Abrir en SSMS y ejecutar completo (F5). No requiere pasos manuales.
   ============================================================================ */

/* ============================================================================
   SECCION 0: CREACION DE LA BASE DE DATOS
   ============================================================================ */
USE master;
GO

IF DB_ID(N'FlashBiteDB') IS NOT NULL
BEGIN
    ALTER DATABASE FlashBiteDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE FlashBiteDB;
END
GO

CREATE DATABASE FlashBiteDB;
GO

ALTER DATABASE FlashBiteDB SET RECOVERY SIMPLE;
GO

USE FlashBiteDB;
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

/* ============================================================================
   SECCION 1: TABLAS DE CATALOGO (LOOKUP TABLES)
   Estas tablas evitan "magic strings" en el resto del modelo y permiten
   agregar nuevos estados/metodos sin alterar la estructura (cumple 3FN:
   ningun atributo dependiente de un valor no clave se repite en otras tablas).
   ============================================================================ */

CREATE TABLE Roles (
    RolID           INT IDENTITY(1,1) NOT NULL,
    NombreRol       NVARCHAR(50)      NOT NULL,
    Descripcion     NVARCHAR(200)     NULL,
    CONSTRAINT PK_Roles PRIMARY KEY (RolID),
    CONSTRAINT UQ_Roles_NombreRol UNIQUE (NombreRol)
);
GO

CREATE TABLE EstadoPedido (
    EstadoPedidoID  INT IDENTITY(1,1) NOT NULL,
    Nombre          NVARCHAR(30)      NOT NULL,
    Descripcion     NVARCHAR(200)     NULL,
    Orden           TINYINT           NOT NULL,
    CONSTRAINT PK_EstadoPedido PRIMARY KEY (EstadoPedidoID),
    CONSTRAINT UQ_EstadoPedido_Nombre UNIQUE (Nombre)
);
GO

CREATE TABLE EstadoDelivery (
    EstadoDeliveryID INT IDENTITY(1,1) NOT NULL,
    Nombre           NVARCHAR(30)      NOT NULL,
    Descripcion      NVARCHAR(200)     NULL,
    CONSTRAINT PK_EstadoDelivery PRIMARY KEY (EstadoDeliveryID),
    CONSTRAINT UQ_EstadoDelivery_Nombre UNIQUE (Nombre)
);
GO

CREATE TABLE MetodosPago (
    MetodoPagoID    INT IDENTITY(1,1) NOT NULL,
    Nombre          NVARCHAR(30)      NOT NULL,
    Descripcion     NVARCHAR(200)     NULL,
    Activo          BIT               NOT NULL CONSTRAINT DF_MetodosPago_Activo DEFAULT (1),
    CONSTRAINT PK_MetodosPago PRIMARY KEY (MetodoPagoID),
    CONSTRAINT UQ_MetodosPago_Nombre UNIQUE (Nombre)
);
GO

CREATE TABLE CategoriasRestaurante (
    CategoriaRestauranteID INT IDENTITY(1,1) NOT NULL,
    Nombre                 NVARCHAR(50)      NOT NULL,
    Descripcion            NVARCHAR(200)     NULL,
    IconoURL               NVARCHAR(255)     NULL,
    CONSTRAINT PK_CategoriasRestaurante PRIMARY KEY (CategoriaRestauranteID),
    CONSTRAINT UQ_CategoriasRestaurante_Nombre UNIQUE (Nombre)
);
GO

CREATE TABLE CategoriasProducto (
    CategoriaProductoID INT IDENTITY(1,1) NOT NULL,
    Nombre               NVARCHAR(50)      NOT NULL,
    Descripcion          NVARCHAR(200)     NULL,
    CONSTRAINT PK_CategoriasProducto PRIMARY KEY (CategoriaProductoID),
    CONSTRAINT UQ_CategoriasProducto_Nombre UNIQUE (Nombre)
);
GO

/* ============================================================================
   SECCION 2: USUARIOS Y SUBTIPOS (Administrador / Cliente / Delivery)
   Patron "tabla base + subtipos": Usuarios centraliza autenticacion
   (correo, hash de password, rol, estado de cuenta). Administradores,
   Clientes y Delivery heredan de Usuarios via UsuarioID (1 a 1), evitando
   duplicar columnas de login y permitiendo login unificado para los 3 roles.
   ============================================================================ */

CREATE TABLE Usuarios (
    UsuarioID           INT IDENTITY(1,1) NOT NULL,
    RolID                INT               NOT NULL,
    Correo               NVARCHAR(150)     NOT NULL,
    ContrasenaHash       VARCHAR(255)      NOT NULL,
    Estado               VARCHAR(20)       NOT NULL CONSTRAINT DF_Usuarios_Estado DEFAULT ('Activo'),
    FechaRegistro        DATETIME2(0)      NOT NULL CONSTRAINT DF_Usuarios_FechaRegistro DEFAULT (SYSDATETIME()),
    FechaActualizacion   DATETIME2(0)      NULL,
    UltimoAcceso         DATETIME2(0)      NULL,
    CONSTRAINT PK_Usuarios PRIMARY KEY (UsuarioID),
    CONSTRAINT UQ_Usuarios_Correo UNIQUE (Correo),
    CONSTRAINT FK_Usuarios_Roles FOREIGN KEY (RolID) REFERENCES Roles (RolID),
    CONSTRAINT CK_Usuarios_Estado CHECK (Estado IN ('Activo','Inactivo','Suspendido'))
);
GO

CREATE TABLE Administradores (
    AdministradorID INT IDENTITY(1,1) NOT NULL,
    UsuarioID        INT               NOT NULL,
    Nombre           NVARCHAR(80)      NOT NULL,
    Apellido         NVARCHAR(80)      NOT NULL,
    Cargo            NVARCHAR(80)      NULL,
    Telefono         VARCHAR(20)       NULL,
    CONSTRAINT PK_Administradores PRIMARY KEY (AdministradorID),
    CONSTRAINT UQ_Administradores_UsuarioID UNIQUE (UsuarioID),
    CONSTRAINT FK_Administradores_Usuarios FOREIGN KEY (UsuarioID) REFERENCES Usuarios (UsuarioID)
);
GO

CREATE TABLE Clientes (
    ClienteID         INT IDENTITY(1,1) NOT NULL,
    UsuarioID          INT               NOT NULL,
    Nombre             NVARCHAR(80)      NOT NULL,
    Apellido           NVARCHAR(80)      NOT NULL,
    Telefono           VARCHAR(20)       NOT NULL,
    FechaNacimiento    DATE              NULL,
    FechaRegistro      DATETIME2(0)      NOT NULL CONSTRAINT DF_Clientes_FechaRegistro DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Clientes PRIMARY KEY (ClienteID),
    CONSTRAINT UQ_Clientes_UsuarioID UNIQUE (UsuarioID),
    CONSTRAINT FK_Clientes_Usuarios FOREIGN KEY (UsuarioID) REFERENCES Usuarios (UsuarioID)
);
GO

CREATE TABLE Vehiculos (
    VehiculoID  INT IDENTITY(1,1) NOT NULL,
    Tipo         NVARCHAR(30)      NOT NULL,
    Marca        NVARCHAR(50)      NULL,
    Modelo       NVARCHAR(50)      NULL,
    Placa        VARCHAR(15)       NULL,
    Color        NVARCHAR(30)      NULL,
    Anio         SMALLINT          NULL,
    CONSTRAINT PK_Vehiculos PRIMARY KEY (VehiculoID),
    CONSTRAINT CK_Vehiculos_Tipo CHECK (Tipo IN ('Moto','Bicicleta','Automovil','A pie')),
    CONSTRAINT CK_Vehiculos_Anio CHECK (Anio IS NULL OR Anio BETWEEN 1990 AND 2100)
);
GO

-- Indice unico filtrado: permite multiples vehiculos sin placa (bicicletas)
-- pero garantiza que ninguna placa registrada se repita.
CREATE UNIQUE INDEX UQ_Vehiculos_Placa ON Vehiculos (Placa) WHERE Placa IS NOT NULL;
GO

CREATE TABLE Delivery (
    DeliveryID             INT IDENTITY(1,1) NOT NULL,
    UsuarioID               INT               NOT NULL,
    Nombre                  NVARCHAR(80)      NOT NULL,
    Apellido                NVARCHAR(80)      NOT NULL,
    Telefono                VARCHAR(20)       NOT NULL,
    VehiculoID              INT               NOT NULL,
    EstadoDeliveryID        INT               NOT NULL,
    CalificacionPromedio    DECIMAL(3,2)      NOT NULL CONSTRAINT DF_Delivery_Calificacion DEFAULT (0),
    FechaRegistro           DATETIME2(0)      NOT NULL CONSTRAINT DF_Delivery_FechaRegistro DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Delivery PRIMARY KEY (DeliveryID),
    CONSTRAINT UQ_Delivery_UsuarioID UNIQUE (UsuarioID),
    CONSTRAINT FK_Delivery_Usuarios FOREIGN KEY (UsuarioID) REFERENCES Usuarios (UsuarioID),
    CONSTRAINT FK_Delivery_Vehiculos FOREIGN KEY (VehiculoID) REFERENCES Vehiculos (VehiculoID),
    CONSTRAINT FK_Delivery_EstadoDelivery FOREIGN KEY (EstadoDeliveryID) REFERENCES EstadoDelivery (EstadoDeliveryID),
    CONSTRAINT CK_Delivery_Calificacion CHECK (CalificacionPromedio BETWEEN 0 AND 5)
);
GO

/* ============================================================================
   SECCION 3: DIRECCIONES DEL CLIENTE (relacion 1 a N: un cliente, muchas direcciones)
   ============================================================================ */

CREATE TABLE Direcciones (
    DireccionID        INT IDENTITY(1,1) NOT NULL,
    ClienteID           INT               NOT NULL,
    Alias               NVARCHAR(50)      NOT NULL CONSTRAINT DF_Direcciones_Alias DEFAULT ('Casa'),
    Calle                NVARCHAR(150)     NOT NULL,
    NumeroExterior      NVARCHAR(20)      NULL,
    Referencia          NVARCHAR(200)     NULL,
    Ciudad               NVARCHAR(80)      NOT NULL,
    Provincia            NVARCHAR(80)      NOT NULL,
    CodigoPostal        VARCHAR(10)       NULL,
    Latitud              DECIMAL(10,7)     NULL,
    Longitud             DECIMAL(10,7)     NULL,
    EsPredeterminada    BIT               NOT NULL CONSTRAINT DF_Direcciones_EsPredeterminada DEFAULT (0),
    CONSTRAINT PK_Direcciones PRIMARY KEY (DireccionID),
    CONSTRAINT FK_Direcciones_Clientes FOREIGN KEY (ClienteID) REFERENCES Clientes (ClienteID)
);
GO

/* ============================================================================
   SECCION 4: RESTAURANTES Y CATALOGO DE PRODUCTOS
   ============================================================================ */

CREATE TABLE Restaurantes (
    RestauranteID           INT IDENTITY(1,1) NOT NULL,
    Nombre                   NVARCHAR(120)     NOT NULL,
    Descripcion              NVARCHAR(500)     NULL,
    CategoriaRestauranteID  INT               NOT NULL,
    Direccion                NVARCHAR(200)     NOT NULL,
    Ciudad                   NVARCHAR(80)      NOT NULL,
    Telefono                 VARCHAR(20)       NOT NULL,
    Correo                   NVARCHAR(150)     NULL,
    LogoURL                  NVARCHAR(255)     NULL,
    HorarioApertura          TIME(0)           NOT NULL,
    HorarioCierre            TIME(0)           NOT NULL,
    Estado                   VARCHAR(20)       NOT NULL CONSTRAINT DF_Restaurantes_Estado DEFAULT ('Pendiente'),
    CalificacionPromedio     DECIMAL(3,2)      NOT NULL CONSTRAINT DF_Restaurantes_Calificacion DEFAULT (0),
    CostoEnvioBase           DECIMAL(10,2)     NOT NULL,
    FechaRegistro             DATETIME2(0)      NOT NULL CONSTRAINT DF_Restaurantes_FechaRegistro DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Restaurantes PRIMARY KEY (RestauranteID),
    CONSTRAINT FK_Restaurantes_CategoriasRestaurante FOREIGN KEY (CategoriaRestauranteID) REFERENCES CategoriasRestaurante (CategoriaRestauranteID),
    CONSTRAINT CK_Restaurantes_Estado CHECK (Estado IN ('Pendiente','Aprobado','Rechazado','Suspendido')),
    CONSTRAINT CK_Restaurantes_Calificacion CHECK (CalificacionPromedio BETWEEN 0 AND 5),
    CONSTRAINT CK_Restaurantes_CostoEnvio CHECK (CostoEnvioBase >= 0)
);
GO

CREATE TABLE HorariosRestaurante (
    HorarioID       INT IDENTITY(1,1) NOT NULL,
    RestauranteID    INT               NOT NULL,
    DiaSemana        TINYINT           NOT NULL,
    HoraApertura     TIME(0)           NOT NULL,
    HoraCierre       TIME(0)           NOT NULL,
    CONSTRAINT PK_HorariosRestaurante PRIMARY KEY (HorarioID),
    CONSTRAINT FK_HorariosRestaurante_Restaurantes FOREIGN KEY (RestauranteID) REFERENCES Restaurantes (RestauranteID),
    CONSTRAINT UQ_HorariosRestaurante_RestDia UNIQUE (RestauranteID, DiaSemana),
    CONSTRAINT CK_HorariosRestaurante_Dia CHECK (DiaSemana BETWEEN 1 AND 7)
);
GO

CREATE TABLE Productos (
    ProductoID                INT IDENTITY(1,1) NOT NULL,
    RestauranteID              INT               NOT NULL,
    CategoriaProductoID       INT               NOT NULL,
    Nombre                     NVARCHAR(120)     NOT NULL,
    Descripcion                NVARCHAR(500)     NULL,
    Precio                     DECIMAL(10,2)     NOT NULL,
    Stock                      INT               NOT NULL CONSTRAINT DF_Productos_Stock DEFAULT (100),
    Disponible                 BIT               NOT NULL CONSTRAINT DF_Productos_Disponible DEFAULT (1),
    TiempoPreparacionMinutos  SMALLINT          NOT NULL,
    ImagenURL                  NVARCHAR(255)     NULL,
    FechaCreacion               DATETIME2(0)      NOT NULL CONSTRAINT DF_Productos_FechaCreacion DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Productos PRIMARY KEY (ProductoID),
    CONSTRAINT FK_Productos_Restaurantes FOREIGN KEY (RestauranteID) REFERENCES Restaurantes (RestauranteID),
    CONSTRAINT FK_Productos_CategoriasProducto FOREIGN KEY (CategoriaProductoID) REFERENCES CategoriasProducto (CategoriaProductoID),
    CONSTRAINT CK_Productos_Precio CHECK (Precio >= 0),
    CONSTRAINT CK_Productos_Stock CHECK (Stock >= 0),
    CONSTRAINT CK_Productos_Tiempo CHECK (TiempoPreparacionMinutos > 0)
);
GO

CREATE TABLE ImagenesProducto (
    ImagenID     INT IDENTITY(1,1) NOT NULL,
    ProductoID    INT               NOT NULL,
    URL           NVARCHAR(255)     NOT NULL,
    EsPrincipal  BIT               NOT NULL CONSTRAINT DF_ImagenesProducto_EsPrincipal DEFAULT (0),
    CONSTRAINT PK_ImagenesProducto PRIMARY KEY (ImagenID),
    CONSTRAINT FK_ImagenesProducto_Productos FOREIGN KEY (ProductoID) REFERENCES Productos (ProductoID)
);
GO

/* ============================================================================
   SECCION 5: CARRITO DE COMPRAS
   ============================================================================ */

CREATE TABLE Carrito (
    CarritoID           INT IDENTITY(1,1) NOT NULL,
    ClienteID            INT               NOT NULL,
    FechaCreacion         DATETIME2(0)      NOT NULL CONSTRAINT DF_Carrito_FechaCreacion DEFAULT (SYSDATETIME()),
    FechaActualizacion   DATETIME2(0)      NULL,
    Estado                VARCHAR(20)       NOT NULL CONSTRAINT DF_Carrito_Estado DEFAULT ('Activo'),
    CONSTRAINT PK_Carrito PRIMARY KEY (CarritoID),
    CONSTRAINT UQ_Carrito_ClienteID UNIQUE (ClienteID),
    CONSTRAINT FK_Carrito_Clientes FOREIGN KEY (ClienteID) REFERENCES Clientes (ClienteID),
    CONSTRAINT CK_Carrito_Estado CHECK (Estado IN ('Activo','Convertido','Abandonado'))
);
GO

CREATE TABLE DetalleCarrito (
    DetalleCarritoID  INT IDENTITY(1,1) NOT NULL,
    CarritoID          INT               NOT NULL,
    ProductoID         INT               NOT NULL,
    Cantidad            INT               NOT NULL,
    PrecioUnitario     DECIMAL(10,2)     NOT NULL,
    Notas               NVARCHAR(200)     NULL,
    CONSTRAINT PK_DetalleCarrito PRIMARY KEY (DetalleCarritoID),
    CONSTRAINT FK_DetalleCarrito_Carrito FOREIGN KEY (CarritoID) REFERENCES Carrito (CarritoID),
    CONSTRAINT FK_DetalleCarrito_Productos FOREIGN KEY (ProductoID) REFERENCES Productos (ProductoID),
    CONSTRAINT UQ_DetalleCarrito_CarritoProducto UNIQUE (CarritoID, ProductoID),
    CONSTRAINT CK_DetalleCarrito_Cantidad CHECK (Cantidad > 0),
    CONSTRAINT CK_DetalleCarrito_Precio CHECK (PrecioUnitario >= 0)
);
GO

/* ============================================================================
   SECCION 6: CUPONES
   ============================================================================ */

CREATE TABLE Cupones (
    CuponID             INT IDENTITY(1,1) NOT NULL,
    Codigo               VARCHAR(30)       NOT NULL,
    Descripcion          NVARCHAR(200)     NULL,
    TipoDescuento        VARCHAR(15)       NOT NULL,
    Valor                 DECIMAL(10,2)     NOT NULL,
    MontoMinimoCompra   DECIMAL(10,2)     NOT NULL CONSTRAINT DF_Cupones_MontoMinimo DEFAULT (0),
    FechaInicio           DATE              NOT NULL,
    FechaFin              DATE              NOT NULL,
    UsoMaximo             INT               NOT NULL,
    UsosActuales          INT               NOT NULL CONSTRAINT DF_Cupones_UsosActuales DEFAULT (0),
    Activo                 BIT               NOT NULL CONSTRAINT DF_Cupones_Activo DEFAULT (1),
    CONSTRAINT PK_Cupones PRIMARY KEY (CuponID),
    CONSTRAINT UQ_Cupones_Codigo UNIQUE (Codigo),
    CONSTRAINT CK_Cupones_Tipo CHECK (TipoDescuento IN ('Porcentaje','Monto')),
    CONSTRAINT CK_Cupones_Valor CHECK (Valor > 0),
    CONSTRAINT CK_Cupones_Fechas CHECK (FechaFin >= FechaInicio),
    CONSTRAINT CK_Cupones_Usos CHECK (UsosActuales <= UsoMaximo)
);
GO

/* ============================================================================
   SECCION 7: PEDIDOS Y DETALLE
   ============================================================================ */

CREATE TABLE Pedidos (
    PedidoID               INT IDENTITY(1,1) NOT NULL,
    ClienteID                INT               NOT NULL,
    RestauranteID            INT               NOT NULL,
    DeliveryID               INT               NULL,
    DireccionEntregaID      INT               NOT NULL,
    EstadoPedidoID           INT               NOT NULL,
    MetodoPagoID             INT               NOT NULL,
    CuponID                   INT               NULL,
    Subtotal                  DECIMAL(10,2)     NOT NULL,
    IVA                        DECIMAL(10,2)     NOT NULL,
    CostoEnvio                DECIMAL(10,2)     NOT NULL,
    Descuento                 DECIMAL(10,2)     NOT NULL CONSTRAINT DF_Pedidos_Descuento DEFAULT (0),
    Total                      DECIMAL(10,2)     NOT NULL,
    FechaPedido                DATETIME2(0)      NOT NULL CONSTRAINT DF_Pedidos_FechaPedido DEFAULT (SYSDATETIME()),
    FechaEntregaEstimada     DATETIME2(0)      NULL,
    FechaEntregaReal          DATETIME2(0)      NULL,
    Notas                      NVARCHAR(300)     NULL,
    CONSTRAINT PK_Pedidos PRIMARY KEY (PedidoID),
    CONSTRAINT FK_Pedidos_Clientes FOREIGN KEY (ClienteID) REFERENCES Clientes (ClienteID),
    CONSTRAINT FK_Pedidos_Restaurantes FOREIGN KEY (RestauranteID) REFERENCES Restaurantes (RestauranteID),
    CONSTRAINT FK_Pedidos_Delivery FOREIGN KEY (DeliveryID) REFERENCES Delivery (DeliveryID),
    CONSTRAINT FK_Pedidos_Direcciones FOREIGN KEY (DireccionEntregaID) REFERENCES Direcciones (DireccionID),
    CONSTRAINT FK_Pedidos_EstadoPedido FOREIGN KEY (EstadoPedidoID) REFERENCES EstadoPedido (EstadoPedidoID),
    CONSTRAINT FK_Pedidos_MetodosPago FOREIGN KEY (MetodoPagoID) REFERENCES MetodosPago (MetodoPagoID),
    CONSTRAINT FK_Pedidos_Cupones FOREIGN KEY (CuponID) REFERENCES Cupones (CuponID),
    CONSTRAINT CK_Pedidos_Subtotal CHECK (Subtotal >= 0),
    CONSTRAINT CK_Pedidos_IVA CHECK (IVA >= 0),
    CONSTRAINT CK_Pedidos_CostoEnvio CHECK (CostoEnvio >= 0),
    CONSTRAINT CK_Pedidos_Descuento CHECK (Descuento >= 0),
    CONSTRAINT CK_Pedidos_Total CHECK (Total >= 0)
);
GO

CREATE TABLE DetallePedido (
    DetallePedidoID  INT IDENTITY(1,1) NOT NULL,
    PedidoID          INT               NOT NULL,
    ProductoID        INT               NOT NULL,
    Cantidad           INT               NOT NULL,
    PrecioUnitario    DECIMAL(10,2)     NOT NULL,
    Subtotal           AS (CAST(Cantidad * PrecioUnitario AS DECIMAL(10,2))) PERSISTED,
    Notas              NVARCHAR(200)     NULL,
    CONSTRAINT PK_DetallePedido PRIMARY KEY (DetallePedidoID),
    CONSTRAINT FK_DetallePedido_Pedidos FOREIGN KEY (PedidoID) REFERENCES Pedidos (PedidoID),
    CONSTRAINT FK_DetallePedido_Productos FOREIGN KEY (ProductoID) REFERENCES Productos (ProductoID),
    CONSTRAINT CK_DetallePedido_Cantidad CHECK (Cantidad > 0),
    CONSTRAINT CK_DetallePedido_Precio CHECK (PrecioUnitario >= 0)
);
GO

CREATE TABLE HistorialEstados (
    HistorialID     INT IDENTITY(1,1) NOT NULL,
    PedidoID         INT               NOT NULL,
    EstadoPedidoID   INT               NOT NULL,
    FechaCambio       DATETIME2(0)      NOT NULL CONSTRAINT DF_HistorialEstados_FechaCambio DEFAULT (SYSDATETIME()),
    UsuarioID         INT               NULL,
    Comentario        NVARCHAR(300)     NULL,
    CONSTRAINT PK_HistorialEstados PRIMARY KEY (HistorialID),
    CONSTRAINT FK_HistorialEstados_Pedidos FOREIGN KEY (PedidoID) REFERENCES Pedidos (PedidoID),
    CONSTRAINT FK_HistorialEstados_EstadoPedido FOREIGN KEY (EstadoPedidoID) REFERENCES EstadoPedido (EstadoPedidoID),
    CONSTRAINT FK_HistorialEstados_Usuarios FOREIGN KEY (UsuarioID) REFERENCES Usuarios (UsuarioID)
);
GO

/* ============================================================================
   SECCION 8: PAGOS Y FACTURACION
   ============================================================================ */

CREATE TABLE Pagos (
    PagoID                   INT IDENTITY(1,1) NOT NULL,
    PedidoID                  INT               NOT NULL,
    MetodoPagoID              INT               NOT NULL,
    Monto                      DECIMAL(10,2)     NOT NULL,
    Estado                     VARCHAR(20)       NOT NULL CONSTRAINT DF_Pagos_Estado DEFAULT ('Pendiente'),
    FechaPago                  DATETIME2(0)      NOT NULL CONSTRAINT DF_Pagos_FechaPago DEFAULT (SYSDATETIME()),
    ReferenciaTransaccion    VARCHAR(100)      NULL,
    CONSTRAINT PK_Pagos PRIMARY KEY (PagoID),
    CONSTRAINT UQ_Pagos_PedidoID UNIQUE (PedidoID),
    CONSTRAINT FK_Pagos_Pedidos FOREIGN KEY (PedidoID) REFERENCES Pedidos (PedidoID),
    CONSTRAINT FK_Pagos_MetodosPago FOREIGN KEY (MetodoPagoID) REFERENCES MetodosPago (MetodoPagoID),
    CONSTRAINT CK_Pagos_Monto CHECK (Monto >= 0),
    CONSTRAINT CK_Pagos_Estado CHECK (Estado IN ('Pendiente','Completado','Fallido','Reembolsado'))
);
GO

CREATE TABLE HistorialPagos (
    HistorialPagoID  INT IDENTITY(1,1) NOT NULL,
    PagoID            INT               NOT NULL,
    EstadoAnterior     VARCHAR(20)       NULL,
    EstadoNuevo        VARCHAR(20)       NOT NULL,
    FechaCambio         DATETIME2(0)      NOT NULL CONSTRAINT DF_HistorialPagos_FechaCambio DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_HistorialPagos PRIMARY KEY (HistorialPagoID),
    CONSTRAINT FK_HistorialPagos_Pagos FOREIGN KEY (PagoID) REFERENCES Pagos (PagoID)
);
GO

CREATE TABLE Facturas (
    FacturaID       INT IDENTITY(1,1) NOT NULL,
    PedidoID         INT               NOT NULL,
    NumeroFactura    VARCHAR(30)       NOT NULL,
    ClienteID        INT               NOT NULL,
    Subtotal          DECIMAL(10,2)     NOT NULL,
    IVA                DECIMAL(10,2)     NOT NULL,
    Total              DECIMAL(10,2)     NOT NULL,
    FechaEmision       DATETIME2(0)      NOT NULL CONSTRAINT DF_Facturas_FechaEmision DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Facturas PRIMARY KEY (FacturaID),
    CONSTRAINT UQ_Facturas_PedidoID UNIQUE (PedidoID),
    CONSTRAINT UQ_Facturas_NumeroFactura UNIQUE (NumeroFactura),
    CONSTRAINT FK_Facturas_Pedidos FOREIGN KEY (PedidoID) REFERENCES Pedidos (PedidoID),
    CONSTRAINT FK_Facturas_Clientes FOREIGN KEY (ClienteID) REFERENCES Clientes (ClienteID)
);
GO

/* ============================================================================
   SECCION 9: CALIFICACIONES
   ============================================================================ */

CREATE TABLE CalificacionesRestaurante (
    CalificacionRestauranteID  INT IDENTITY(1,1) NOT NULL,
    PedidoID                     INT               NOT NULL,
    ClienteID                    INT               NOT NULL,
    RestauranteID                INT               NOT NULL,
    Puntuacion                    TINYINT           NOT NULL,
    Comentario                    NVARCHAR(500)     NULL,
    Fecha                          DATETIME2(0)      NOT NULL CONSTRAINT DF_CalificacionesRestaurante_Fecha DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_CalificacionesRestaurante PRIMARY KEY (CalificacionRestauranteID),
    CONSTRAINT UQ_CalificacionesRestaurante_PedidoID UNIQUE (PedidoID),
    CONSTRAINT FK_CalificacionesRestaurante_Pedidos FOREIGN KEY (PedidoID) REFERENCES Pedidos (PedidoID),
    CONSTRAINT FK_CalificacionesRestaurante_Clientes FOREIGN KEY (ClienteID) REFERENCES Clientes (ClienteID),
    CONSTRAINT FK_CalificacionesRestaurante_Restaurantes FOREIGN KEY (RestauranteID) REFERENCES Restaurantes (RestauranteID),
    CONSTRAINT CK_CalificacionesRestaurante_Puntuacion CHECK (Puntuacion BETWEEN 1 AND 5)
);
GO

CREATE TABLE CalificacionesDelivery (
    CalificacionDeliveryID  INT IDENTITY(1,1) NOT NULL,
    PedidoID                  INT               NOT NULL,
    ClienteID                 INT               NOT NULL,
    DeliveryID                INT               NOT NULL,
    Puntuacion                 TINYINT           NOT NULL,
    Comentario                 NVARCHAR(500)     NULL,
    Fecha                       DATETIME2(0)      NOT NULL CONSTRAINT DF_CalificacionesDelivery_Fecha DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_CalificacionesDelivery PRIMARY KEY (CalificacionDeliveryID),
    CONSTRAINT UQ_CalificacionesDelivery_PedidoID UNIQUE (PedidoID),
    CONSTRAINT FK_CalificacionesDelivery_Pedidos FOREIGN KEY (PedidoID) REFERENCES Pedidos (PedidoID),
    CONSTRAINT FK_CalificacionesDelivery_Clientes FOREIGN KEY (ClienteID) REFERENCES Clientes (ClienteID),
    CONSTRAINT FK_CalificacionesDelivery_Delivery FOREIGN KEY (DeliveryID) REFERENCES Delivery (DeliveryID),
    CONSTRAINT CK_CalificacionesDelivery_Puntuacion CHECK (Puntuacion BETWEEN 1 AND 5)
);
GO

/* ============================================================================
   SECCION 10: TABLAS PROFESIONALES ADICIONALES
   ============================================================================ */

CREATE TABLE Favoritos (
    FavoritoID     INT IDENTITY(1,1) NOT NULL,
    ClienteID       INT               NOT NULL,
    RestauranteID   INT               NOT NULL,
    FechaAgregado    DATETIME2(0)      NOT NULL CONSTRAINT DF_Favoritos_FechaAgregado DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Favoritos PRIMARY KEY (FavoritoID),
    CONSTRAINT FK_Favoritos_Clientes FOREIGN KEY (ClienteID) REFERENCES Clientes (ClienteID),
    CONSTRAINT FK_Favoritos_Restaurantes FOREIGN KEY (RestauranteID) REFERENCES Restaurantes (RestauranteID),
    CONSTRAINT UQ_Favoritos_ClienteRestaurante UNIQUE (ClienteID, RestauranteID)
);
GO

CREATE TABLE Promociones (
    PromocionID           INT IDENTITY(1,1) NOT NULL,
    RestauranteID          INT               NOT NULL,
    Nombre                  NVARCHAR(100)     NOT NULL,
    Descripcion             NVARCHAR(300)     NULL,
    DescuentoPorcentaje   DECIMAL(5,2)      NOT NULL,
    FechaInicio             DATE              NOT NULL,
    FechaFin                DATE              NOT NULL,
    Activo                   BIT               NOT NULL CONSTRAINT DF_Promociones_Activo DEFAULT (1),
    CONSTRAINT PK_Promociones PRIMARY KEY (PromocionID),
    CONSTRAINT FK_Promociones_Restaurantes FOREIGN KEY (RestauranteID) REFERENCES Restaurantes (RestauranteID),
    CONSTRAINT CK_Promociones_Descuento CHECK (DescuentoPorcentaje BETWEEN 0 AND 100),
    CONSTRAINT CK_Promociones_Fechas CHECK (FechaFin >= FechaInicio)
);
GO

CREATE TABLE Notificaciones (
    NotificacionID  INT IDENTITY(1,1) NOT NULL,
    UsuarioID        INT               NOT NULL,
    Titulo            NVARCHAR(150)     NOT NULL,
    Mensaje           NVARCHAR(500)     NOT NULL,
    Tipo               VARCHAR(30)       NOT NULL,
    Leida              BIT               NOT NULL CONSTRAINT DF_Notificaciones_Leida DEFAULT (0),
    FechaCreacion       DATETIME2(0)      NOT NULL CONSTRAINT DF_Notificaciones_FechaCreacion DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Notificaciones PRIMARY KEY (NotificacionID),
    CONSTRAINT FK_Notificaciones_Usuarios FOREIGN KEY (UsuarioID) REFERENCES Usuarios (UsuarioID),
    CONSTRAINT CK_Notificaciones_Tipo CHECK (Tipo IN ('Pedido','Promocion','Sistema','General'))
);
GO

CREATE TABLE Sesiones (
    SesionID          INT IDENTITY(1,1) NOT NULL,
    UsuarioID          INT               NOT NULL,
    Token               VARCHAR(255)      NOT NULL,
    FechaInicio          DATETIME2(0)      NOT NULL CONSTRAINT DF_Sesiones_FechaInicio DEFAULT (SYSDATETIME()),
    FechaExpiracion     DATETIME2(0)      NOT NULL,
    DireccionIP          VARCHAR(45)       NULL,
    Activa                BIT               NOT NULL CONSTRAINT DF_Sesiones_Activa DEFAULT (1),
    CONSTRAINT PK_Sesiones PRIMARY KEY (SesionID),
    CONSTRAINT UQ_Sesiones_Token UNIQUE (Token),
    CONSTRAINT FK_Sesiones_Usuarios FOREIGN KEY (UsuarioID) REFERENCES Usuarios (UsuarioID)
);
GO

CREATE TABLE Tokens (
    TokenID           INT IDENTITY(1,1) NOT NULL,
    UsuarioID          INT               NOT NULL,
    Token               VARCHAR(255)      NOT NULL,
    Tipo                 VARCHAR(20)       NOT NULL,
    FechaCreacion         DATETIME2(0)      NOT NULL CONSTRAINT DF_Tokens_FechaCreacion DEFAULT (SYSDATETIME()),
    FechaExpiracion      DATETIME2(0)      NOT NULL,
    Usado                 BIT               NOT NULL CONSTRAINT DF_Tokens_Usado DEFAULT (0),
    CONSTRAINT PK_Tokens PRIMARY KEY (TokenID),
    CONSTRAINT UQ_Tokens_Token UNIQUE (Token),
    CONSTRAINT FK_Tokens_Usuarios FOREIGN KEY (UsuarioID) REFERENCES Usuarios (UsuarioID),
    CONSTRAINT CK_Tokens_Tipo CHECK (Tipo IN ('Recuperacion','Verificacion','RefreshToken'))
);
GO

CREATE TABLE Auditoria (
    AuditoriaID  INT IDENTITY(1,1) NOT NULL,
    UsuarioID     INT               NULL,
    Tabla          VARCHAR(50)       NOT NULL,
    Accion         VARCHAR(20)       NOT NULL,
    RegistroID     INT               NULL,
    FechaAccion     DATETIME2(0)      NOT NULL CONSTRAINT DF_Auditoria_FechaAccion DEFAULT (SYSDATETIME()),
    Detalles       NVARCHAR(MAX)     NULL,
    CONSTRAINT PK_Auditoria PRIMARY KEY (AuditoriaID),
    CONSTRAINT FK_Auditoria_Usuarios FOREIGN KEY (UsuarioID) REFERENCES Usuarios (UsuarioID),
    CONSTRAINT CK_Auditoria_Accion CHECK (Accion IN ('INSERT','UPDATE','DELETE'))
);
GO

/* ============================================================================
   SECCION 11: INDICES DE APOYO (ademas de los generados por PK/UNIQUE)
   Cubren los patrones de consulta mas comunes: listados por FK, filtros por
   estado/disponibilidad, y ordenamientos por fecha.
   ============================================================================ */

CREATE INDEX IX_Usuarios_RolID ON Usuarios (RolID);
CREATE INDEX IX_Direcciones_ClienteID ON Direcciones (ClienteID);
CREATE INDEX IX_Delivery_EstadoDeliveryID ON Delivery (EstadoDeliveryID);
CREATE INDEX IX_Restaurantes_CategoriaRestauranteID ON Restaurantes (CategoriaRestauranteID);
CREATE INDEX IX_Restaurantes_Estado ON Restaurantes (Estado);
CREATE INDEX IX_Restaurantes_Nombre ON Restaurantes (Nombre);
CREATE INDEX IX_Productos_RestauranteID ON Productos (RestauranteID);
CREATE INDEX IX_Productos_CategoriaProductoID ON Productos (CategoriaProductoID);
CREATE INDEX IX_Productos_Disponible ON Productos (Disponible);
CREATE INDEX IX_Productos_Nombre ON Productos (Nombre);
CREATE INDEX IX_ImagenesProducto_ProductoID ON ImagenesProducto (ProductoID);
CREATE INDEX IX_DetalleCarrito_CarritoID ON DetalleCarrito (CarritoID);
CREATE INDEX IX_DetalleCarrito_ProductoID ON DetalleCarrito (ProductoID);
CREATE INDEX IX_Pedidos_ClienteID ON Pedidos (ClienteID);
CREATE INDEX IX_Pedidos_RestauranteID ON Pedidos (RestauranteID);
CREATE INDEX IX_Pedidos_DeliveryID ON Pedidos (DeliveryID);
CREATE INDEX IX_Pedidos_EstadoPedidoID ON Pedidos (EstadoPedidoID);
CREATE INDEX IX_Pedidos_FechaPedido ON Pedidos (FechaPedido DESC);
CREATE INDEX IX_DetallePedido_PedidoID ON DetallePedido (PedidoID);
CREATE INDEX IX_DetallePedido_ProductoID ON DetallePedido (ProductoID);
CREATE INDEX IX_HistorialEstados_PedidoID ON HistorialEstados (PedidoID);
CREATE INDEX IX_Pagos_MetodoPagoID ON Pagos (MetodoPagoID);
CREATE INDEX IX_HistorialPagos_PagoID ON HistorialPagos (PagoID);
CREATE INDEX IX_CalificacionesRestaurante_RestauranteID ON CalificacionesRestaurante (RestauranteID);
CREATE INDEX IX_CalificacionesRestaurante_ClienteID ON CalificacionesRestaurante (ClienteID);
CREATE INDEX IX_CalificacionesDelivery_DeliveryID ON CalificacionesDelivery (DeliveryID);
CREATE INDEX IX_CalificacionesDelivery_ClienteID ON CalificacionesDelivery (ClienteID);
CREATE INDEX IX_Favoritos_ClienteID ON Favoritos (ClienteID);
CREATE INDEX IX_Promociones_RestauranteID ON Promociones (RestauranteID);
CREATE INDEX IX_Notificaciones_UsuarioID_Leida ON Notificaciones (UsuarioID, Leida);
CREATE INDEX IX_Sesiones_UsuarioID ON Sesiones (UsuarioID);
CREATE INDEX IX_Tokens_UsuarioID ON Tokens (UsuarioID);
CREATE INDEX IX_Auditoria_Tabla_RegistroID ON Auditoria (Tabla, RegistroID);
GO

/* ============================================================================
   SECCION 12: DATOS DE CATALOGO
   ============================================================================ */

INSERT INTO Roles (NombreRol, Descripcion) VALUES
(N'Administrador', N'Gestiona la plataforma: usuarios, restaurantes, productos y pedidos'),
(N'Cliente', N'Usuario final que realiza pedidos de comida'),
(N'Delivery', N'Repartidor encargado de entregar los pedidos');
GO

INSERT INTO EstadoPedido (Nombre, Descripcion, Orden) VALUES
(N'Pendiente', N'El pedido fue creado y espera confirmacion del restaurante', 1),
(N'Aceptado', N'El restaurante acepto el pedido', 2),
(N'Preparando', N'El restaurante esta preparando el pedido', 3),
(N'En camino', N'El repartidor recogio el pedido y va hacia el cliente', 4),
(N'Entregado', N'El pedido fue entregado al cliente', 5),
(N'Cancelado', N'El pedido fue cancelado', 6);
GO

INSERT INTO EstadoDelivery (Nombre, Descripcion) VALUES
(N'Disponible', N'El repartidor esta libre y puede aceptar pedidos'),
(N'Ocupado', N'El repartidor se encuentra realizando una entrega'),
(N'Desconectado', N'El repartidor no esta activo en la plataforma');
GO

INSERT INTO MetodosPago (Nombre, Descripcion, Activo) VALUES
(N'Efectivo', N'Pago en efectivo contra entrega', 1),
(N'Tarjeta', N'Pago con tarjeta de credito o debito', 1),
(N'Transferencia', N'Transferencia bancaria directa', 1),
(N'PayPhone', N'Pago movil mediante PayPhone', 1),
(N'PayPal', N'Pago mediante billetera PayPal', 1);
GO

INSERT INTO CategoriasRestaurante (Nombre, Descripcion) VALUES
(N'Comida Rapida', N'Hamburguesas y menu rapido de cadena internacional'),
(N'Pollo Broaster', N'Especialistas en pollo frito y broaster'),
(N'Pizzeria', N'Pizzas al horno de diferentes tamanos y sabores'),
(N'Sandwiches y Saludable', N'Subs, wraps y opciones ligeras'),
(N'Comida Mexicana', N'Tacos, burritos y antojos mexicanos'),
(N'Mariscos y Comida Ecuatoriana', N'Ceviches y platos tipicos de la costa ecuatoriana'),
(N'Panaderia y Cafeteria', N'Panaderia, reposteria y cafeteria');
GO

INSERT INTO CategoriasProducto (Nombre, Descripcion) VALUES
(N'Hamburguesas', N'Hamburguesas y combos de carne o pollo'),
(N'Pollo', N'Piezas y presas de pollo frito o broaster'),
(N'Pizza', N'Pizzas individuales, medianas, grandes y familiares'),
(N'Sandwiches', N'Subs y sandwiches frios y calientes'),
(N'Tacos y Burritos', N'Antojos de comida mexicana'),
(N'Mariscos', N'Ceviches, encebollados y platos de mar'),
(N'Bebidas', N'Gaseosas, jugos y bebidas frias'),
(N'Postres', N'Helados, tortas y postres'),
(N'Acompanantes', N'Papas fritas, aros de cebolla y extras'),
(N'Ensaladas', N'Ensaladas y opciones saludables'),
(N'Panaderia', N'Pan, empanadas y reposteria'),
(N'Desayunos', N'Opciones de desayuno y cafeteria'),
(N'Combos', N'Combos y promociones de varios productos');
GO

/* ============================================================================
   SECCION 13: ADMINISTRADORES
   ============================================================================ */

INSERT INTO Usuarios (RolID, Correo, ContrasenaHash, Estado, FechaRegistro) VALUES
(1, N'sofia.ramirez@flashbite.com', 'AQAAAAIAAYagAAAAEJx1kQpZ3n7R8mLxWc2VbT9hF4dK6sYtN0pQaRw3eXcVbNmL1oP2qR4sT6uV8wX0y1==', 'Activo', '2023-01-10T08:00:00'),
(1, N'pablo.andrade@flashbite.com', 'AQAAAAIAAYagAAAAEKm2lRqA4o8S9nMyXd3WcU0iG5eL7tZuO1qRbSx4fYdWcOnM2pQ3rS5tU7vW9xY1z2==', 'Activo', '2023-01-10T08:05:00'),
(1, N'veronica.salcedo@flashbite.com', 'AQAAAAIAAYagAAAAELn3mSrB5p9T0oNzYe4XdV1jH6fM8uAvP2rSctY5gZeXdPoN3qR4sT6uV8wX0y2A3==', 'Activo', '2023-01-12T09:15:00');
GO

INSERT INTO Administradores (UsuarioID, Nombre, Apellido, Cargo, Telefono) VALUES
(1, N'Sofia Elizabeth', N'Ramirez', N'Gerente General', '0991234501'),
(2, N'Pablo Cesar', N'Andrade', N'Administrador de Plataforma', '0991234502'),
(3, N'Veronica Paulina', N'Salcedo', N'Soporte y Operaciones', '0991234503');
GO

/* ============================================================================
   SECCION 14: CLIENTES (25) Y SUS DIRECCIONES
   ============================================================================ */

INSERT INTO Usuarios (RolID, Correo, ContrasenaHash, Estado, FechaRegistro) VALUES
(2, N'maria.salazar@gmail.com',    'AQAAAAIAAYagAAAAEHc01a1b1c1d1e1f1g1h1i1j1k1l1m1n1o1p1q1r1s1t1u1v1w1x1y1z1A1B1C1D==', 'Activo', '2024-02-05T10:12:00'),
(2, N'carlos.perez@gmail.com',     'AQAAAAIAAYagAAAAEHc02a2b2c2d2e2f2g2h2i2j2k2l2m2n2o2p2q2r2s2t2u2v2w2x2y2z2A2B2C2D==', 'Activo', '2024-02-06T11:20:00'),
(2, N'andrea.torres@hotmail.com',  'AQAAAAIAAYagAAAAEHc03a3b3c3d3e3f3g3h3i3j3k3l3m3n3o3p3q3r3s3t3u3v3w3x3y3z3A3B3C3D==', 'Activo', '2024-02-08T09:45:00'),
(2, N'luis.vega@gmail.com',        'AQAAAAIAAYagAAAAEHc04a4b4c4d4e4f4g4h4i4j4k4l4m4n4o4p4q4r4s4t4u4v4w4x4y4z4A4B4C4D==', 'Activo', '2024-02-10T14:30:00'),
(2, N'camila.rojas@outlook.com',   'AQAAAAIAAYagAAAAEHc05a5b5c5d5e5f5g5h5i5j5k5l5m5n5o5p5q5r5s5t5u5v5w5x5y5z5A5B5C5D==', 'Activo', '2024-02-11T16:00:00'),
(2, N'juan.guerrero@gmail.com',    'AQAAAAIAAYagAAAAEHc06a6b6c6d6e6f6g6h6i6j6k6l6m6n6o6p6q6r6s6t6u6v6w6x6y6z6A6B6C6D==', 'Activo', '2024-02-14T08:22:00'),
(2, N'valentina.ibarra@gmail.com', 'AQAAAAIAAYagAAAAEHc07a7b7c7d7e7f7g7h7i7j7k7l7m7n7o7p7q7r7s7t7u7v7w7x7y7z7A7B7C7D==', 'Activo', '2024-02-15T12:40:00'),
(2, N'diego.suarez@hotmail.com',   'AQAAAAIAAYagAAAAEHc08a8b8c8d8e8f8g8h8i8j8k8l8m8n8o8p8q8r8s8t8u8v8w8x8y8z8A8B8C8D==', 'Activo', '2024-02-18T17:05:00'),
(2, N'paula.mendoza@gmail.com',    'AQAAAAIAAYagAAAAEHc09a9b9c9d9e9f9g9h9i9j9k9l9m9n9o9p9q9r9s9t9u9v9w9x9y9z9A9B9C9D==', 'Activo', '2024-02-20T10:10:00'),
(2, N'jorge.chavez@gmail.com',     'AQAAAAIAAYagAAAAEHc10a1b1c1d1e1f1g1h1i1j1k1l1m1n1o1p1q1r1s1t1u1v1w1x1y1z1A1B1C1E==', 'Activo', '2024-02-22T13:33:00'),
(2, N'daniela.ortiz@outlook.com',  'AQAAAAIAAYagAAAAEHc11a2b2c2d2e2f2g2h2i2j2k2l2m2n2o2p2q2r2s2t2u2v2w2x2y2z2A2B2C2E==', 'Activo', '2024-03-01T09:00:00'),
(2, N'miguel.herrera@gmail.com',   'AQAAAAIAAYagAAAAEHc12a3b3c3d3e3f3g3h3i3j3k3l3m3n3o3p3q3r3s3t3u3v3w3x3y3z3A3B3C3E==', 'Activo', '2024-03-03T15:18:00'),
(2, N'gabriela.paredes@gmail.com', 'AQAAAAIAAYagAAAAEHc13a4b4c4d4e4f4g4h4i4j4k4l4m4n4o4p4q4r4s4t4u4v4w4x4y4z4A4B4C4E==', 'Activo', '2024-03-05T18:45:00'),
(2, N'kevin.molina@hotmail.com',   'AQAAAAIAAYagAAAAEHc14a5b5c5d5e5f5g5h5i5j5k5l5m5n5o5p5q5r5s5t5u5v5w5x5y5z5A5B5C5E==', 'Activo', '2024-03-08T11:27:00'),
(2, N'nicole.cabrera@gmail.com',   'AQAAAAIAAYagAAAAEHc15a6b6c6d6e6f6g6h6i6j6k6l6m6n6o6p6q6r6s6t6u6v6w6x6y6z6A6B6C6E==', 'Activo', '2024-03-10T14:52:00'),
(2, N'andres.rivas@gmail.com',     'AQAAAAIAAYagAAAAEHc16a7b7c7d7e7f7g7h7i7j7k7l7m7n7o7p7q7r7s7t7u7v7w7x7y7z7A7B7C5E==', 'Activo', '2024-03-12T08:15:00'),
(2, N'mishell.zambrano@outlook.com','AQAAAAIAAYagAAAAEHc17a8b8c8d8e8f8g8h8i8j8k8l8m8n8o8p8q8r8s8t8u8v8w8x8y8z8A8B8C5E==', 'Activo', '2024-03-15T19:10:00'),
(2, N'christian.loor@gmail.com',   'AQAAAAIAAYagAAAAEHc18a9b9c9d9e9f9g9h9i9j9k9l9m9n9o9p9q9r9s9t9u9v9w9x9y9z9A9B9C5E==', 'Activo', '2024-03-18T10:40:00'),
(2, N'katherine.vera@gmail.com',   'AQAAAAIAAYagAAAAEHc19b1c1d1e1f1g1h1i1j1k1l1m1n1o1p1q1r1s1t1u1v1w1x1y1z1A1B1C1D1E==', 'Activo', '2024-03-20T16:05:00'),
(2, N'bryan.yepez@hotmail.com',    'AQAAAAIAAYagAAAAEHc20b2c2d2e2f2g2h2i2j2k2l2m2n2o2p2q2r2s2t2u2v2w2x2y2z2A2B2C2D2E==', 'Activo', '2024-03-22T09:50:00'),
(2, N'alexandra.castro@gmail.com', 'AQAAAAIAAYagAAAAEHc21b3c3d3e3f3g3h3i3j3k3l3m3n3o3p3q3r3s3t3u3v3w3x3y3z3A3B3C3D3E==', 'Activo', '2024-03-25T12:15:00'),
(2, N'fernando.bravo@gmail.com',   'AQAAAAIAAYagAAAAEHc22b4c4d4e4f4g4h4i4j4k4l4m4n4o4p4q4r4s4t4u4v4w4x4y4z4A4B4C4D4E==', 'Activo', '2024-03-28T17:37:00'),
(2, N'estefania.pena@outlook.com', 'AQAAAAIAAYagAAAAEHc23b5c5d5e5f5g5h5i5j5k5l5m5n5o5p5q5r5s5t5u5v5w5x5y5z5A5B5C5D5E==', 'Activo', '2024-04-02T08:05:00'),
(2, N'ricardo.guaman@gmail.com',   'AQAAAAIAAYagAAAAEHc24b6c6d6e6f6g6h6i6j6k6l6m6n6o6p6q6r6s6t6u6v6w6x6y6z6A6B6C6D6E==', 'Activo', '2024-04-05T13:48:00'),
(2, N'lorena.cando@gmail.com',     'AQAAAAIAAYagAAAAEHc25b7c7d7e7f7g7h7i7j7k7l7m7n7o7p7q7r7s7t7u7v7w7x7y7z7A7B7C7D7E==', 'Activo', '2024-04-08T15:20:00');
GO

INSERT INTO Clientes (UsuarioID, Nombre, Apellido, Telefono, FechaNacimiento, FechaRegistro) VALUES
(4,  N'Maria Jose',        N'Salazar',   '0987654301', '1996-05-12', '2024-02-05T10:12:00'),
(5,  N'Carlos Andres',     N'Perez',     '0987654302', '1993-08-22', '2024-02-06T11:20:00'),
(6,  N'Andrea Gabriela',   N'Torres',    '0987654303', '1998-01-30', '2024-02-08T09:45:00'),
(7,  N'Luis Fernando',     N'Vega',      '0987654304', '1990-11-02', '2024-02-10T14:30:00'),
(8,  N'Camila Alejandra',  N'Rojas',     '0987654305', '1999-03-17', '2024-02-11T16:00:00'),
(9,  N'Juan Pablo',        N'Guerrero',  '0987654306', '1995-07-09', '2024-02-14T08:22:00'),
(10, N'Valentina',         N'Ibarra',    '0987654307', '1997-09-25', '2024-02-15T12:40:00'),
(11, N'Diego Armando',     N'Suarez',    '0987654308', '1992-12-14', '2024-02-18T17:05:00'),
(12, N'Paula Cristina',    N'Mendoza',   '0987654309', '1994-04-03', '2024-02-20T10:10:00'),
(13, N'Jorge Eduardo',     N'Chavez',    '0987654310', '1989-06-19', '2024-02-22T13:33:00'),
(14, N'Daniela Fernanda',  N'Ortiz',     '0987654311', '2000-02-27', '2024-03-01T09:00:00'),
(15, N'Miguel Angel',      N'Herrera',   '0987654312', '1991-10-08', '2024-03-03T15:18:00'),
(16, N'Gabriela Estefania',N'Paredes',   '0987654313', '1996-01-15', '2024-03-05T18:45:00'),
(17, N'Kevin Alexander',   N'Molina',    '0987654314', '1998-08-05', '2024-03-08T11:27:00'),
(18, N'Nicole Estefania',  N'Cabrera',   '0987654315', '1997-05-29', '2024-03-10T14:52:00'),
(19, N'Andres Sebastian',  N'Rivas',     '0987654316', '1993-02-11', '2024-03-12T08:15:00'),
(20, N'Mishell Alejandra', N'Zambrano',  '0987654317', '1999-12-01', '2024-03-15T19:10:00'),
(21, N'Christian David',   N'Loor',      '0987654318', '1990-07-23', '2024-03-18T10:40:00'),
(22, N'Katherine Michelle',N'Vera',      '0987654319', '1995-09-14', '2024-03-20T16:05:00'),
(23, N'Bryan Patricio',    N'Yepez',     '0987654320', '1994-03-08', '2024-03-22T09:50:00'),
(24, N'Alexandra Nicole',  N'Castro',    '0987654321', '1998-06-30', '2024-03-25T12:15:00'),
(25, N'Fernando Jose',     N'Bravo',     '0987654322', '1992-01-19', '2024-03-28T17:37:00'),
(26, N'Estefania Nicole',  N'Pena',      '0987654323', '1996-11-27', '2024-04-02T08:05:00'),
(27, N'Ricardo Javier',    N'Guaman',    '0987654324', '1991-04-16', '2024-04-05T13:48:00'),
(28, N'Lorena Isabel',     N'Cando',     '0987654325', '1997-08-21', '2024-04-08T15:20:00');
GO

INSERT INTO Direcciones (ClienteID, Alias, Calle, NumeroExterior, Referencia, Ciudad, Provincia, CodigoPostal, Latitud, Longitud, EsPredeterminada) VALUES
(1,  N'Casa',     N'Av. Amazonas',              N'N34-120', N'Frente al parque La Carolina', N'Quito',      N'Pichincha',  '170135', -0.1807000, -78.4870000, 1),
(1,  N'Trabajo',  N'Av. 6 de Diciembre',        N'N24-100', N'Edificio Torre Azul, piso 4',   N'Quito',      N'Pichincha',  '170150', -0.1950000, -78.4830000, 0),
(2,  N'Casa',     N'Av. Republica del Salvador',N'E7-123',  N'Cerca al centro comercial',     N'Quito',      N'Pichincha',  '170504', -0.1789000, -78.4813000, 1),
(2,  N'Trabajo',  N'Av. Eloy Alfaro',           N'N32-45',  N'Junto al Ministerio',           N'Quito',      N'Pichincha',  '170136', -0.1799000, -78.4790000, 0),
(3,  N'Casa',     N'Av. Naciones Unidas',       N'N45-67',  N'Cerca del World Trade Center',  N'Quito',      N'Pichincha',  '170518', -0.1810000, -78.4870000, 1),
(3,  N'Trabajo',  N'Av. Colon',                 N'E10-89',  N'Edificio Plaza Colon',          N'Quito',      N'Pichincha',  '170143', -0.1960000, -78.4850000, 0),
(4,  N'Casa',     N'Av. Francisco de Orellana', N'S/N',     N'Torres del Rio, torre B',        N'Guayaquil',  N'Guayas',     '090150', -2.1710000, -79.8990000, 1),
(4,  N'Trabajo',  N'Av. Miguel H. Alcivar',     N'S/N',     N'Cerca de Mall del Sol',          N'Guayaquil',  N'Guayas',     '090510', -2.1580000, -79.9010000, 0),
(5,  N'Casa',     N'Av. Occidental',            N'N80-23',  N'Detras del hospital',           N'Quito',      N'Pichincha',  '170147', -0.1650000, -78.5100000, 1),
(5,  N'Trabajo',  N'Av. Mariscal Sucre',        N'N12-56',  N'Local comercial planta baja',   N'Quito',      N'Pichincha',  '170147', -0.1700000, -78.5050000, 0),
(6,  N'Casa',     N'Av. Juan Tanca Marengo',    N'S/N',     N'Km 2.5, urbanizacion privada',   N'Guayaquil',  N'Guayas',     '090615', -2.1450000, -79.9200000, 1),
(6,  N'Trabajo',  N'Av. Carlos Julio Arosemena',N'S/N',     N'Edificio corporativo',          N'Guayaquil',  N'Guayas',     '090313', -2.1770000, -79.8930000, 0),
(7,  N'Casa',     N'Av. Gaspar de Villarroel',  N'E4-56',   N'Conjunto residencial Los Pinos', N'Quito',      N'Pichincha',  '170505', -0.1730000, -78.4780000, 1),
(7,  N'Trabajo',  N'Av. de las Americas',       N'N15-30',  N'Cerca del estadio',              N'Quito',      N'Pichincha',  '170136', -0.1690000, -78.4900000, 0),
(8,  N'Casa',     N'Av. Kennedy',               N'S/N',     N'Ciudadela Kennedy Norte',        N'Guayaquil',  N'Guayas',     '090505', -2.1500000, -79.9060000, 1),
(8,  N'Trabajo',  N'Av. Rodrigo Chavez',        N'S/N',     N'Cerca del Riocentro',            N'Guayaquil',  N'Guayas',     '090615', -2.1650000, -79.9150000, 0),
(9,  N'Casa',     N'Av. Simon Bolivar',         N'N60-15',  N'Sector la Y',                    N'Quito',      N'Pichincha',  '170150', -0.1500000, -78.4600000, 1),
(9,  N'Trabajo',  N'Av. Interoceanica',         N'Km 12',   N'Plaza comercial',                N'Quito',      N'Pichincha',  '170157', -0.1900000, -78.4200000, 0),
(10, N'Casa',     N'Av. Diez de Agosto',        N'N23-44',  N'Cerca del parque',               N'Quito',      N'Pichincha',  '170136', -0.2050000, -78.4980000, 1),
(10, N'Trabajo',  N'Av. La Prensa',             N'N56-78',  N'Edificio corporativo Norte',     N'Quito',      N'Pichincha',  '170136', -0.1550000, -78.4900000, 0),
(11, N'Casa',     N'Av. Portete',               N'S/N',     N'Sector Urdesa',                  N'Guayaquil',  N'Guayas',     '090615', -2.1600000, -79.9080000, 1),
(11, N'Trabajo',  N'Av. Malecon Simon Bolivar', N'S/N',     N'Cerca del Malecon 2000',         N'Guayaquil',  N'Guayas',     '090313', -2.1900000, -79.8830000, 0),
(12, N'Casa',     N'Av. Atahualpa',             N'N70-12',  N'Sector Carcelen',                N'Quito',      N'Pichincha',  '170138', -0.1200000, -78.4650000, 1),
(13, N'Casa',     N'Av. Isidro Ayora',          N'S/N',     N'Cerca del aeropuerto',           N'Guayaquil',  N'Guayas',     '090612', -2.1550000, -79.8850000, 1),
(14, N'Casa',     N'Av. Galo Plaza Lasso',      N'N45-20',  N'Sector Inaquito',                N'Quito',      N'Pichincha',  '170515', -0.1780000, -78.4840000, 1),
(15, N'Casa',     N'Av. Nueve de Octubre',      N'S/N',     N'Centro de Guayaquil',            N'Guayaquil',  N'Guayas',     '090313', -2.1900000, -79.8860000, 1),
(16, N'Casa',     N'Av. Eloy Alfaro',           N'N50-33',  N'Sector Bellavista',              N'Quito',      N'Pichincha',  '170136', -0.1770000, -78.4790000, 1),
(17, N'Casa',     N'Av. Machala',               N'S/N',     N'Sector sur de Guayaquil',        N'Guayaquil',  N'Guayas',     '090150', -2.2100000, -79.9100000, 1),
(18, N'Casa',     N'Av. Mariana de Jesus',      N'N28-19',  N'Cerca al hospital Metropolitano',N'Quito',      N'Pichincha',  '170143', -0.1850000, -78.4930000, 1),
(19, N'Casa',     N'Av. Del Bombero',           N'S/N',     N'Sector norte',                   N'Guayaquil',  N'Guayas',     '090505', -2.1400000, -79.9050000, 1),
(20, N'Casa',     N'Av. Coruna',                N'N26-12',  N'Sector La Floresta',             N'Quito',      N'Pichincha',  '170525', -0.2080000, -78.4930000, 1),
(21, N'Casa',     N'Av. Domingo Comin',         N'S/N',     N'Sector centro',                  N'Guayaquil',  N'Guayas',     '090313', -2.1950000, -79.8870000, 1),
(22, N'Casa',     N'Av. 12 de Octubre',         N'N24-556', N'Cerca de la Universidad Catolica', N'Quito',    N'Pichincha',  '170143', -0.1980000, -78.4870000, 1),
(23, N'Casa',     N'Av. Pedro Menendez Gilbert',N'S/N',     N'Sector Los Ceibos',              N'Guayaquil',  N'Guayas',     '090505', -2.1350000, -79.9250000, 1),
(24, N'Casa',     N'Av. Granados',              N'E12-40',  N'Sector Republica del Salvador',  N'Quito',      N'Pichincha',  '170504', -0.1770000, -78.4800000, 1),
(25, N'Casa',     N'Av. Francisco Bolonia',      N'S/N',     N'Sector Alborada',                N'Guayaquil',  N'Guayas',     '090615', -2.1300000, -79.9050000, 1);
GO

/* ============================================================================
   SECCION 15: REPARTIDORES (DELIVERY) Y VEHICULOS (15)
   ============================================================================ */

INSERT INTO Vehiculos (Tipo, Marca, Modelo, Placa, Color, Anio) VALUES
(N'Moto',      N'Suzuki',   N'AX100',      'PBA-1001', N'Rojo',    2019),
(N'Moto',      N'Honda',    N'CB110',      'PBB-1002', N'Negro',   2020),
(N'Moto',      N'Yamaha',   N'YBR125',     'PBC-1003', N'Azul',    2021),
(N'Bicicleta', N'GW',       N'Urban',      NULL,        N'Verde',   2022),
(N'Moto',      N'Suzuki',   N'GN125',      'PBD-1004', N'Blanco',  2018),
(N'Moto',      N'Honda',    N'XR150',      'PBE-1005', N'Rojo',    2022),
(N'Automovil', N'Chevrolet',N'Sail',       'GYA-2001', N'Gris',    2017),
(N'Moto',      N'Bajaj',    N'Boxer150',   'PBF-1006', N'Negro',   2020),
(N'Bicicleta', N'Aro',      N'MTB Aro 27', NULL,        N'Amarillo',2021),
(N'Moto',      N'Suzuki',   N'AX100',      'PBG-1007', N'Azul',    2019),
(N'Moto',      N'Honda',    N'CB110',      'PBH-1008', N'Rojo',    2021),
(N'Moto',      N'Yamaha',   N'YBR125',     'PBI-1009', N'Negro',   2020),
(N'Moto',      N'Bajaj',    N'Discover125',N'PBJ-1010', N'Blanco',  2022),
(N'Automovil', N'Kia',      N'Rio',        'GYB-2002', N'Plateado',2018),
(N'Moto',      N'Suzuki',   N'GN125',      'PBK-1011', N'Verde',   2019);
GO

INSERT INTO Usuarios (RolID, Correo, ContrasenaHash, Estado, FechaRegistro) VALUES
(3, N'pedro.lopez@flashbite-delivery.com',    'AQAAAAIAAYagAAAAEDl01c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1D==', 'Activo', '2024-01-15T08:00:00'),
(3, N'jonathan.quishpe@flashbite-delivery.com','AQAAAAIAAYagAAAAEDl02c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2D==', 'Activo', '2024-01-16T08:00:00'),
(3, N'marco.tenesaca@flashbite-delivery.com',  'AQAAAAIAAYagAAAAEDl03c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3D==', 'Activo', '2024-01-17T08:00:00'),
(3, N'byron.cuenca@flashbite-delivery.com',    'AQAAAAIAAYagAAAAEDl04c4c4c4c4c4c4c4c4c4c4c4c4c4c4c4c4c4c4c4c4c4c4c4c4c4c4c4c4c4D==', 'Activo', '2024-01-18T08:00:00'),
(3, N'wilson.toapanta@flashbite-delivery.com', 'AQAAAAIAAYagAAAAEDl05c5c5c5c5c5c5c5c5c5c5c5c5c5c5c5c5c5c5c5c5c5c5c5c5c5c5c5c5c5D==', 'Activo', '2024-01-19T08:00:00'),
(3, N'erick.salinas@flashbite-delivery.com',   'AQAAAAIAAYagAAAAEDl06c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6D==', 'Activo', '2024-01-20T08:00:00'),
(3, N'henry.chala@flashbite-delivery.com',     'AQAAAAIAAYagAAAAEDl07c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7D==', 'Activo', '2024-01-21T08:00:00'),
(3, N'cristian.males@flashbite-delivery.com',  'AQAAAAIAAYagAAAAEDl08c8c8c8c8c8c8c8c8c8c8c8c8c8c8c8c8c8c8c8c8c8c8c8c8c8c8c8c8c8D==', 'Activo', '2024-01-22T08:00:00'),
(3, N'anthony.villacis@flashbite-delivery.com','AQAAAAIAAYagAAAAEDl09c9c9c9c9c9c9c9c9c9c9c9c9c9c9c9c9c9c9c9c9c9c9c9c9c9c9c9c9c9D==', 'Activo', '2024-01-23T08:00:00'),
(3, N'franklin.cuasquer@flashbite-delivery.com','AQAAAAIAAYagAAAAEDl10d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1D==','Activo', '2024-01-24T08:00:00'),
(3, N'segundo.punina@flashbite-delivery.com',  'AQAAAAIAAYagAAAAEDl11d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2D==', 'Activo', '2024-01-25T08:00:00'),
(3, N'jefferson.tigre@flashbite-delivery.com', 'AQAAAAIAAYagAAAAEDl12d3d3d3d3d3d3d3d3d3d3d3d3d3d3d3d3d3d3d3d3d3d3d3d3d3d3d3d3d3D==', 'Activo', '2024-01-26T08:00:00'),
(3, N'danny.sarango@flashbite-delivery.com',   'AQAAAAIAAYagAAAAEDl13d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4D==', 'Activo', '2024-01-27T08:00:00'),
(3, N'roberto.zuniga@flashbite-delivery.com',  'AQAAAAIAAYagAAAAEDl14d5d5d5d5d5d5d5d5d5d5d5d5d5d5d5d5d5d5d5d5d5d5d5d5d5d5d5d5d5D==', 'Activo', '2024-01-28T08:00:00'),
(3, N'luis.anrango@flashbite-delivery.com',    'AQAAAAIAAYagAAAAEDl15d6d6d6d6d6d6d6d6d6d6d6d6d6d6d6d6d6d6d6d6d6d6d6d6d6d6d6d6d6D==', 'Activo', '2024-01-29T08:00:00');
GO

INSERT INTO Delivery (UsuarioID, Nombre, Apellido, Telefono, VehiculoID, EstadoDeliveryID, CalificacionPromedio, FechaRegistro) VALUES
(29, N'Pedro Antonio',   N'Lopez',     '0976543201', 1,  1, 0, '2024-01-15T08:00:00'),
(30, N'Jonathan Steven', N'Quishpe',   '0976543202', 2,  1, 0, '2024-01-16T08:00:00'),
(31, N'Marco Vinicio',   N'Tenesaca',  '0976543203', 3,  2, 0, '2024-01-17T08:00:00'),
(32, N'Byron Alexis',    N'Cuenca',    '0976543204', 4,  1, 0, '2024-01-18T08:00:00'),
(33, N'Wilson Patricio', N'Toapanta',  '0976543205', 5,  2, 0, '2024-01-19T08:00:00'),
(34, N'Erick Fabricio',  N'Salinas',   '0976543206', 6,  1, 0, '2024-01-20T08:00:00'),
(35, N'Henry David',     N'Chala',     '0976543207', 7,  3, 0, '2024-01-21T08:00:00'),
(36, N'Cristian Paul',   N'Males',     '0976543208', 8,  1, 0, '2024-01-22T08:00:00'),
(37, N'Anthony Josue',   N'Villacis',  '0976543209', 9,  2, 0, '2024-01-23T08:00:00'),
(38, N'Franklin Ivan',   N'Cuasquer',  '0976543210', 10, 1, 0, '2024-01-24T08:00:00'),
(39, N'Segundo Manuel',  N'Punina',    '0976543211', 11, 1, 0, '2024-01-25T08:00:00'),
(40, N'Jefferson Omar',  N'Tigre',     '0976543212', 12, 3, 0, '2024-01-26T08:00:00'),
(41, N'Danny Alexander', N'Sarango',   '0976543213', 13, 1, 0, '2024-01-27T08:00:00'),
(42, N'Roberto Carlos',  N'Zuniga',    '0976543214', 14, 2, 0, '2024-01-28T08:00:00'),
(43, N'Luis Miguel',     N'Anrango',   '0976543215', 15, 1, 0, '2024-01-29T08:00:00');
GO

/* ============================================================================
   SECCION 16: RESTAURANTES (10) Y SUS HORARIOS
   ============================================================================ */

INSERT INTO Restaurantes (Nombre, Descripcion, CategoriaRestauranteID, Direccion, Ciudad, Telefono, Correo, LogoURL, HorarioApertura, HorarioCierre, Estado, CalificacionPromedio, CostoEnvioBase, FechaRegistro) VALUES
(N'McDonalds',                       N'Cadena internacional de comida rapida, famosa por sus hamburguesas y McFlurry.',        1, N'Av. Amazonas N34-120, Quito',            N'Quito',     '022345001', N'contacto@mcdonalds-ec.com',  N'https://cdn.flashbite.com/logos/mcdonalds.png',    '08:00', '23:00', 'Aprobado', 0, 2.00, '2023-06-01T09:00:00'),
(N'KFC',                              N'Especialistas en pollo frito crocante con recetas originales.',                          2, N'Av. 6 de Diciembre N24-100, Quito',      N'Quito',     '022345002', N'contacto@kfc-ec.com',        N'https://cdn.flashbite.com/logos/kfc.png',           '09:00', '22:30', 'Aprobado', 0, 2.00, '2023-06-02T09:00:00'),
(N'Burger King',                      N'Hamburguesas a la parrilla al estilo flame-grilled.',                                    1, N'C.C. San Marino, Guayaquil',              N'Guayaquil', '042345003', N'contacto@burgerking-ec.com', N'https://cdn.flashbite.com/logos/burgerking.png',    '09:00', '23:00', 'Aprobado', 0, 2.50, '2023-06-05T09:00:00'),
(N'Pizza Hut',                        N'Pizzas artesanales al horno de piedra con ingredientes premium.',                        3, N'Av. Republica del Salvador N35-50, Quito',N'Quito',     '022345004', N'contacto@pizzahut-ec.com',   N'https://cdn.flashbite.com/logos/pizzahut.png',      '10:00', '22:00', 'Aprobado', 0, 2.50, '2023-06-08T09:00:00'),
(N'Dominos Pizza',                    N'Pizza rapida, caliente y recien horneada a domicilio.',                                   3, N'Av. Francisco de Orellana, Guayaquil',    N'Guayaquil', '042345005', N'contacto@dominos-ec.com',    N'https://cdn.flashbite.com/logos/dominos.png',       '10:00', '23:00', 'Aprobado', 0, 2.50, '2023-06-10T09:00:00'),
(N'Subway',                           N'Subs frescos preparados al momento con ingredientes saludables.',                        4, N'Av. Naciones Unidas E4-30, Quito',        N'Quito',     '022345006', N'contacto@subway-ec.com',     N'https://cdn.flashbite.com/logos/subway.png',        '08:00', '21:00', 'Aprobado', 0, 1.75, '2023-06-12T09:00:00'),
(N'Taco Bell',                        N'Comida mexicana rapida: tacos, burritos y nachos.',                                       5, N'Mall del Sol, Guayaquil',                 N'Guayaquil', '042345007', N'contacto@tacobell-ec.com',   N'https://cdn.flashbite.com/logos/tacobell.png',      '10:00', '22:00', 'Aprobado', 0, 2.25, '2023-06-15T09:00:00'),
(N'Los Cebiches de la Ruminahui',    N'Ceviches y mariscos frescos con la sazon tradicional de la costa ecuatoriana.',          6, N'Av. de los Shyris N38-25, Quito',         N'Quito',     '022345008', N'contacto@cebichesrumi.com',  N'https://cdn.flashbite.com/logos/cebichesrumi.png',  '11:00', '21:00', 'Aprobado', 0, 2.00, '2023-06-18T09:00:00'),
(N'Pollos Gus',                       N'Pollo a la broaster con la receta ecuatoriana original desde 1972.',                     2, N'Av. Rodrigo Chavez, Guayaquil',           N'Guayaquil', '042345009', N'contacto@pollosgus.com',     N'https://cdn.flashbite.com/logos/pollosgus.png',     '09:00', '22:00', 'Aprobado', 0, 1.75, '2023-06-20T09:00:00'),
(N'El Espanol',                       N'Panaderia y cafeteria con productos horneados artesanalmente cada dia.',                 7, N'Av. Eloy Alfaro N29-10, Quito',           N'Quito',     '022345010', N'contacto@elespanol-ec.com',  N'https://cdn.flashbite.com/logos/elespanol.png',     '06:30', '20:00', 'Aprobado', 0, 1.50, '2023-06-22T09:00:00');
GO

INSERT INTO HorariosRestaurante (RestauranteID, DiaSemana, HoraApertura, HoraCierre)
SELECT R.RestauranteID, D.DiaSemana, R.HorarioApertura, R.HorarioCierre
FROM Restaurantes R
CROSS JOIN (SELECT CAST(1 AS TINYINT) AS DiaSemana UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
            UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7) D;
GO

/* ============================================================================
   SECCION 17: PRODUCTOS POR RESTAURANTE (12 c/u) E IMAGENES
   ============================================================================ */

-- 1. McDonald's (Hamburguesas=1, Bebidas=7, Postres=8, Acompanantes=9, Ensaladas=10, Combos=13)
INSERT INTO Productos (RestauranteID, CategoriaProductoID, Nombre, Descripcion, Precio, Stock, Disponible, TiempoPreparacionMinutos, ImagenURL) VALUES
(1, 1,  N'Big Mac',                       N'Doble carne, salsa especial, lechuga, queso, pepinillos y cebolla.', 5.50, 150, 1, 8,  N'https://cdn.flashbite.com/prod/mc_bigmac.jpg'),
(1, 1,  N'Cuarto de Libra con Queso',     N'Carne 100% res con doble queso cheddar.',                            5.25, 150, 1, 8,  N'https://cdn.flashbite.com/prod/mc_cuartolibra.jpg'),
(1, 1,  N'McPollo',                       N'Filete de pollo crocante con lechuga y mayonesa.',                   4.75, 150, 1, 7,  N'https://cdn.flashbite.com/prod/mc_mcpollo.jpg'),
(1, 1,  N'Big Mac Doble',                 N'Version con doble porcion de carne extra.',                          6.75, 100, 1, 9,  N'https://cdn.flashbite.com/prod/mc_bigmacdoble.jpg'),
(1, 9,  N'McNuggets 10 piezas',           N'Nuggets de pollo crocantes con salsa a elegir.',                     4.50, 200, 1, 6,  N'https://cdn.flashbite.com/prod/mc_nuggets10.jpg'),
(1, 9,  N'Papas Fritas Grandes',          N'Papas fritas doradas y crocantes tamano grande.',                    2.75, 300, 1, 5,  N'https://cdn.flashbite.com/prod/mc_papasgrandes.jpg'),
(1, 8,  N'McFlurry Oreo',                 N'Helado suave con trozos de galleta Oreo.',                           3.25, 120, 1, 4,  N'https://cdn.flashbite.com/prod/mc_mcflurry.jpg'),
(1, 8,  N'Sundae de Chocolate',           N'Helado de vainilla con cobertura de chocolate caliente.',            2.25, 120, 1, 3,  N'https://cdn.flashbite.com/prod/mc_sundae.jpg'),
(1, 13, N'Cajita Feliz',                  N'Combo infantil con hamburguesa, papas, jugo y juguete.',             4.99, 100, 1, 8,  N'https://cdn.flashbite.com/prod/mc_cajitafeliz.jpg'),
(1, 8,  N'Cono de Helado',                N'Cono de helado suave de vainilla.',                                  1.25, 200, 1, 2,  N'https://cdn.flashbite.com/prod/mc_cono.jpg'),
(1, 7,  N'Coca-Cola Grande',              N'Bebida gaseosa tamano grande servida bien fria.',                    1.75, 250, 1, 2,  N'https://cdn.flashbite.com/prod/mc_cocacola.jpg'),
(1, 10, N'Ensalada Cesar con Pollo',      N'Lechuga fresca, pollo a la parrilla, crotones y aderezo cesar.',     4.95, 80,  1, 6,  N'https://cdn.flashbite.com/prod/mc_ensaladacesar.jpg');
GO

-- 2. KFC (Pollo=2, Acompanantes=9, Combos=13, Ensaladas=10, Panaderia=11, Bebidas=7)
INSERT INTO Productos (RestauranteID, CategoriaProductoID, Nombre, Descripcion, Precio, Stock, Disponible, TiempoPreparacionMinutos, ImagenURL) VALUES
(2, 2,  N'Bucket 8 Piezas',               N'8 piezas de pollo original crocante para compartir.',                14.99, 80,  1, 15, N'https://cdn.flashbite.com/prod/kfc_bucket8.jpg'),
(2, 2,  N'Bucket 12 Piezas',              N'12 piezas de pollo original ideal para grupos grandes.',             20.99, 60,  1, 18, N'https://cdn.flashbite.com/prod/kfc_bucket12.jpg'),
(2, 13, N'Combo Twister',                 N'Wrap de pollo crocante con papas y bebida.',                          6.50, 100, 1, 8,  N'https://cdn.flashbite.com/prod/kfc_twister.jpg'),
(2, 2,  N'Filete Crispy',                 N'Filete de pechuga apanado extra crocante.',                           5.25, 120, 1, 9,  N'https://cdn.flashbite.com/prod/kfc_filete.jpg'),
(2, 2,  N'Alitas Picantes x6',            N'6 alitas banadas en salsa picante.',                                  5.75, 100, 1, 10, N'https://cdn.flashbite.com/prod/kfc_alitas.jpg'),
(2, 9,  N'Papas KFC',                     N'Papas fritas sazonadas con especias de la casa.',                     2.50, 200, 1, 5,  N'https://cdn.flashbite.com/prod/kfc_papas.jpg'),
(2, 9,  N'Pure de Papa con Salsa',        N'Pure cremoso banado en salsa gravy.',                                 2.75, 150, 1, 5,  N'https://cdn.flashbite.com/prod/kfc_pure.jpg'),
(2, 10, N'Ensalada de Col',               N'Ensalada fresca de col con aderezo cremoso.',                         2.25, 100, 1, 3,  N'https://cdn.flashbite.com/prod/kfc_col.jpg'),
(2, 11, N'Pan de Miel',                   N'Pan suave horneado con un toque de miel.',                            1.75, 150, 1, 3,  N'https://cdn.flashbite.com/prod/kfc_pandemiel.jpg'),
(2, 2,  N'Popcorn Chicken',               N'Bocaditos de pollo crocante estilo popcorn.',                         4.25, 130, 1, 7,  N'https://cdn.flashbite.com/prod/kfc_popcorn.jpg'),
(2, 13, N'Combo Familiar',                N'16 piezas de pollo, papas grandes y gaseosa 1.5L.',                  27.99, 40,  1, 20, N'https://cdn.flashbite.com/prod/kfc_combofamiliar.jpg'),
(2, 7,  N'Gaseosa 500ml',                 N'Bebida gaseosa personal de 500ml.',                                   1.50, 250, 1, 2,  N'https://cdn.flashbite.com/prod/kfc_gaseosa.jpg');
GO

-- 3. Burger King (Hamburguesas=1, Acompanantes=9, Postres=8, Combos=13, Ensaladas=10, Panaderia=11)
INSERT INTO Productos (RestauranteID, CategoriaProductoID, Nombre, Descripcion, Precio, Stock, Disponible, TiempoPreparacionMinutos, ImagenURL) VALUES
(3, 1,  N'Whopper',                       N'Hamburguesa a la parrilla con vegetales frescos y salsa especial.',  5.75, 150, 1, 8,  N'https://cdn.flashbite.com/prod/bk_whopper.jpg'),
(3, 1,  N'Whopper con Queso',             N'El clasico Whopper con doble queso cheddar.',                         6.25, 150, 1, 8,  N'https://cdn.flashbite.com/prod/bk_whopperqueso.jpg'),
(3, 1,  N'Doble Whopper',                 N'Whopper con doble carne para el hambre mas grande.',                 7.75, 100, 1, 10, N'https://cdn.flashbite.com/prod/bk_doblewhopper.jpg'),
(3, 1,  N'Chicken Royale',                N'Filete de pollo crocante con lechuga y mayonesa.',                    5.25, 120, 1, 8,  N'https://cdn.flashbite.com/prod/bk_chickenroyale.jpg'),
(3, 9,  N'Nuggets 9 Piezas',              N'9 nuggets de pollo crocante con salsa a elegir.',                     4.25, 180, 1, 6,  N'https://cdn.flashbite.com/prod/bk_nuggets9.jpg'),
(3, 9,  N'Papas Fritas Medianas',         N'Papas fritas doradas tamano mediano.',                                2.50, 250, 1, 5,  N'https://cdn.flashbite.com/prod/bk_papasmedianas.jpg'),
(3, 9,  N'Aros de Cebolla',               N'Aros de cebolla apanados y crocantes.',                               2.95, 150, 1, 6,  N'https://cdn.flashbite.com/prod/bk_arosdecebolla.jpg'),
(3, 8,  N'Sundae de Caramelo',            N'Helado de vainilla con cobertura de caramelo.',                       2.25, 120, 1, 3,  N'https://cdn.flashbite.com/prod/bk_sundaecaramelo.jpg'),
(3, 13, N'Combo Whopper',                 N'Whopper, papas medianas y bebida mediana.',                           8.50, 100, 1, 10, N'https://cdn.flashbite.com/prod/bk_combowhopper.jpg'),
(3, 7,  N'Malteada de Chocolate',         N'Malteada cremosa de chocolate.',                                      3.25, 100, 1, 4,  N'https://cdn.flashbite.com/prod/bk_malteada.jpg'),
(3, 10, N'Ensalada Rodeo',                N'Ensalada fresca con pollo grillado y vegetales.',                     4.75, 70,  1, 6,  N'https://cdn.flashbite.com/prod/bk_ensaladarodeo.jpg'),
(3, 11, N'Croissant de Pollo',            N'Croissant relleno de pollo y queso.',                                 3.75, 90,  1, 7,  N'https://cdn.flashbite.com/prod/bk_croissantpollo.jpg');
GO

-- 4. Pizza Hut (Pizza=3, Panaderia=11, Acompanantes=9, Combos=13, Ensaladas=10, Postres=8, Bebidas=7)
INSERT INTO Productos (RestauranteID, CategoriaProductoID, Nombre, Descripcion, Precio, Stock, Disponible, TiempoPreparacionMinutos, ImagenURL) VALUES
(4, 3,  N'Pizza Pepperoni Familiar',      N'Pizza familiar con abundante pepperoni y queso mozzarella.',        16.99, 60,  1, 20, N'https://cdn.flashbite.com/prod/ph_pepperonifam.jpg'),
(4, 3,  N'Pizza Hawaiana Mediana',        N'Jamon, pina y queso mozzarella sobre masa artesanal.',               12.99, 80,  1, 18, N'https://cdn.flashbite.com/prod/ph_hawaianamed.jpg'),
(4, 3,  N'Pizza Suprema Grande',          N'Carne, pepperoni, pimientos, cebolla y aceitunas.',                  17.99, 60,  1, 22, N'https://cdn.flashbite.com/prod/ph_supremagrande.jpg'),
(4, 3,  N'Pizza Vegetariana',             N'Pimientos, champinones, cebolla, aceitunas y maiz.',                 13.50, 70,  1, 18, N'https://cdn.flashbite.com/prod/ph_vegetariana.jpg'),
(4, 11, N'Pan de Ajo',                    N'Pan artesanal con mantequilla de ajo y perejil.',                     3.50, 150, 1, 8,  N'https://cdn.flashbite.com/prod/ph_pandeajo.jpg'),
(4, 9,  N'Alitas BBQ x8',                 N'8 alitas de pollo banadas en salsa BBQ.',                             7.50, 100, 1, 12, N'https://cdn.flashbite.com/prod/ph_alitasbbq.jpg'),
(4, 13, N'Pasta Alfredo',                 N'Pasta cremosa con salsa alfredo y pollo.',                            6.75, 80,  1, 12, N'https://cdn.flashbite.com/prod/ph_pastaalfredo.jpg'),
(4, 9,  N'Palitos de Queso',              N'Palitos de masa horneados rellenos de queso mozzarella.',            4.99, 120, 1, 10, N'https://cdn.flashbite.com/prod/ph_palitosqueso.jpg'),
(4, 10, N'Ensalada Mixta',                N'Vegetales frescos de temporada con aderezo a eleccion.',             3.99, 90,  1, 6,  N'https://cdn.flashbite.com/prod/ph_ensaladamixta.jpg'),
(4, 8,  N'Brownie de Chocolate',          N'Brownie tibio con trozos de chocolate.',                              2.99, 100, 1, 5,  N'https://cdn.flashbite.com/prod/ph_brownie.jpg'),
(4, 7,  N'Gaseosa 1.5L',                  N'Botella familiar de bebida gaseosa.',                                 3.25, 100, 1, 2,  N'https://cdn.flashbite.com/prod/ph_gaseosa15.jpg'),
(4, 13, N'Combo 2 Pizzas Medianas',       N'Dos pizzas medianas a eleccion mas pan de ajo.',                     24.99, 50,  1, 22, N'https://cdn.flashbite.com/prod/ph_combo2pizzas.jpg');
GO

-- 5. Domino's Pizza (Pizza=3, Panaderia=11, Acompanantes=9, Postres=8, Ensaladas=10, Combos=13, Bebidas=7)
INSERT INTO Productos (RestauranteID, CategoriaProductoID, Nombre, Descripcion, Precio, Stock, Disponible, TiempoPreparacionMinutos, ImagenURL) VALUES
(5, 3,  N'Pizza Pepperoni Personal',      N'Pizza individual con pepperoni y queso mozzarella.',                  6.99, 100, 1, 15, N'https://cdn.flashbite.com/prod/dp_pepperonipersonal.jpg'),
(5, 3,  N'Pizza Especialidad Suprema',    N'Carne, pepperoni, salchicha, pimientos y cebolla.',                  15.99, 70,  1, 20, N'https://cdn.flashbite.com/prod/dp_especialidadsuprema.jpg'),
(5, 3,  N'Pizza 4 Quesos',                N'Mozzarella, parmesano, queso azul y provolone.',                     14.99, 70,  1, 18, N'https://cdn.flashbite.com/prod/dp_4quesos.jpg'),
(5, 3,  N'Pizza Hawaiana Grande',         N'Jamon y pina sobre masa fina crocante tamano grande.',               15.50, 65,  1, 20, N'https://cdn.flashbite.com/prod/dp_hawaianagrande.jpg'),
(5, 11, N'Pan con Ajo y Queso',           N'Pan horneado con ajo, mantequilla y queso gratinado.',                4.25, 120, 1, 9,  N'https://cdn.flashbite.com/prod/dp_panajoqueso.jpg'),
(5, 9,  N'Papas Domino s',                N'Papas fritas sazonadas estilo de la casa.',                           2.75, 200, 1, 6,  N'https://cdn.flashbite.com/prod/dp_papas.jpg'),
(5, 9,  N'Alitas Picantes',               N'Alitas de pollo banadas en salsa picante buffalo.',                   6.99, 100, 1, 12, N'https://cdn.flashbite.com/prod/dp_alitaspicantes.jpg'),
(5, 8,  N'Chocolate Lava Cake',           N'Pastel de chocolate con centro fundido.',                             3.50, 90,  1, 8,  N'https://cdn.flashbite.com/prod/dp_lavacake.jpg'),
(5, 10, N'Ensalada Cesar',                N'Lechuga romana, crotones, parmesano y aderezo cesar.',                4.25, 80,  1, 6,  N'https://cdn.flashbite.com/prod/dp_ensaladacesar.jpg'),
(5, 13, N'Combo Familiar 2 Pizzas',       N'Dos pizzas grandes a eleccion con pan de ajo incluido.',             26.99, 45,  1, 22, N'https://cdn.flashbite.com/prod/dp_combofamiliar.jpg'),
(5, 7,  N'Gaseosa 2L',                    N'Botella grande de bebida gaseosa para compartir.',                    3.75, 100, 1, 2,  N'https://cdn.flashbite.com/prod/dp_gaseosa2l.jpg'),
(5, 3,  N'Pizza Vegetariana Mediana',     N'Champinones, pimientos, cebolla, tomate y aceitunas.',               12.50, 75,  1, 18, N'https://cdn.flashbite.com/prod/dp_vegetarianamed.jpg');
GO

-- 6. Subway (Sandwiches=4, Ensaladas=10, Postres=8, Bebidas=7, Combos=13)
INSERT INTO Productos (RestauranteID, CategoriaProductoID, Nombre, Descripcion, Precio, Stock, Disponible, TiempoPreparacionMinutos, ImagenURL) VALUES
(6, 4,  N'Sub Italiano BMT 15cm',         N'Salami, pepperoni y jamon con vegetales frescos.',                    4.99, 120, 1, 6,  N'https://cdn.flashbite.com/prod/sw_italianobmt.jpg'),
(6, 4,  N'Sub Pollo Teriyaki 15cm',       N'Pollo a la teriyaki con vegetales frescos.',                          5.25, 120, 1, 6,  N'https://cdn.flashbite.com/prod/sw_pollotteriyaki.jpg'),
(6, 4,  N'Sub Atun 15cm',                 N'Atun mezclado con mayonesa y vegetales frescos.',                     4.75, 100, 1, 6,  N'https://cdn.flashbite.com/prod/sw_atun.jpg'),
(6, 4,  N'Sub Vegetariano 15cm',          N'Vegetales frescos, queso y aderezos a eleccion.',                     4.25, 100, 1, 5,  N'https://cdn.flashbite.com/prod/sw_vegetariano.jpg'),
(6, 4,  N'Sub Pavo y Jamon 30cm',         N'Version grande con pavo, jamon y vegetales frescos.',                 8.50, 80,  1, 8,  N'https://cdn.flashbite.com/prod/sw_pavojamon30.jpg'),
(6, 4,  N'Wrap de Pollo',                 N'Tortilla integral con pollo grillado y vegetales.',                   4.99, 90,  1, 6,  N'https://cdn.flashbite.com/prod/sw_wrappollo.jpg'),
(6, 10, N'Ensalada de Pollo',             N'Vegetales frescos con pollo grillado en cubos.',                      5.25, 70,  1, 6,  N'https://cdn.flashbite.com/prod/sw_ensaladapollo.jpg'),
(6, 8,  N'Galleta de Chispas',            N'Galleta horneada con chispas de chocolate.',                          1.25, 200, 1, 2,  N'https://cdn.flashbite.com/prod/sw_galletachispas.jpg'),
(6, 8,  N'Cookies Surtidas',              N'Paquete de galletas surtidas de distintos sabores.',                  3.25, 150, 1, 2,  N'https://cdn.flashbite.com/prod/sw_cookiessurtidas.jpg'),
(6, 9,  N'Papas Lays',                    N'Funda individual de papas fritas.',                                   1.50, 200, 1, 1,  N'https://cdn.flashbite.com/prod/sw_papaslays.jpg'),
(6, 7,  N'Jugo Natural',                  N'Jugo natural de frutas de temporada.',                                2.25, 100, 1, 3,  N'https://cdn.flashbite.com/prod/sw_jugonatural.jpg'),
(6, 13, N'Combo Sub del Dia',             N'Sub 15cm, papas y bebida a eleccion.',                                7.25, 90,  1, 8,  N'https://cdn.flashbite.com/prod/sw_combosubdeldia.jpg');
GO

-- 7. Taco Bell (Tacos y Burritos=5, Acompanantes=9, Postres=8, Bebidas=7, Combos=13)
INSERT INTO Productos (RestauranteID, CategoriaProductoID, Nombre, Descripcion, Precio, Stock, Disponible, TiempoPreparacionMinutos, ImagenURL) VALUES
(7, 5,  N'Crunchwrap Supreme',            N'Tortilla envuelta con carne, queso, lechuga y tomate.',               5.99, 100, 1, 8,  N'https://cdn.flashbite.com/prod/tb_crunchwrap.jpg'),
(7, 5,  N'Taco Supreme',                  N'Taco crocante con carne, queso y crema.',                             2.25, 200, 1, 5,  N'https://cdn.flashbite.com/prod/tb_tacosupreme.jpg'),
(7, 5,  N'Burrito Supreme',               N'Burrito grande con carne, frijoles, arroz y queso.',                  5.25, 120, 1, 7,  N'https://cdn.flashbite.com/prod/tb_burritosupreme.jpg'),
(7, 5,  N'Quesadilla de Pollo',           N'Tortilla de trigo rellena de pollo y queso fundido.',                 4.75, 100, 1, 7,  N'https://cdn.flashbite.com/prod/tb_quesadillapollo.jpg'),
(7, 9,  N'Nachos BellGrande',             N'Nachos con carne, queso, frijoles y guacamole.',                      6.50, 80,  1, 8,  N'https://cdn.flashbite.com/prod/tb_nachosbellgrande.jpg'),
(7, 5,  N'Chalupa Supreme',               N'Pan frito relleno de carne, lechuga y tomate.',                       3.50, 100, 1, 7,  N'https://cdn.flashbite.com/prod/tb_chalupasupreme.jpg'),
(7, 5,  N'Taco Dorado x3',                N'Tres tacos dorados crocantes rellenos de carne.',                     4.50, 100, 1, 6,  N'https://cdn.flashbite.com/prod/tb_tacodoradox3.jpg'),
(7, 5,  N'Burrito de Frijoles',           N'Burrito vegetariano de frijoles refritos y queso.',                   3.99, 90,  1, 6,  N'https://cdn.flashbite.com/prod/tb_burritofrijoles.jpg'),
(7, 9,  N'Papas Nacho Fries',             N'Papas sazonadas con especias nacho.',                                 2.99, 150, 1, 5,  N'https://cdn.flashbite.com/prod/tb_nachofries.jpg'),
(7, 8,  N'Churros',                       N'Churros crocantes espolvoreados con azucar y canela.',                2.50, 120, 1, 6,  N'https://cdn.flashbite.com/prod/tb_churros.jpg'),
(7, 7,  N'Freeze de Frutos Rojos',        N'Bebida helada frutal estilo slush.',                                  2.75, 100, 1, 4,  N'https://cdn.flashbite.com/prod/tb_freeze.jpg'),
(7, 13, N'Combo Taco Party',              N'2 tacos, burrito, nachos y bebida.',                                 10.99, 60,  1, 10, N'https://cdn.flashbite.com/prod/tb_combotacoparty.jpg');
GO

-- 8. Los Cebiches de la Ruminahui (Mariscos=6, Bebidas=7, Acompanantes=9, Combos=13)
INSERT INTO Productos (RestauranteID, CategoriaProductoID, Nombre, Descripcion, Precio, Stock, Disponible, TiempoPreparacionMinutos, ImagenURL) VALUES
(8, 6,  N'Ceviche de Camaron',            N'Camaron fresco marinado en limon con cebolla y cilantro.',            8.50, 80,  1, 15, N'https://cdn.flashbite.com/prod/cr_cevichecamaron.jpg'),
(8, 6,  N'Ceviche Mixto',                 N'Mezcla de camaron, pescado y concha en salsa citrica.',               9.75, 70,  1, 15, N'https://cdn.flashbite.com/prod/cr_cevichemixto.jpg'),
(8, 6,  N'Ceviche de Pescado',            N'Pescado blanco fresco marinado con especias de la costa.',            7.50, 80,  1, 14, N'https://cdn.flashbite.com/prod/cr_cevichepescado.jpg'),
(8, 6,  N'Arroz Marinero',                N'Arroz con mariscos variados y salsa criolla.',                        9.25, 60,  1, 18, N'https://cdn.flashbite.com/prod/cr_arrozmarinero.jpg'),
(8, 6,  N'Encebollado de Pescado',        N'Caldo tradicional de pescado con yuca y cebolla curtida.',            6.75, 80,  1, 16, N'https://cdn.flashbite.com/prod/cr_encebollado.jpg'),
(8, 6,  N'Camarones Apanados',            N'Camarones empanizados y fritos, crocantes por fuera.',                8.99, 70,  1, 14, N'https://cdn.flashbite.com/prod/cr_camaronesapanados.jpg'),
(8, 6,  N'Cazuela de Mariscos',           N'Cazuela horneada con mariscos, mani y verde.',                        9.50, 50,  1, 20, N'https://cdn.flashbite.com/prod/cr_cazuela.jpg'),
(8, 6,  N'Sopa Marinera',                 N'Sopa caliente con variedad de mariscos frescos.',                     7.99, 60,  1, 17, N'https://cdn.flashbite.com/prod/cr_sopamarinera.jpg'),
(8, 9,  N'Patacones con Camaron',         N'Patacones verdes cubiertos con camaron salteado.',                    6.50, 90,  1, 12, N'https://cdn.flashbite.com/prod/cr_pataconescamaron.jpg'),
(8, 6,  N'Corviche',                      N'Bolitas de platano verde y pescado, fritas.',                         3.50, 100, 1, 10, N'https://cdn.flashbite.com/prod/cr_corviche.jpg'),
(8, 7,  N'Jugo de Maracuya',              N'Jugo natural de maracuya recien preparado.',                          2.25, 100, 1, 4,  N'https://cdn.flashbite.com/prod/cr_jugomaracuya.jpg'),
(8, 7,  N'Cerveza Nacional',              N'Cerveza ecuatoriana bien fria, presentacion personal.',               2.75, 120, 1, 1,  N'https://cdn.flashbite.com/prod/cr_cerveza.jpg');
GO

-- 9. Pollos Gus (Pollo=2, Acompanantes=9, Ensaladas=10, Combos=13, Bebidas=7, Postres=8)
INSERT INTO Productos (RestauranteID, CategoriaProductoID, Nombre, Descripcion, Precio, Stock, Disponible, TiempoPreparacionMinutos, ImagenURL) VALUES
(9, 2,  N'Pollo a la Broaster 1/4',       N'Cuarto de pollo broaster con la receta original.',                    3.75, 150, 1, 12, N'https://cdn.flashbite.com/prod/pg_broaster14.jpg'),
(9, 2,  N'Pollo a la Broaster 1/2',       N'Medio pollo broaster crocante y jugoso.',                             6.99, 100, 1, 15, N'https://cdn.flashbite.com/prod/pg_broaster12.jpg'),
(9, 2,  N'Pollo Entero Broaster',         N'Pollo entero broaster ideal para compartir en familia.',             12.99, 60,  1, 22, N'https://cdn.flashbite.com/prod/pg_broasterentero.jpg'),
(9, 2,  N'Alitas Picantes x8',            N'8 alitas de pollo con salsa picante de la casa.',                     6.50, 100, 1, 12, N'https://cdn.flashbite.com/prod/pg_alitas8.jpg'),
(9, 9,  N'Papas con Salsa de la Casa',    N'Papas fritas acompanadas de salsa especial Gus.',                     2.99, 200, 1, 6,  N'https://cdn.flashbite.com/prod/pg_papassalsacasa.jpg'),
(9, 9,  N'Yuca Frita',                    N'Yuca frita crocante por fuera y suave por dentro.',                   2.50, 150, 1, 8,  N'https://cdn.flashbite.com/prod/pg_yucafrita.jpg'),
(9, 10, N'Ensalada de Verduras',          N'Mezcla fresca de verduras de temporada.',                             2.25, 100, 1, 5,  N'https://cdn.flashbite.com/prod/pg_ensaladaverduras.jpg'),
(9, 13, N'Combo Familiar Gus',            N'Pollo entero, papas grandes, ensalada y gaseosa familiar.',          18.99, 40,  1, 25, N'https://cdn.flashbite.com/prod/pg_combofamiliar.jpg'),
(9, 9,  N'Arroz con Menestra',            N'Arroz blanco acompanado de menestra de lenteja.',                     2.75, 120, 1, 8,  N'https://cdn.flashbite.com/prod/pg_arrozmenestra.jpg'),
(9, 9,  N'Patacones',                     N'Tajadas de platano verde fritas y aplastadas.',                       2.25, 150, 1, 8,  N'https://cdn.flashbite.com/prod/pg_patacones.jpg'),
(9, 7,  N'Gaseosa Familiar',              N'Botella de gaseosa de 2 litros para compartir.',                      3.50, 100, 1, 2,  N'https://cdn.flashbite.com/prod/pg_gaseosafamiliar.jpg'),
(9, 8,  N'Torta de Chocolate',            N'Porcion de torta de chocolate humeda casera.',                        2.99, 80,  1, 4,  N'https://cdn.flashbite.com/prod/pg_tortachocolate.jpg');
GO

-- 10. El Espanol (Panaderia=11, Desayunos=12, Bebidas=7, Postres=8, Sandwiches=4)
INSERT INTO Productos (RestauranteID, CategoriaProductoID, Nombre, Descripcion, Precio, Stock, Disponible, TiempoPreparacionMinutos, ImagenURL) VALUES
(10, 11, N'Croissant de Jamon y Queso',    N'Croissant horneado relleno de jamon y queso fundido.',                2.75, 100, 1, 8,  N'https://cdn.flashbite.com/prod/es_croissantjamonqueso.jpg'),
(10, 4,  N'Baguette Mixta',                N'Pan baguette con jamon, queso y vegetales frescos.',                  3.50, 90,  1, 7,  N'https://cdn.flashbite.com/prod/es_baguettemixta.jpg'),
(10, 11, N'Empanada de Pollo',             N'Empanada horneada rellena de pollo desmenuzado.',                     1.75, 150, 1, 6,  N'https://cdn.flashbite.com/prod/es_empanadapollo.jpg'),
(10, 11, N'Empanada de Queso',             N'Empanada horneada rellena de queso mozzarella.',                      1.50, 150, 1, 6,  N'https://cdn.flashbite.com/prod/es_empanadaqueso.jpg'),
(10, 8,  N'Torta de Naranja',              N'Porcion de torta esponjosa de naranja.',                              2.50, 80,  1, 4,  N'https://cdn.flashbite.com/prod/es_tortanaranja.jpg'),
(10, 12, N'Cafe Americano',                N'Cafe negro filtrado recien preparado.',                               1.75, 200, 1, 3,  N'https://cdn.flashbite.com/prod/es_cafeamericano.jpg'),
(10, 12, N'Cappuccino',                    N'Cafe espresso con leche vaporizada y espuma.',                        2.25, 180, 1, 4,  N'https://cdn.flashbite.com/prod/es_cappuccino.jpg'),
(10, 11, N'Pan de Yuca',                   N'Pan tradicional ecuatoriano hecho con almidon de yuca y queso.',      1.25, 200, 1, 5,  N'https://cdn.flashbite.com/prod/es_pandeyuca.jpg'),
(10, 12, N'Quiche de Espinaca',            N'Tarta horneada de espinaca con queso y huevo.',                       3.25, 70,  1, 8,  N'https://cdn.flashbite.com/prod/es_quicheespinaca.jpg'),
(10, 4,  N'Sandwich Club',                 N'Sandwich triple de pollo, tocino, lechuga y tomate.',                 4.25, 80,  1, 8,  N'https://cdn.flashbite.com/prod/es_sandwichclub.jpg'),
(10, 7,  N'Jugo de Naranja Natural',       N'Jugo de naranja recien exprimido.',                                   2.00, 100, 1, 3,  N'https://cdn.flashbite.com/prod/es_jugonaranja.jpg'),
(10, 11, N'Tabla de Quesos y Embutidos',   N'Seleccion de quesos y embutidos finos con pan artesanal.',            8.99, 40,  1, 10, N'https://cdn.flashbite.com/prod/es_tablaquesos.jpg');
GO

-- Imagenes adicionales (galeria) para el producto insignia de cada restaurante
INSERT INTO ImagenesProducto (ProductoID, URL, EsPrincipal)
SELECT ProductoID, ImagenURL, 1 FROM Productos;
GO
INSERT INTO ImagenesProducto (ProductoID, URL, EsPrincipal) VALUES
(1,  N'https://cdn.flashbite.com/prod/gallery/mc_bigmac_2.jpg', 0),
(13, N'https://cdn.flashbite.com/prod/gallery/kfc_bucket8_2.jpg', 0),
(25, N'https://cdn.flashbite.com/prod/gallery/bk_whopper_2.jpg', 0),
(37, N'https://cdn.flashbite.com/prod/gallery/ph_pepperonifam_2.jpg', 0),
(49, N'https://cdn.flashbite.com/prod/gallery/dp_pepperonipersonal_2.jpg', 0),
(61, N'https://cdn.flashbite.com/prod/gallery/sw_italianobmt_2.jpg', 0),
(73, N'https://cdn.flashbite.com/prod/gallery/tb_crunchwrap_2.jpg', 0),
(85, N'https://cdn.flashbite.com/prod/gallery/cr_cevichecamaron_2.jpg', 0),
(97, N'https://cdn.flashbite.com/prod/gallery/pg_broaster14_2.jpg', 0),
(109,N'https://cdn.flashbite.com/prod/gallery/es_croissantjamonqueso_2.jpg', 0);
GO

/* ============================================================================
   SECCION 18: CUPONES, PROMOCIONES Y FAVORITOS
   ============================================================================ */

INSERT INTO Cupones (Codigo, Descripcion, TipoDescuento, Valor, MontoMinimoCompra, FechaInicio, FechaFin, UsoMaximo, UsosActuales, Activo) VALUES
('BIENVENIDA10',  N'10% de descuento para nuevos clientes',              'Porcentaje', 10.00, 8.00,  '2024-01-01', '2026-12-31', 500, 0, 1),
('FLASH15',       N'15% de descuento en pedidos mayores a 15 dolares',   'Porcentaje', 15.00, 15.00, '2024-01-01', '2026-12-31', 300, 0, 1),
('ENVIOGRATIS',   N'Descuento equivalente al costo de envio',            'Monto',       2.50, 10.00, '2024-01-01', '2026-12-31', 400, 0, 1),
('AHORRA5',       N'5 dolares de descuento en tu pedido',                'Monto',       5.00, 20.00, '2024-01-01', '2026-12-31', 200, 0, 1),
('FINDESEMANA20', N'20% de descuento los fines de semana',               'Porcentaje', 20.00, 12.00, '2024-01-01', '2026-12-31', 250, 0, 1),
('PRIMERPEDIDO',  N'3 dolares de descuento en tu primer pedido',         'Monto',       3.00, 5.00,  '2024-01-01', '2026-12-31', 500, 0, 1);
GO

INSERT INTO Promociones (RestauranteID, Nombre, Descripcion, DescuentoPorcentaje, FechaInicio, FechaFin, Activo) VALUES
(1, N'Martes de Combo',        N'15% de descuento en combos todos los martes.',            15.00, '2024-01-01', '2026-12-31', 1),
(2, N'Bucket a Mitad de Precio', N'20% de descuento en buckets los miercoles.',            20.00, '2024-01-01', '2026-12-31', 1),
(3, N'Whopper Days',           N'10% de descuento en la linea Whopper.',                   10.00, '2024-01-01', '2026-12-31', 1),
(4, N'2x1 en Pan de Ajo',      N'Segundo pan de ajo con 50% de descuento.',                50.00, '2024-01-01', '2026-12-31', 1),
(5, N'Pizza Party',            N'25% de descuento en pizzas familiares los fines de semana.',25.00,'2024-01-01', '2026-12-31', 1),
(8, N'Ceviche del Dia',        N'12% de descuento en ceviches de lunes a jueves.',          12.00, '2024-01-01', '2026-12-31', 1),
(9, N'Combo Familiar Gus',     N'15% de descuento en combo familiar los domingos.',         15.00, '2024-01-01', '2026-12-31', 1),
(10,N'Desayuno Madrugador',    N'10% de descuento en desayunos antes de las 9am.',          10.00, '2024-01-01', '2026-12-31', 1);
GO

INSERT INTO Favoritos (ClienteID, RestauranteID) VALUES
(1,1),(1,8),(2,2),(2,9),(3,4),(3,5),(4,6),(5,1),(5,3),(6,8),
(7,2),(7,9),(8,4),(9,5),(10,1),(11,8),(12,3),(13,9),(14,6),(15,10),
(16,1),(17,8),(18,2),(19,4),(20,10);
GO

/* ============================================================================
   SECCION 19: GENERACION MASIVA DE PEDIDOS (80), DETALLE, HISTORIAL,
   PAGOS Y FACTURAS.
   Se utiliza un bloque procedural (T-SQL) para garantizar integridad
   referencial real: cada pedido toma productos del restaurante elegido,
   una direccion del cliente elegido, y totales calculados a partir de las
   lineas de detalle (Subtotal, IVA 15%, envio y total).
   Distribucion de estados (deterministica para asegurar suficientes
   pedidos "Entregado" con los que generar las calificaciones pedidas):
     - Pedidos 1-60  : Entregado  (permiten calificar restaurante/delivery)
     - Pedidos 61-70 : Cancelado
     - Pedidos 71-80 : Activos (Pendiente/Aceptado/Preparando/En camino)
   ============================================================================ */

SET NOCOUNT ON;
GO

DECLARE @i INT = 1;
DECLARE @TotalPedidos INT = 80;

DECLARE @EstadoPendienteID  INT = (SELECT EstadoPedidoID FROM EstadoPedido WHERE Nombre = N'Pendiente');
DECLARE @EstadoAceptadoID   INT = (SELECT EstadoPedidoID FROM EstadoPedido WHERE Nombre = N'Aceptado');
DECLARE @EstadoPreparandoID INT = (SELECT EstadoPedidoID FROM EstadoPedido WHERE Nombre = N'Preparando');
DECLARE @EstadoEnCaminoID   INT = (SELECT EstadoPedidoID FROM EstadoPedido WHERE Nombre = N'En camino');
DECLARE @EstadoEntregadoID  INT = (SELECT EstadoPedidoID FROM EstadoPedido WHERE Nombre = N'Entregado');
DECLARE @EstadoCanceladoID  INT = (SELECT EstadoPedidoID FROM EstadoPedido WHERE Nombre = N'Cancelado');

DECLARE @ClienteID INT, @RestauranteID INT, @DeliveryID INT, @DireccionID INT;
DECLARE @EstadoPedidoID INT, @MetodoPagoID INT, @CuponID INT;
DECLARE @Subtotal DECIMAL(10,2), @IVA DECIMAL(10,2), @CostoEnvio DECIMAL(10,2);
DECLARE @Descuento DECIMAL(10,2), @Total DECIMAL(10,2);
DECLARE @FechaPedido DATETIME2(0), @DiasAtras INT, @MinutosAtras INT;
DECLARE @NumItems INT, @j INT, @ProductoID INT, @PrecioUnitario DECIMAL(10,2), @Cantidad INT;
DECLARE @PedidoID INT, @EstadoPago VARCHAR(20), @PagoID INT;
DECLARE @EstadoRand INT;

DECLARE @Items TABLE (ProductoID INT, Cantidad INT, PrecioUnitario DECIMAL(10,2));

WHILE @i <= @TotalPedidos
BEGIN
    DELETE FROM @Items;

    SET @ClienteID = ((@i - 1) % 25) + 1;
    SET @RestauranteID = ABS(CHECKSUM(NEWID())) % 10 + 1;
    SELECT TOP 1 @DireccionID = DireccionID FROM Direcciones WHERE ClienteID = @ClienteID ORDER BY NEWID();
    SET @MetodoPagoID = ABS(CHECKSUM(NEWID())) % 5 + 1;

    IF @i <= 60
    BEGIN
        SET @EstadoPedidoID = @EstadoEntregadoID;
        SET @DiasAtras = ABS(CHECKSUM(NEWID())) % 88 + 3;
        SET @DeliveryID = ABS(CHECKSUM(NEWID())) % 15 + 1;
    END
    ELSE IF @i <= 70
    BEGIN
        SET @EstadoPedidoID = @EstadoCanceladoID;
        SET @DiasAtras = ABS(CHECKSUM(NEWID())) % 88 + 3;
        SET @DeliveryID = NULL;
    END
    ELSE
    BEGIN
        SET @DiasAtras = ABS(CHECKSUM(NEWID())) % 3;
        SET @EstadoRand = (@i - 71) % 4;
        IF @EstadoRand = 0
        BEGIN SET @EstadoPedidoID = @EstadoPendienteID;  SET @DeliveryID = NULL; END
        ELSE IF @EstadoRand = 1
        BEGIN SET @EstadoPedidoID = @EstadoAceptadoID;   SET @DeliveryID = ABS(CHECKSUM(NEWID())) % 15 + 1; END
        ELSE IF @EstadoRand = 2
        BEGIN SET @EstadoPedidoID = @EstadoPreparandoID; SET @DeliveryID = ABS(CHECKSUM(NEWID())) % 15 + 1; END
        ELSE
        BEGIN SET @EstadoPedidoID = @EstadoEnCaminoID;   SET @DeliveryID = ABS(CHECKSUM(NEWID())) % 15 + 1; END
    END

    SET @MinutosAtras = ABS(CHECKSUM(NEWID())) % 720;
    SET @FechaPedido = DATEADD(MINUTE, -@MinutosAtras, DATEADD(DAY, -@DiasAtras, SYSDATETIME()));

    -- Lineas de detalle: entre 2 y 5 productos del restaurante elegido
    SET @NumItems = ABS(CHECKSUM(NEWID())) % 4 + 2;
    SET @j = 1;
    WHILE @j <= @NumItems
    BEGIN
        SELECT TOP 1 @ProductoID = ProductoID, @PrecioUnitario = Precio
        FROM Productos
        WHERE RestauranteID = @RestauranteID
        ORDER BY NEWID();

        SET @Cantidad = ABS(CHECKSUM(NEWID())) % 3 + 1;

        IF NOT EXISTS (SELECT 1 FROM @Items WHERE ProductoID = @ProductoID)
            INSERT INTO @Items (ProductoID, Cantidad, PrecioUnitario) VALUES (@ProductoID, @Cantidad, @PrecioUnitario);

        SET @j += 1;
    END

    SELECT @Subtotal = SUM(Cantidad * PrecioUnitario) FROM @Items;

    SELECT @CostoEnvio = CostoEnvioBase FROM Restaurantes WHERE RestauranteID = @RestauranteID;
    SET @CostoEnvio = CAST(@CostoEnvio + (ABS(CHECKSUM(NEWID())) % 200) / 100.0 AS DECIMAL(10,2));

    SET @CuponID = NULL;
    SET @Descuento = 0;
    IF @i % 7 = 0
    BEGIN
        SELECT TOP 1 @CuponID = CuponID,
               @Descuento = CASE WHEN TipoDescuento = 'Porcentaje' THEN CAST(@Subtotal * (Valor / 100.0) AS DECIMAL(10,2)) ELSE Valor END
        FROM Cupones
        WHERE Activo = 1 AND MontoMinimoCompra <= @Subtotal
        ORDER BY NEWID();
        IF @Descuento IS NULL SET @Descuento = 0;
    END

    SET @IVA = CAST((@Subtotal - @Descuento) * 0.15 AS DECIMAL(10,2));
    SET @Total = @Subtotal + @IVA + @CostoEnvio - @Descuento;

    INSERT INTO Pedidos (ClienteID, RestauranteID, DeliveryID, DireccionEntregaID, EstadoPedidoID, MetodoPagoID, CuponID,
                          Subtotal, IVA, CostoEnvio, Descuento, Total, FechaPedido, FechaEntregaEstimada, FechaEntregaReal, Notas)
    VALUES (@ClienteID, @RestauranteID, @DeliveryID, @DireccionID, @EstadoPedidoID, @MetodoPagoID, @CuponID,
            @Subtotal, @IVA, @CostoEnvio, @Descuento, @Total, @FechaPedido,
            DATEADD(MINUTE, 45, @FechaPedido),
            CASE WHEN @EstadoPedidoID = @EstadoEntregadoID THEN DATEADD(MINUTE, 40 + ABS(CHECKSUM(NEWID())) % 20, @FechaPedido) ELSE NULL END,
            NULL);

    SET @PedidoID = SCOPE_IDENTITY();

    INSERT INTO DetallePedido (PedidoID, ProductoID, Cantidad, PrecioUnitario)
    SELECT @PedidoID, ProductoID, Cantidad, PrecioUnitario FROM @Items;

    -- Historial de estados: reconstruye la secuencia recorrida hasta el estado actual
    INSERT INTO HistorialEstados (PedidoID, EstadoPedidoID, FechaCambio, Comentario)
    VALUES (@PedidoID, @EstadoPendienteID, @FechaPedido, N'Pedido creado por el cliente');

    IF @EstadoPedidoID = @EstadoCanceladoID
        INSERT INTO HistorialEstados (PedidoID, EstadoPedidoID, FechaCambio, Comentario)
        VALUES (@PedidoID, @EstadoCanceladoID, DATEADD(MINUTE, 8, @FechaPedido), N'Pedido cancelado');

    IF @EstadoPedidoID IN (@EstadoAceptadoID, @EstadoPreparandoID, @EstadoEnCaminoID, @EstadoEntregadoID)
        INSERT INTO HistorialEstados (PedidoID, EstadoPedidoID, FechaCambio, Comentario)
        VALUES (@PedidoID, @EstadoAceptadoID, DATEADD(MINUTE, 5, @FechaPedido), N'Pedido aceptado por el restaurante');

    IF @EstadoPedidoID IN (@EstadoPreparandoID, @EstadoEnCaminoID, @EstadoEntregadoID)
        INSERT INTO HistorialEstados (PedidoID, EstadoPedidoID, FechaCambio, Comentario)
        VALUES (@PedidoID, @EstadoPreparandoID, DATEADD(MINUTE, 10, @FechaPedido), N'Restaurante preparando el pedido');

    IF @EstadoPedidoID IN (@EstadoEnCaminoID, @EstadoEntregadoID)
        INSERT INTO HistorialEstados (PedidoID, EstadoPedidoID, FechaCambio, Comentario)
        VALUES (@PedidoID, @EstadoEnCaminoID, DATEADD(MINUTE, 25, @FechaPedido), N'Repartidor en camino hacia el cliente');

    IF @EstadoPedidoID = @EstadoEntregadoID
        INSERT INTO HistorialEstados (PedidoID, EstadoPedidoID, FechaCambio, Comentario)
        VALUES (@PedidoID, @EstadoEntregadoID, DATEADD(MINUTE, 45, @FechaPedido), N'Pedido entregado al cliente');

    -- Pago asociado (relacion 1 a 1 con el pedido)
    SET @EstadoPago = CASE
                            WHEN @EstadoPedidoID = @EstadoEntregadoID THEN 'Completado'
                            WHEN @EstadoPedidoID = @EstadoCanceladoID THEN 'Fallido'
                            ELSE 'Pendiente'
                       END;

    INSERT INTO Pagos (PedidoID, MetodoPagoID, Monto, Estado, FechaPago, ReferenciaTransaccion)
    VALUES (@PedidoID, @MetodoPagoID, @Total, @EstadoPago, @FechaPedido, 'TRX-' + RIGHT('000000' + CAST(@PedidoID AS VARCHAR(6)), 6));

    SET @PagoID = SCOPE_IDENTITY();

    IF @EstadoPago <> 'Pendiente'
        INSERT INTO HistorialPagos (PagoID, EstadoAnterior, EstadoNuevo, FechaCambio)
        VALUES (@PagoID, 'Pendiente', @EstadoPago, DATEADD(MINUTE, 46, @FechaPedido));

    -- Factura: se emite unicamente cuando el pedido fue entregado
    IF @EstadoPedidoID = @EstadoEntregadoID
        INSERT INTO Facturas (PedidoID, NumeroFactura, ClienteID, Subtotal, IVA, Total, FechaEmision)
        VALUES (@PedidoID, 'FAC-' + RIGHT('000000' + CAST(@PedidoID AS VARCHAR(6)), 6), @ClienteID, @Subtotal, @IVA, @Total, DATEADD(MINUTE, 46, @FechaPedido));

    SET @i += 1;
END
GO

/* ============================================================================
   SECCION 20: CALIFICACIONES (50 de restaurante, 40 de repartidor)
   Se seleccionan de forma aleatoria entre los pedidos "Entregado" (60
   disponibles), lo que garantiza que ambas cuotas solicitadas se cumplan.
   Un mismo pedido puede tener calificacion de restaurante y de delivery,
   igual que en apps reales (se pide calificar ambos tras la entrega).
   ============================================================================ */

DECLARE @ComentariosRestaurante TABLE (Idx INT IDENTITY(0,1), Texto NVARCHAR(500));
INSERT INTO @ComentariosRestaurante (Texto) VALUES
(N'Excelente comida y rapidez en la entrega.'),
(N'Muy buen sabor, volveria a pedir sin duda.'),
(N'La comida llego un poco fria, podria mejorar.'),
(N'Buena atencion y presentacion de los platos.'),
(N'El pedido tardo mas de lo esperado.'),
(N'Todo perfecto, tal como lo pedi.'),
(N'Buena relacion calidad-precio.'),
(N'Cumplio con mis expectativas, recomendado.');

DECLARE @ComentariosDelivery TABLE (Idx INT IDENTITY(0,1), Texto NVARCHAR(500));
INSERT INTO @ComentariosDelivery (Texto) VALUES
(N'El repartidor fue muy amable y puntual.'),
(N'Entrega rapida, todo en buen estado.'),
(N'Se demoro un poco mas de lo indicado.'),
(N'Excelente actitud del repartidor.'),
(N'Encontro la direccion sin problema.'),
(N'Buen servicio, el pedido llego completo.'),
(N'El repartidor mantuvo comunicacion constante.'),
(N'Todo perfecto con la entrega.');

;WITH EntregadosRest AS (
    SELECT PedidoID, ClienteID, RestauranteID, FechaPedido,
           ROW_NUMBER() OVER (ORDER BY NEWID()) AS rn
    FROM Pedidos
    WHERE EstadoPedidoID = (SELECT EstadoPedidoID FROM EstadoPedido WHERE Nombre = N'Entregado')
)
INSERT INTO CalificacionesRestaurante (PedidoID, ClienteID, RestauranteID, Puntuacion, Comentario, Fecha)
SELECT E.PedidoID, E.ClienteID, E.RestauranteID,
       ABS(CHECKSUM(NEWID())) % 5 + 1,
       (SELECT TOP 1 Texto FROM @ComentariosRestaurante ORDER BY NEWID()),
       DATEADD(HOUR, 2, E.FechaPedido)
FROM EntregadosRest E
WHERE E.rn <= 50;

;WITH EntregadosDel AS (
    SELECT PedidoID, ClienteID, DeliveryID, FechaPedido,
           ROW_NUMBER() OVER (ORDER BY NEWID()) AS rn
    FROM Pedidos
    WHERE EstadoPedidoID = (SELECT EstadoPedidoID FROM EstadoPedido WHERE Nombre = N'Entregado')
      AND DeliveryID IS NOT NULL
)
INSERT INTO CalificacionesDelivery (PedidoID, ClienteID, DeliveryID, Puntuacion, Comentario, Fecha)
SELECT E.PedidoID, E.ClienteID, E.DeliveryID,
       ABS(CHECKSUM(NEWID())) % 5 + 1,
       (SELECT TOP 1 Texto FROM @ComentariosDelivery ORDER BY NEWID()),
       DATEADD(HOUR, 2, E.FechaPedido)
FROM EntregadosDel E
WHERE E.rn <= 40;
GO

-- Recalcular promedios de calificacion en Restaurantes y Delivery
UPDATE R
SET CalificacionPromedio = T.Promedio
FROM Restaurantes R
INNER JOIN (
    SELECT RestauranteID, CAST(AVG(CAST(Puntuacion AS DECIMAL(5,2))) AS DECIMAL(3,2)) AS Promedio
    FROM CalificacionesRestaurante
    GROUP BY RestauranteID
) T ON T.RestauranteID = R.RestauranteID;
GO

UPDATE D
SET CalificacionPromedio = T.Promedio
FROM Delivery D
INNER JOIN (
    SELECT DeliveryID, CAST(AVG(CAST(Puntuacion AS DECIMAL(5,2))) AS DECIMAL(3,2)) AS Promedio
    FROM CalificacionesDelivery
    GROUP BY DeliveryID
) T ON T.DeliveryID = D.DeliveryID;
GO

/* ============================================================================
   SECCION 21: NOTIFICACIONES, SESIONES, TOKENS Y AUDITORIA (muestras)
   ============================================================================ */

INSERT INTO Notificaciones (UsuarioID, Titulo, Mensaje, Tipo, Leida, FechaCreacion) VALUES
(4,  N'Tu pedido fue confirmado',   N'McDonalds acepto tu pedido y lo esta preparando.',      'Pedido',    1, DATEADD(DAY, -5, SYSDATETIME())),
(5,  N'Tu pedido esta en camino',   N'El repartidor Pedro va hacia tu direccion.',            'Pedido',    1, DATEADD(DAY, -4, SYSDATETIME())),
(6,  N'Pedido entregado',           N'Tu pedido de KFC fue entregado. Califica tu experiencia.', 'Pedido', 0, DATEADD(DAY, -3, SYSDATETIME())),
(7,  N'Cupon disponible',           N'Usa el codigo FLASH15 y obten 15% de descuento.',       'Promocion', 0, DATEADD(DAY, -2, SYSDATETIME())),
(8,  N'Bienvenido a FlashBite',     N'Gracias por registrarte, disfruta tu primer pedido.',    'Sistema',   1, DATEADD(DAY, -30, SYSDATETIME())),
(29, N'Nuevo pedido disponible',    N'Tienes un nuevo pedido para recoger en Pizza Hut.',      'Pedido',    1, DATEADD(DAY, -5, SYSDATETIME())),
(30, N'Calificacion recibida',      N'Un cliente califico tu entrega con 5 estrellas.',        'Sistema',   0, DATEADD(DAY, -1, SYSDATETIME())),
(1,  N'Nuevo restaurante pendiente',N'El Espanol solicito unirse a la plataforma.',            'Sistema',   1, DATEADD(DAY, -60, SYSDATETIME()));
GO

INSERT INTO Sesiones (UsuarioID, Token, FechaInicio, FechaExpiracion, DireccionIP, Activa) VALUES
(4, 'sess_a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6', DATEADD(HOUR, -2, SYSDATETIME()), DATEADD(HOUR, 22, SYSDATETIME()), '190.152.10.21', 1),
(5, 'sess_b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7', DATEADD(HOUR, -5, SYSDATETIME()), DATEADD(HOUR, 19, SYSDATETIME()), '190.152.10.22', 1),
(29,'sess_c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8', DATEADD(HOUR, -1, SYSDATETIME()), DATEADD(HOUR, 23, SYSDATETIME()), '190.152.10.55', 1),
(1, 'sess_d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9', DATEADD(HOUR, -8, SYSDATETIME()), DATEADD(HOUR, 16, SYSDATETIME()), '190.152.10.99', 0);
GO

INSERT INTO Tokens (UsuarioID, Token, Tipo, FechaCreacion, FechaExpiracion, Usado) VALUES
(6,  'rec_e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0', 'Recuperacion',   DATEADD(HOUR, -3, SYSDATETIME()), DATEADD(HOUR, 1, SYSDATETIME()), 0),
(9,  'ver_f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1', 'Verificacion',   DATEADD(DAY, -10, SYSDATETIME()), DATEADD(DAY, -9, SYSDATETIME()), 1),
(31, 'ref_g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2', 'RefreshToken',   DATEADD(DAY, -1, SYSDATETIME()), DATEADD(DAY, 29, SYSDATETIME()), 0);
GO

INSERT INTO Auditoria (UsuarioID, Tabla, Accion, RegistroID, FechaAccion, Detalles) VALUES
(1, 'Restaurantes', 'UPDATE', 10, DATEADD(DAY, -55, SYSDATETIME()), N'El administrador aprobo el restaurante El Espanol.'),
(2, 'Productos',    'INSERT', 1,  DATEADD(DAY, -200, SYSDATETIME()), N'Se agrego el producto Big Mac al catalogo de McDonalds.'),
(3, 'Delivery',     'UPDATE', 7,  DATEADD(DAY, -20, SYSDATETIME()), N'Se actualizo el estado del repartidor Henry David Chala a Desconectado.'),
(1, 'Usuarios',     'UPDATE', 15, DATEADD(DAY, -3, SYSDATETIME()),  N'Se reactivo la cuenta del cliente Miguel Angel Herrera.');
GO

/* ============================================================================
   SECCION 22: VERIFICACION FINAL (conteos de control)
   Ejecutar junto con el resto del script para confirmar que la carga de
   datos se completo correctamente.
   ============================================================================ */

SELECT 'Roles' AS Tabla, COUNT(*) AS Total FROM Roles
UNION ALL SELECT 'Usuarios', COUNT(*) FROM Usuarios
UNION ALL SELECT 'Administradores', COUNT(*) FROM Administradores
UNION ALL SELECT 'Clientes', COUNT(*) FROM Clientes
UNION ALL SELECT 'Delivery', COUNT(*) FROM Delivery
UNION ALL SELECT 'Vehiculos', COUNT(*) FROM Vehiculos
UNION ALL SELECT 'Direcciones', COUNT(*) FROM Direcciones
UNION ALL SELECT 'Restaurantes', COUNT(*) FROM Restaurantes
UNION ALL SELECT 'Productos', COUNT(*) FROM Productos
UNION ALL SELECT 'ImagenesProducto', COUNT(*) FROM ImagenesProducto
UNION ALL SELECT 'Cupones', COUNT(*) FROM Cupones
UNION ALL SELECT 'Promociones', COUNT(*) FROM Promociones
UNION ALL SELECT 'Favoritos', COUNT(*) FROM Favoritos
UNION ALL SELECT 'Pedidos', COUNT(*) FROM Pedidos
UNION ALL SELECT 'DetallePedido', COUNT(*) FROM DetallePedido
UNION ALL SELECT 'HistorialEstados', COUNT(*) FROM HistorialEstados
UNION ALL SELECT 'Pagos', COUNT(*) FROM Pagos
UNION ALL SELECT 'HistorialPagos', COUNT(*) FROM HistorialPagos
UNION ALL SELECT 'Facturas', COUNT(*) FROM Facturas
UNION ALL SELECT 'CalificacionesRestaurante', COUNT(*) FROM CalificacionesRestaurante
UNION ALL SELECT 'CalificacionesDelivery', COUNT(*) FROM CalificacionesDelivery
UNION ALL SELECT 'Notificaciones', COUNT(*) FROM Notificaciones
UNION ALL SELECT 'Sesiones', COUNT(*) FROM Sesiones
UNION ALL SELECT 'Tokens', COUNT(*) FROM Tokens
UNION ALL SELECT 'Auditoria', COUNT(*) FROM Auditoria;
GO

PRINT N'FlashBiteDB creada y poblada correctamente.';
GO
