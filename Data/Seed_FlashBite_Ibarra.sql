-- ============================================================================
-- FlashBite - Seed de datos reales: Ibarra, Imbabura, Ecuador
-- Reemplaza POR COMPLETO los restaurantes y productos de prueba.
-- Compatible con Microsoft SQL Server. Ejecutar sobre la base de FlashBite.
-- Imagenes: URLs verificadas de Unsplash y Pexels (libres de uso).
-- Nota: se vacian Orders/OrderDetails/Payments para respetar las claves
--       foraneas hacia Restaurants y Products (datos de prueba).
-- ============================================================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Limpieza en orden de dependencias (hijos -> padres)
DELETE FROM Payments;
DELETE FROM Products;
DELETE FROM OrderDetails;
DELETE FROM Orders;
DELETE FROM Restaurants;

DBCC CHECKIDENT ('Products', RESEED, 0);
DBCC CHECKIDENT ('OrderDetails', RESEED, 0);
DBCC CHECKIDENT ('Restaurants', RESEED, 0);

-- 2. Restaurantes de Ibarra
SET IDENTITY_INSERT Restaurants ON;

INSERT INTO Restaurants (Id, Name, Description, Address, Phone, Category, Latitude, Longitude, ImagePath, OpeningHour, ClosingHour, IsActive) VALUES
(1, N'La Estelita', N'Restaurante mirador con la mejor vista de Ibarra y la laguna de Yahuarcocha. Cocina de autor con ingredientes de la sierra.', N'Km 5 vía a Yuracrucito, mirador de Yahuarcocha, Ibarra', N'062951190', N'Gourmet', 0.3672, -78.0958,
 N'/images/catalog/u1559339352-11d035aa65de-1200.jpg', '12:00:00', '22:00:00', 1),
(2, N'La Casona Ibarreña', N'Comida típica de la sierra en una casona del centro histórico: hornado, llapingachos y fritada de tradición.', N'Bolívar 7-59 y Pedro Moncayo, centro histórico, Ibarra', N'062958721', N'Comida Típica', 0.3496, -78.1195,
 N'/images/catalog/u1552566626-52f8b828add9-1200.jpg', '09:00:00', '21:00:00', 1),
(3, N'Hostería Ajaví', N'Restaurante de la tradicional Hostería Ajaví: cocina internacional, pizzas artesanales y pastas en ambiente familiar.', N'Av. Mariano Acosta 16-38, Ibarra', N'062955221', N'Internacional', 0.3438, -78.129,
 N'/images/catalog/u1517248135467-4c7edcad34c4-1200.jpg', '07:00:00', '22:30:00', 1),
(4, N'El Portón', N'Sabores ibarreños en un patio tradicional: hornado, carne colorada y canelazo.', N'Pedro Moncayo 4-26 y Sucre, Ibarra', N'062609431', N'Comida Típica', 0.3512, -78.1178,
 N'/images/catalog/u1555396273-367ea4eb4db5-1200.jpg', '10:00:00', '21:00:00', 1),
(5, N'La Choza', N'Asados al carbón, pollos al horno y platos criollos en un ambiente campestre.', N'Av. Teodoro Gómez de la Torre 9-45, Ibarra', N'062640173', N'Asados', 0.3446, -78.1226,
 N'/images/catalog/u1521017432531-fbd92d768814-1200.jpg', '11:00:00', '21:30:00', 1),
(6, N'Helados de Paila Rosalía Suárez', N'La heladería más tradicional de Ibarra, desde 1896: helados de paila batidos a mano en bronce sobre hielo.', N'Olmedo 7-79 y Oviedo, Ibarra', N'062958778', N'Heladería y Postres', 0.3507, -78.1198,
 N'/images/catalog/u1516559828984-fb3b99548b21-1200.jpg', '08:00:00', '19:30:00', 1),
(7, N'Café Río', N'Cafetería junto al río Tahuando: café de Imbabura, hamburguesas y postres caseros.', N'Av. 17 de Julio y Aurelio Espinosa Pólit, El Olivo, Ibarra', N'0998214567', N'Cafetería', 0.3572, -78.1103,
 N'/images/catalog/u1554118811-1e0d58224f24-1200.jpg', '07:30:00', '20:30:00', 1),
