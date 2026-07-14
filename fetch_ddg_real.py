import os
import time
import requests
import re
from ddgs import DDGS

products = {
    "Ceviche de Camarón": "ceviche de camaron ecuatoriano plato",
    "Ceviche Mixto": "ceviche mixto ecuatoriano",
    "Corvina Frita": "corvina frita ecuador plato",
    "Camarones Apanados": "camarones apanados ecuatorianos plato",
    "Arroz con Camarón": "arroz con camaron ecuatoriano",
    "Arroz Marinero": "arroz marinero ecuatoriano",
    "Pizza Margarita": "pizza margarita",
    "Pizza Pepperoni": "pizza pepperoni",
    "Pizza Hawaiana": "pizza hawaiana",
    "Pizza Cuatro Quesos": "pizza cuatro quesos",
    "Calzone Clásico": "calzone clasico pizza",
    "Lasagna Boloñesa": "lasagna boloñesa",
    "Spaghetti a la Carbonara": "spaghetti carbonara plato",
    "Combo Cuarto de Pollo": "cuarto de pollo asado con papas plato",
    "Combo Medio Pollo": "medio pollo asado con papas plato",
    "Pollo Entero Broaster": "pollo entero broaster plato",
    "Alitas BBQ (6 piezas)": "alitas bbq plato",
    "Alitas Picantes (6 piezas)": "alitas buffalo plato",
    "Nuggets de Pollo (10 piezas)": "nuggets de pollo plato",
    "Papas Gus Familiares": "papas fritas porcion grande",
    "Ensalada Coleslaw": "ensalada coleslaw",
    "Fritada con Mote": "fritada con mote ecuador",
    "Seco de Chivo": "seco de chivo ecuatoriano",
    "Seco de Pollo": "seco de pollo ecuatoriano",
    "Chuleta a la Parrilla": "chuleta a la parrilla asado plato",
    "Lomo Asado": "lomo asado carne plato",
    "Parrillada Personal": "parrillada de carne mixta plato",
    "Consomé de Pollo": "consome de pollo casero sopa",
    "Empanadas de Verde (3 piezas)": "empanadas de verde ecuador fritas",
    "Helado de Mora en Paila": "helado de paila de mora",
    "Helado de Guanábana": "helado de paila de guanabana",
    "Café Americano": "taza de cafe negro americano",
    "Capuchino Clásico": "taza de capuchino espumoso",
    "Ensalada de Frutas": "ensalada de frutas fresca plato",
    "Banana Split": "banana split copa de helado",
    "Tostadas Mixtas": "tostadas mixtas de queso y jamon",
    "Jugo Natural de Naranja": "vaso de jugo de naranja natural",
    "Pan de Ajo Supremo": "pan de ajo tostado con queso",
    "Encebollado Sencillo": "encebollado ecuatoriano sopa",
    "Encebollado Mixto": "encebollado mixto ecuatoriano camarones sopa"
}

os.makedirs("wwwroot/images/products/real", exist_ok=True)
ddgs = DDGS()

with open("Program.cs", "r", encoding="utf-8") as f:
    content = f.read()

for idx, (name, query) in enumerate(products.items()):
    try:
        results = ddgs.images(query, max_results=5)
        image_url = None
        for r in results:
            if r['image'].endswith('.jpg') or r['image'].endswith('.png') or r['image'].endswith('.jpeg'):
                image_url = r['image']
                break
        
        if not image_url and len(results) > 0:
            image_url = results[0]['image']
            
        if image_url:
            filename = f"prod_{idx}.jpg"
            filepath = f"wwwroot/images/products/real/{filename}"
            
            # Download
            img_data = requests.get(image_url, timeout=10, headers={'User-Agent': 'Mozilla/5.0'}).content
            with open(filepath, "wb") as img_file:
                img_file.write(img_data)
            
            # Replace in Program.cs
            # Need to find the ImagePath for this specific product
            pattern = r'(Name\s*=\s*"' + re.escape(name) + r'".*?ImagePath\s*=\s*")[^"]+(")'
            content = re.sub(pattern, r'\g<1>' + f"/images/products/real/{filename}" + r'\g<2>', content)
            
            print(f"Downloaded {name} -> {image_url}")
        time.sleep(0.5)
    except Exception as e:
        print(f"Failed {name}: {e}")

with open("Program.cs", "w", encoding="utf-8") as f:
    f.write(content)

print("All replacements done!")
