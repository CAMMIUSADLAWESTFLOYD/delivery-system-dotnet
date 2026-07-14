import urllib.request, re, time

queries = {
    "Ceviche de Camarón": "ceviche-shrimp",
    "Ceviche Mixto": "ceviche",
    "Corvina Frita": "fried-fish",
    "Camarones Apanados": "fried-shrimp",
    "Arroz con Camarón": "shrimp-rice",
    "Arroz Marinero": "seafood-rice",
    "Pizza de Pepperoni": "pepperoni-pizza",
    "Pizza Hawaiana": "hawaiian-pizza",
    "Pizza Cuatro Quesos": "cheese-pizza",
    "Calzone Clásico": "calzone",
    "Lasagna Boloñesa": "lasagna",
    "Spaghetti a la Carbonara": "pasta-carbonara",
    "Combo Cuarto de Pollo": "roast-chicken",
    "Combo Medio Pollo": "roast-chicken",
    "Pollo Entero": "roast-chicken-whole",
    "Alitas BBQ (6 piezas)": "bbq-chicken-wings",
    "Alitas Picantes (6 piezas)": "buffalo-wings",
    "Nuggets de Pollo (10 piezas)": "chicken-nuggets",
    "Papas Fritas Grandes": "french-fries",
    "Ensalada Coleslaw": "coleslaw",
    "Fritada con Mote": "fried-pork",
    "Seco de Chivo": "meat-stew",
    "Seco de Pollo": "chicken-stew",
    "Chuleta a la Parrilla": "grilled-pork-chop",
    "Lomo Asado": "grilled-steak",
    "Parrillada Personal": "bbq-meat",
    "Empanadas de Verde (3 piezas)": "empanadas",
    "Café Americano": "black-coffee",
    "Capuchino Clásico": "cappuccino",
    "Ensalada de Frutas": "fruit-salad",
    "Banana Split": "banana-split",
    "Tostadas con Queso": "cheese-toast",
    "Jugo Natural (Naranja)": "orange-juice"
}

def get_unsplash(query):
    req = urllib.request.Request("https://unsplash.com/s/photos/" + query, headers={'User-Agent': 'Mozilla/5.0'})
    try:
        html = urllib.request.urlopen(req, timeout=5).read().decode('utf-8')
        match = re.search(r'https://images\.unsplash\.com/photo-[0-9a-zA-Z\-]+', html)
        if match:
            return match.group(0) + "?w=600&q=80"
    except Exception as e:
        pass
    return "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=600&q=80" # fallback food

results = {}
for name, q in queries.items():
    res = get_unsplash(q)
    print(f"        productos.First(p => p.Name == \"{name}\").ImagePath = \"{res}\";")
    time.sleep(0.1)