(8, N'Rincón Manabita', N'Auténtico sabor manabita: ceviches, encebollados y mariscos frescos todos los días.', N'Av. Pérez Guerrero 6-54 y Bolívar, Ibarra', N'0991483920', N'Mariscos', 0.3489, -78.1211,
 N'/images/catalog/u1615141982883-c7ad0e69fd62-1200.jpg', '08:00:00', '17:00:00', 1),
(9, N'El Fogón Costeño', N'Cocina de la costa ecuatoriana: encocados, arroces marineros y camarones con sazón esmeraldeña.', N'Av. Jaime Rivadeneira 5-70 y Oviedo, Ibarra', N'0984012776', N'Mariscos', 0.3524, -78.1249,
 N'/images/catalog/u1535399831218-d5bd36d1a6b3-1200.jpg', '09:00:00', '18:00:00', 1),
(10, N'La Parrilla de Ibarra', N'Cortes a la parrilla, costillas BBQ y pollos a la brasa: carbón encendido todos los días.', N'Av. Cristóbal de Troya 10-15, Ibarra', N'0996305181', N'Parrilla', 0.3568, -78.1247,
 N'/images/catalog/p37069415-1200.jpg', '12:00:00', '22:30:00', 1);

SET IDENTITY_INSERT Restaurants OFF;

-- 3. Productos (RestaurantId respeta la clave foranea)
SET IDENTITY_INSERT Products ON;

-- 1. La Estelita (8 productos)
INSERT INTO Products (Id, RestaurantId, Name, Description, Price, Category, ImagePath, Available, OrderDetailsId) VALUES
(1, 1, N'Lomo Fino a la Pimienta', N'Medallón de lomo fino en salsa de pimienta verde, acompañado de papas rústicas.', 14.50, N'Carnes',
 N'/images/catalog/u1600891964092-4316c288032e-800.jpg', 1, NULL),
(2, 1, N'Trucha a la Plancha', N'Filete de trucha de la sierra a la plancha sobre ensalada fresca de la huerta.', 11.90, N'Pescados',
 N'/images/catalog/u1519708227418-c8fd9a32b7a2-800.jpg', 1, NULL),
(3, 1, N'Filete de Trucha sobre Arroz', N'Filete dorado servido sobre arroz cremoso de hierbas con reducción de la casa.', 12.50, N'Pescados',
 N'/images/catalog/u1580476262798-bddd9f4b7369-800.jpg', 1, NULL),
(4, 1, N'Ensalada César con Pollo', N'Lechugas frescas, pollo grillado, crotones y aderezo césar artesanal.', 7.50, N'Ensaladas',
 N'/images/catalog/u1546069901-ba9599a7e63c-800.jpg', 1, NULL),
(5, 1, N'Langostinos a la Plancha', N'Langostinos enteros a la plancha con mantequilla de ajo y limón.', 16.50, N'Mariscos',
 N'/images/catalog/u1559742811-822873691df8-800.jpg', 1, NULL),
(6, 1, N'Cheesecake de Mora', N'Cheesecake horneado con salsa de mora de Imbabura.', 5.50, N'Postres',
 N'/images/catalog/u1533134242443-d4fd215305ad-800.jpg', 1, NULL),
(7, 1, N'Copa de Helado con Caramelo', N'Helado artesanal bañado en salsa de caramelo con barquillo.', 4.90, N'Postres',
 N'/images/catalog/u1551024506-0bccd828d307-800.jpg', 1, NULL),
(8, 1, N'Limonada de Hierbabuena', N'Limonada natural con hierbabuena fresca y hielo.', 2.90, N'Bebidas',
 N'/images/catalog/u1621263764928-df1444c5e859-800.jpg', 1, NULL);

-- 2. La Casona Ibarreña (11 productos)
INSERT INTO Products (Id, RestaurantId, Name, Description, Price, Category, ImagePath, Available, OrderDetailsId) VALUES
(9, 2, N'Hornado Pastuso', N'Hornado jugoso con tortillas de papa, mote y ají de chochos.', 8.50, N'Comida Típica',
 N'/images/catalog/p36850055-800.jpg', 1, NULL),
