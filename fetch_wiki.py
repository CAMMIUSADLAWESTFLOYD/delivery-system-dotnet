import urllib.request
import json
import urllib.parse
import time

products = {
    "Ceviche de Camarón": "Ceviche",
    "Ceviche Mixto": "Ceviche",
    "Corvina Frita": "Fried_fish",
    "Camarones Apanados": "Fried_shrimp",
    "Arroz con Camarón": "Arroz_con_camarones",
    "Arroz Marinero": "Seafood",
    "Pizza de Pepperoni": "Pepperoni",
    "Pizza Hawaiana": "Hawaiian_pizza",
    "Pizza Cuatro Quesos": "Quattro_formaggi",
    "Calzone Clásico": "Calzone",
    "Lasagna Boloñesa": "Lasagne",
    "Spaghetti a la Carbonara": "Carbonara",
    "Combo Cuarto de Pollo": "Roast_chicken",
    "Combo Medio Pollo": "Roast_chicken",
    "Pollo Entero Broaster": "Fried_chicken",
    "Alitas BBQ (6 piezas)": "Buffalo_wing",
    "Alitas Picantes (6 piezas)": "Buffalo_wing",
    "Nuggets de Pollo (10 piezas)": "Chicken_nugget",
    "Papas Gus Familiares": "French_fries",
    "Ensalada Coleslaw": "Coleslaw",
    "Fritada con Mote": "Fritada",
    "Seco de Chivo": "Seco_(food)",
    "Seco de Pollo": "Seco_(food)",
    "Chuleta a la Parrilla": "Pork_chop",
    "Lomo Asado": "Roast_beef",
    "Parrillada Personal": "Churrasco",
    "Consomé de Pollo": "Chicken_soup",
    "Empanadas de Verde (3 piezas)": "Empanada",
    "Helado de Mora en Paila": "Ice_cream",
    "Helado de Guanábana": "Ice_cream",
    "Café Americano": "Caffè_Americano",
    "Capuchino Clásico": "Cappuccino",
    "Ensalada de Frutas": "Fruit_salad",
    "Banana Split": "Banana_split",
    "Tostadas Mixtas": "Toast",
    "Jugo Natural de Naranja": "Orange_juice",
    "Pan de Ajo Supremo": "Garlic_bread"
}

def get_wiki_image(query):
    url = f'https://en.wikipedia.org/w/api.php?action=query&prop=pageimages&format=json&pithumbsize=800&titles={urllib.parse.quote(query)}'
    req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
    try:
        with urllib.request.urlopen(req, timeout=5) as response:
            data = json.loads(response.read())
            pages = data['query']['pages']
            for page_id in pages:
                if 'thumbnail' in pages[page_id]:
                    return pages[page_id]['thumbnail']['source']
    except:
        pass

    # Try spanish
    url = f'https://es.wikipedia.org/w/api.php?action=query&prop=pageimages&format=json&pithumbsize=800&titles={urllib.parse.quote(query)}'
    req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
    try:
        with urllib.request.urlopen(req, timeout=5) as response:
            data = json.loads(response.read())
            pages = data['query']['pages']
            for page_id in pages:
                if 'thumbnail' in pages[page_id]:
                    return pages[page_id]['thumbnail']['source']
    except:
        pass
        
    return "https://upload.wikimedia.org/wikipedia/commons/thumb/6/6d/Good_Food_Display_-_NCI_Visuals_Online.jpg/800px-Good_Food_Display_-_NCI_Visuals_Online.jpg"

print("using System.Linq;")
print("using System;")
print("public class Fixer {")
print("  public static void Run(ApplicationDbContext context) {")
for name, q in products.items():
    url = get_wiki_image(q)
    print(f'    var p_{hash(name) % 10000} = context.Products.FirstOrDefault(p => p.Name.Contains("{name.split()[0]}"));')
    print(f'    if(p_{hash(name) % 10000} != null) p_{hash(name) % 10000}.ImagePath = "{url}";')
    time.sleep(0.1)
print("    context.SaveChanges();")
print("  }")
print("}")