(10, 2, N'Llapingachos con Chorizo', N'Tortillas de papa doradas en tiesto con chorizo, huevo frito y ensalada.', 6.50, N'Comida Típica',
 N'/images/catalog/p17091787-800.jpg', 1, NULL),
(11, 2, N'Fritada de la Casona', N'Fritada dorada en paila de bronce, con mote, maduro y encurtido.', 8.00, N'Comida Típica',
 N'/images/catalog/p34837746-800.jpg', 1, NULL),
(12, 2, N'Empanadas de Viento (3 und.)', N'Empanadas crocantes de queso espolvoreadas con azúcar.', 2.50, N'Comida Típica',
 N'/images/catalog/u1673716575331-60b042589b8d-800.jpg', 1, NULL),
(13, 2, N'Caldo de Gallina Criolla', N'Caldo sustancioso de gallina de campo con papa y cilantro.', 4.50, N'Sopas',
 N'/images/catalog/u1603105037880-880cd4edfb0d-800.jpg', 1, NULL),
(14, 2, N'Seco de Chivo', N'Estofado tradicional de chivo en salsa de cerveza y naranjilla, con arroz amarillo.', 7.50, N'Comida Típica',
 N'/images/catalog/u1565557623262-b51c2513a641-800.jpg', 1, NULL),
(15, 2, N'Bandeja Típica Casona', N'Degustación de tortillas, mote, tostado, empanada y ají de la casa.', 10.50, N'Comida Típica',
 N'/images/catalog/p32921658-800.jpg', 1, NULL),
(16, 2, N'Locro de Papa con Aguacate', N'Locro cremoso de papa chola con queso y aguacate.', 4.00, N'Sopas',
 N'/images/catalog/u1613844237701-8f3664fc2eff-800.jpg', 1, NULL),
(17, 2, N'Torta de Tres Leches', N'Bizcocho bañado en tres leches con crema y frutilla.', 4.50, N'Postres',
 N'/images/catalog/u1641848421644-a1603f016f51-800.jpg', 1, NULL),
(18, 2, N'Flan de Caramelo', N'Flan casero de vainilla con caramelo dorado.', 3.50, N'Postres',
 N'/images/catalog/u1780798465831-de1e4d58ee46-800.jpg', 1, NULL),
(19, 2, N'Batido de Mora', N'Batido cremoso de mora andina con leche.', 2.50, N'Bebidas',
 N'/images/catalog/u1553530666-ba11a7da3888-800.jpg', 1, NULL);

-- 3. Hostería Ajaví (12 productos)
INSERT INTO Products (Id, RestaurantId, Name, Description, Price, Category, ImagePath, Available, OrderDetailsId) VALUES
(20, 3, N'Corvina a la Plancha', N'Filete de corvina a la plancha con vegetales salteados.', 10.90, N'Pescados',
 N'/images/catalog/u1467003909585-2f8a72700288-800.jpg', 1, NULL),
(21, 3, N'Espagueti a la Boloñesa', N'Pasta con salsa boloñesa de carne cocinada a fuego lento.', 8.50, N'Pastas',
 N'/images/catalog/u1598866594230-a7c12756260f-800.jpg', 1, NULL),
(22, 3, N'Fettuccine con Lomo Saltado', N'Fettuccine salteado con tiras de lomo y champiñones.', 9.50, N'Pastas',
 N'/images/catalog/u1551183053-bf91a1d81141-800.jpg', 1, NULL),
(23, 3, N'Penne en Salsa Pomodoro', N'Penne al dente en salsa pomodoro con parmesano.', 7.90, N'Pastas',
 N'/images/catalog/u1621996346565-e3dbc646d9a9-800.jpg', 1, NULL),
(24, 3, N'Pizza Margarita', N'Salsa de tomate, mozzarella y albahaca fresca.', 8.90, N'Pizzas',
 N'/images/catalog/u1574071318508-1cdbab80d002-800.jpg', 1, NULL),
(25, 3, N'Pizza Pepperoni', N'Mozzarella y doble porción de pepperoni.', 10.50, N'Pizzas',
 N'/images/catalog/u1604382354936-07c5d9983bd3-800.jpg', 1, NULL),
(26, 3, N'Pizza Cuatro Quesos', N'Mozzarella, cheddar, parmesano y queso azul.', 11.50, N'Pizzas',
 N'/images/catalog/u1513104890138-7c749659a591-800.jpg', 1, NULL),
(27, 3, N'Pizza Pollo BBQ', N'Pollo desmechado, cebolla morada y salsa BBQ.', 11.90, N'Pizzas',
 N'/images/catalog/u1565299624946-b28f40a0ae38-800.jpg', 1, NULL),
(28, 3, N'Club Sánduche Ajaví', N'Triple piso con pollo, jamón, queso, tocino y papas.', 6.50, N'Sánduches',
 N'/images/catalog/u1528735602780-2552fd46c7af-800.jpg', 1, NULL),
(29, 3, N'Ensalada de la Huerta', N'Mix de vegetales frescos, palta y garbanzos con vinagreta.', 5.90, N'Ensaladas',
 N'/images/catalog/u1512621776951-a57141f2eefd-800.jpg', 1, NULL),
(30, 3, N'Pastel de Chocolate', N'Porción de torta húmeda de chocolate con ganache.', 4.50, N'Postres',
 N'/images/catalog/u1578985545062-69928b1d9587-800.jpg', 1, NULL),
(31, 3, N'Agua Mineral', N'Agua mineral con o sin gas (500 ml).', 2.00, N'Bebidas',
 N'/images/catalog/u1548839140-29a749e1cf4d-800.jpg', 1, NULL);

-- 4. El Portón (11 productos)
INSERT INTO Products (Id, RestaurantId, Name, Description, Price, Category, ImagePath, Available, OrderDetailsId) VALUES
(32, 4, N'Hornado Ibarreño', N'Hornado de chancho al horno de leña con mote y tortillas.', 9.00, N'Comida Típica',
 N'/images/catalog/p36850029-800.jpg', 1, NULL),
(33, 4, N'Carne Colorada del Portón', N'Carne colorada con achiote, papas cocinadas, maduro y encurtido.', 9.50, N'Comida Típica',
 N'/images/catalog/u1628294895950-9805252327bc-800.jpg', 1, NULL),
(34, 4, N'Patacones con Chicharrón', N'Canastas de patacón rellenas de chicharrón crocante.', 5.50, N'Comida Típica',
 N'/images/catalog/p37121088-800.jpg', 1, NULL),
(35, 4, N'Seco de Pollo Criollo', N'Pollo criollo guisado en salsa de cilantro con arroz.', 6.90, N'Comida Típica',
 N'/images/catalog/u1541518763669-27fef04b14ea-800.jpg', 1, NULL),
(36, 4, N'Choclo Asado con Queso', N'Choclo serrano asado al carbón con queso y mapahuira.', 3.50, N'Comida Típica',
 N'/images/catalog/u1470119693884-47d3a1d1f180-800.jpg', 1, NULL),
(37, 4, N'Chifles con Ají', N'Porción de chifles crocantes con ají de la casa.', 2.50, N'Entradas',
 N'/images/catalog/u1528751014936-863e6e7a319c-800.jpg', 1, NULL),
(38, 4, N'Crema de Tomate', N'Crema de tomate riñón con crotones y albahaca.', 3.90, N'Sopas',
 N'/images/catalog/u1547592166-23ac45744acd-800.jpg', 1, NULL),
(39, 4, N'Borrego Asado con Papas', N'Borrego asado a fuego lento con puré rústico.', 9.90, N'Carnes',
 N'/images/catalog/p38330332-800.jpg', 1, NULL),
(40, 4, N'Mousse de Frutilla', N'Mousse suave de frutilla con crema batida.', 3.50, N'Postres',
 N'/images/catalog/u1488477181946-6428a0291777-800.jpg', 1, NULL),
(41, 4, N'Jugo de Naranja', N'Jugo de naranja recién exprimido.', 2.50, N'Bebidas',
 N'/images/catalog/u1600271886742-f049cd451bba-800.jpg', 1, NULL),
(42, 4, N'Canelazo Tradicional', N'Bebida caliente de canela y naranjilla, típica de la sierra.', 3.00, N'Bebidas',
 N'/images/catalog/u1576092768241-dec231879fc3-800.jpg', 1, NULL);

-- 5. La Choza (10 productos)
INSERT INTO Products (Id, RestaurantId, Name, Description, Price, Category, ImagePath, Available, OrderDetailsId) VALUES
(43, 5, N'Pollo Asado con Papas', N'Pollo dorado al horno de leña con papas y ají criollo.', 8.90, N'Pollo',
 N'/images/catalog/u1598103442097-8b74394b95c6-800.jpg', 1, NULL),
(44, 5, N'Pollo al Horno con Ensalada', N'Presas de pollo al horno con ensalada fresca y granada.', 8.50, N'Pollo',
 N'/images/catalog/u1610057099431-d73a1c9d2f2f-800.jpg', 1, NULL),
(45, 5, N'Gallina al Horno Criolla', N'Gallina de campo horneada entera, para compartir.', 9.50, N'Pollo',
 N'/images/catalog/u1594221708779-94832f4320d1-800.jpg', 1, NULL),
(46, 5, N'Carne Asada de Res', N'Corte de res asado al carbón, jugoso y ahumado.', 8.50, N'Carnes',
 N'/images/catalog/u1529692236671-f1f6cf9683ba-800.jpg', 1, NULL),
(47, 5, N'Chicharrón con Yuca', N'Chicharrón reventado con yucas doradas y ají.', 7.50, N'Comida Típica',
 N'/images/catalog/p6697492-800.jpg', 1, NULL),
(48, 5, N'Chaulafán Criollo', N'Arroz salteado con pollo, chancho, huevo y verduras.', 6.50, N'Comida Típica',
 N'/images/catalog/u1603133872878-684f208fb84b-800.jpg', 1, NULL),
(49, 5, N'Porción de Arroz', N'Porción de arroz graneado.', 2.00, N'Acompañamientos',
 N'/images/catalog/u1516684732162-798a0062be99-800.jpg', 1, NULL),
(50, 5, N'Papas Rústicas', N'Papas fritas rústicas con perejil y sal de ajo.', 2.50, N'Acompañamientos',
 N'/images/catalog/u1630431341973-02e1b662ec35-800.jpg', 1, NULL),
(51, 5, N'Hot Dog de La Choza', N'Hot dog con salsas de la casa y papitas al hilo.', 3.00, N'Snacks',
 N'/images/catalog/u1619740455993-9e612b1af08a-800.jpg', 1, NULL),
(52, 5, N'Batido de Frutilla', N'Batido espeso de frutilla con leche entera.', 2.90, N'Bebidas',
 N'/images/catalog/u1579954115545-a95591f28bfc-800.jpg', 1, NULL);

-- 6. Helados de Paila Rosalía Suárez (12 productos)
INSERT INTO Products (Id, RestaurantId, Name, Description, Price, Category, ImagePath, Available, OrderDetailsId) VALUES
(53, 6, N'Helado de Paila (2 sabores)', N'Helado artesanal batido en paila de bronce; elige dos sabores.', 2.50, N'Postres',
 N'/images/catalog/u1560008581-09826d1de69e-800.jpg', 1, NULL),
(54, 6, N'Cono Artesanal de Frutas', N'Cono con helado de paila de frutas andinas.', 2.00, N'Postres',
 N'/images/catalog/u1501443762994-82bd5dace89a-800.jpg', 1, NULL),
(55, 6, N'Helado en Cono Clásico', N'Cono clásico de un sabor: mora, coco, guanábana o taxo.', 2.00, N'Postres',
 N'/images/catalog/u1497034825429-c343d7c6a68f-800.jpg', 1, NULL),
(56, 6, N'Copa Rosalía', N'Copa especial con tres sabores, crema, galleta y salsa de chocolate.', 4.50, N'Postres',
 N'/images/catalog/u1563805042-7684c019e1cb-800.jpg', 1, NULL),
(57, 6, N'Paleta Artesanal de Mora', N'Paleta helada de mora natural.', 2.00, N'Postres',
 N'/images/catalog/u1488900128323-21503983a07e-800.jpg', 1, NULL),
(58, 6, N'Milkshake de Chocolate', N'Batido helado de chocolate con crema y galleta.', 3.90, N'Bebidas',
 N'/images/catalog/u1572490122747-3968b75cc699-800.jpg', 1, NULL),
(59, 6, N'Malteada de Vainilla y Chocolate', N'Malteada cremosa de vainilla con hilos de chocolate.', 3.50, N'Bebidas',
 N'/images/catalog/u1553787499-6f9133860278-800.jpg', 1, NULL),
(60, 6, N'Ensalada de Frutas con Crema', N'Frutas frescas picadas con crema y helado de paila.', 3.50, N'Postres',
 N'/images/catalog/u1490474418585-ba9bad8fd0ea-800.jpg', 1, NULL),
(61, 6, N'Waffles con Helado', N'Waffles calientes con helado de paila y miel.', 4.50, N'Postres',
 N'/images/catalog/u1562376552-0d160a2f238d-800.jpg', 1, NULL),
(62, 6, N'Pastel de Frambuesa', N'Porción de pastel de vainilla con crema y frambuesas.', 4.00, N'Postres',
 N'/images/catalog/u1565958011703-44f9829ba187-800.jpg', 1, NULL),
(63, 6, N'Brownie de Chocolate', N'Brownie húmedo de chocolate con nueces.', 3.90, N'Postres',
 N'/images/catalog/u1606313564200-e75d5e30476c-800.jpg', 1, NULL),
(64, 6, N'Tarta de Chocolate', N'Tarta fina de chocolate oscuro con higos.', 4.20, N'Postres',
 N'/images/catalog/u1626803775151-61d756612f97-800.jpg', 1, NULL);

-- 7. Café Río (13 productos)
INSERT INTO Products (Id, RestaurantId, Name, Description, Price, Category, ImagePath, Available, OrderDetailsId) VALUES
(65, 7, N'Café Americano', N'Café de altura de Imbabura, filtrado.', 2.00, N'Bebidas Calientes',
 N'/images/catalog/u1517578239113-b03992dcdd25-800.jpg', 1, NULL),
(66, 7, N'Capuchino Clásico', N'Espresso con leche vaporizada y espuma cremosa.', 2.50, N'Bebidas Calientes',
 N'/images/catalog/u1572442388796-11668a67e53d-800.jpg', 1, NULL),
(67, 7, N'Café Latte', N'Espresso suave con leche cremada y arte latte.', 2.80, N'Bebidas Calientes',
 N'/images/catalog/u1541167760496-1628856ab772-800.jpg', 1, NULL),
(68, 7, N'Café con Leche', N'Café pasado con leche caliente, estilo tradicional.', 2.20, N'Bebidas Calientes',
 N'/images/catalog/u1509042239860-f550ce710b93-800.jpg', 1, NULL),
(69, 7, N'Café Mocca', N'Espresso con chocolate y leche vaporizada.', 3.00, N'Bebidas Calientes',
 N'/images/catalog/u1512568400610-62da28bc8a13-800.jpg', 1, NULL),
(70, 7, N'Chocolate Caliente con Galletas', N'Chocolate espeso acompañado de galletas artesanales.', 2.80, N'Bebidas Calientes',
 N'/images/catalog/u1544787219-7f47ccb76574-800.jpg', 1, NULL),
(71, 7, N'Croissant de Mantequilla', N'Croissant hojaldrado horneado cada mañana.', 2.20, N'Panadería',
 N'/images/catalog/u1555507036-ab1f4038808a-800.jpg', 1, NULL),
(72, 7, N'Hamburguesa Crispy de Pollo', N'Pollo crocante, col morada y salsa especial en pan brioche.', 5.50, N'Hamburguesas',
 N'/images/catalog/u1481070555726-e2fe8357725c-800.jpg', 1, NULL),
(73, 7, N'Hamburguesa Clásica con Papas', N'Carne de res, queso y vegetales, con papas fritas.', 6.50, N'Hamburguesas',
 N'/images/catalog/u1541544741938-0af808871cc0-800.jpg', 1, NULL),
(74, 7, N'Hamburguesa Doble Río', N'Doble carne, doble queso y tocino.', 7.50, N'Hamburguesas',
 N'/images/catalog/u1550547660-d9450f859349-800.jpg', 1, NULL),
(75, 7, N'Tostadas Francesas', N'Tostadas francesas con banano, frutos rojos y miel.', 4.50, N'Postres',
 N'/images/catalog/u1484723091739-30a097e8f929-800.jpg', 1, NULL),
(76, 7, N'Pancakes con Miel', N'Torre de pancakes esponjosos con miel de la casa.', 4.00, N'Postres',
 N'/images/catalog/u1567620905732-2d1ec7ab7445-800.jpg', 1, NULL),
(77, 7, N'Cheesecake del Río', N'Cheesecake clásico horneado, textura sedosa.', 4.50, N'Postres',
 N'/images/catalog/u1524351199678-941a58a3df50-800.jpg', 1, NULL);

-- 8. Rincón Manabita (8 productos)
INSERT INTO Products (Id, RestaurantId, Name, Description, Price, Category, ImagePath, Available, OrderDetailsId) VALUES
(78, 8, N'Ceviche de Pescado', N'Ceviche de pescado fresco curtido en limón con cebolla paiteña.', 8.50, N'Mariscos',
 N'/images/catalog/u1562707774-553917f561df-800.jpg', 1, NULL),
(79, 8, N'Ceviche Mixto', N'Pescado, camarón y calamar crocante con camote y maíz.', 10.50, N'Mariscos',
 N'/images/catalog/u1761314037289-a9d6760eb37d-800.jpg', 1, NULL),
(80, 8, N'Ceviche de Camarón con Chifles', N'Ceviche de camarón estilo manabita acompañado de chifles.', 9.50, N'Mariscos',
 N'/images/catalog/p38330330-800.jpg', 1, NULL),
(81, 8, N'Ceviche Manabita con Maduro', N'Ceviche tradicional servido con maduro asado.', 10.00, N'Mariscos',
 N'/images/catalog/p38347627-800.jpg', 1, NULL),
(82, 8, N'Encebollado de Albacora', N'Encebollado espeso de albacora con yuca y cebolla curtida.', 5.50, N'Sopas',
 N'/images/catalog/p37387553-800.jpg', 1, NULL),
(83, 8, N'Pescado Apanado', N'Dedos de pescado apanados con salsa tártara.', 8.90, N'Mariscos',
 N'/images/catalog/u1562967914-608f82629710-800.jpg', 1, NULL),
(84, 8, N'Tallarín de Mariscos', N'Tallarín salteado con mix de mariscos frescos.', 10.90, N'Mariscos',
 N'/images/catalog/u1595295333158-4742f28fbd85-800.jpg', 1, NULL),
(85, 8, N'Jugo Natural del Día', N'Jugo de fruta fresca de temporada.', 2.50, N'Bebidas',
 N'/images/catalog/u1613478223719-2ab802602423-800.jpg', 1, NULL);

-- 9. El Fogón Costeño (9 productos)
INSERT INTO Products (Id, RestaurantId, Name, Description, Price, Category, ImagePath, Available, OrderDetailsId) VALUES
(86, 9, N'Encocado de Camarón', N'Camarones en salsa de coco esmeraldeña con arroz blanco.', 11.90, N'Mariscos',
 N'/images/catalog/u1559847844-5315695dadae-800.jpg', 1, NULL),
(87, 9, N'Arroz Marinero', N'Arroz costeño con camarón, calamar, concha y mejillones.', 12.50, N'Mariscos',
 N'/images/catalog/u1534080564583-6be75777b70a-800.jpg', 1, NULL),
(88, 9, N'Camarones al Ajillo', N'Camarones salteados en mantequilla de ajo y hierbas.', 10.90, N'Mariscos',
 N'/images/catalog/u1625943553852-781c6dd46faa-800.jpg', 1, NULL),
(89, 9, N'Ceviche del Fogón', N'Ceviche de pescado con camote y maíz tostado.', 9.00, N'Mariscos',
 N'/images/catalog/u1761314036709-f6f68a3d7cf1-800.jpg', 1, NULL),
(90, 9, N'Camarones al Vapor', N'Camarones enteros al vapor con limón (porción).', 11.50, N'Mariscos',
 N'/images/catalog/u1565680018434-b513d5e5fd47-800.jpg', 1, NULL),
(91, 9, N'Fideos Salteados con Camarón', N'Fideos al wok con camarón y vegetales.', 9.90, N'Mariscos',
 N'/images/catalog/u1563379926898-05f4575a45d8-800.jpg', 1, NULL),
(92, 9, N'Combo Frito Costeño', N'Pollo frito, maduro y papas al estilo de la costa.', 7.50, N'Pollo',
 N'/images/catalog/p37324427-800.jpg', 1, NULL),
(93, 9, N'Limonada Imperial', N'Limonada helada con hierbabuena.', 3.00, N'Bebidas',
 N'/images/catalog/u1599021456807-25db0f974333-800.jpg', 1, NULL),
(94, 9, N'Cola Personal', N'Gaseosa personal bien fría.', 2.00, N'Bebidas',
 N'/images/catalog/u1554866585-cd94860890b7-800.jpg', 1, NULL);

-- 10. La Parrilla de Ibarra (11 productos)
INSERT INTO Products (Id, RestaurantId, Name, Description, Price, Category, ImagePath, Available, OrderDetailsId) VALUES
(95, 10, N'Parrillada Ibarreña (2 personas)', N'Res, cerdo, pollo y chorizo a la parrilla con papas y ají.', 19.90, N'Carnes',
 N'/images/catalog/u1555939594-58d7cb561ad1-800.jpg', 1, NULL),
(96, 10, N'Costillas BBQ', N'Costillar bañado en salsa BBQ, cocción lenta.', 12.50, N'Carnes',
 N'/images/catalog/u1544025162-d76694265947-800.jpg', 1, NULL),
(97, 10, N'Bife de Res a la Parrilla', N'Bife jugoso al carbón, término a elección.', 11.90, N'Carnes',
 N'/images/catalog/u1558030006-450675393462-800.jpg', 1, NULL),
(98, 10, N'Alitas BBQ (8 und.)', N'Alitas glaseadas en salsa BBQ con apio.', 6.50, N'Pollo',
 N'/images/catalog/u1624726175512-19b9baf9fbd1-800.jpg', 1, NULL),
(99, 10, N'Alitas Picantes (8 und.)', N'Alitas picantes estilo búfalo con salsa de la casa.', 6.90, N'Pollo',
 N'/images/catalog/u1527477396000-e27163b481c2-800.jpg', 1, NULL),
(100, 10, N'Pollo Frito Crocante', N'Presas de pollo frito crocante estilo sureño.', 6.50, N'Pollo',
 N'/images/catalog/u1626082927389-6cd097cdc6ec-800.jpg', 1, NULL),
(101, 10, N'Pollo Broaster con Papas', N'Presa de pollo broaster con papas fritas y consomé.', 6.90, N'Pollo',
 N'/images/catalog/p16936595-800.jpg', 1, NULL),
(102, 10, N'Pollo a la Brasa Entero', N'Pollo entero a la brasa, dorado y jugoso.', 12.90, N'Pollo',
 N'/images/catalog/u1606728035253-49e8a23146de-800.jpg', 1, NULL),
(103, 10, N'Papas Fritas con Queso', N'Papas fritas gratinadas con queso y especias.', 3.50, N'Acompañamientos',
 N'/images/catalog/u1573080496219-bb080dd4f877-800.jpg', 1, NULL),
(104, 10, N'Ensalada Fresca', N'Ensalada de vegetales frescos con vinagreta.', 4.50, N'Ensaladas',
 N'/images/catalog/u1540189549336-e6e99c3679fe-800.jpg', 1, NULL),
(105, 10, N'Jugo Verde Natural', N'Jugo verde de manzana, kiwi y hierbabuena.', 2.50, N'Bebidas',
 N'/images/catalog/u1610970881699-44a5587cabec-800.jpg', 1, NULL);

SET IDENTITY_INSERT Products OFF;

COMMIT TRANSACTION;

PRINT 'Seed FlashBite Ibarra aplicado: 10 restaurantes y 105 productos.';
