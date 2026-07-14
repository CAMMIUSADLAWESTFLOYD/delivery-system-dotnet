import os
import time
import requests
import re
from duckduckgo_search import DDGS

products = {
    "Ceviche de Camarón": "ceviche de camaron ecuatoriano plato",
    "Ceviche Mixto": "ceviche mixto ecuatoriano",
    "Corvina Frita": "corvina frita ecuador plato",
    "Camarones Apanados": "camarones apanados con arroz",
    "Arroz con Camarón": "arroz con camaron ecuatoriano",
    "Arroz Marinero": "arroz marinero ecuatoriano",
    "Pizza Margarita": "pizza margarita",
    "Pizza Pepperoni": "pizza pepperoni",
    "Pizza Hawaiana": "pizza hawaiana",
    "Pizza Cuatro Quesos": "pizza cuatro quesos",
    "Calzone Clásico": "calzone clasico",
    "Lasagna Boloñesa": "lasagna boloñesa",
    "Spaghetti a la Carbonara": "spaghetti carbonara plato",
    "Combo Cuarto de Pollo": "cuarto de pollo asado con papas",
    "Combo Medio Pollo": "medio pollo asado con papas",
    "Pollo Entero Broaster": "pollo entero broaster",
    "Alitas BBQ (6 piezas)": "alitas bbq plato",
    "Alitas Picantes (6 piezas)": "alitas buffalo plato",
    "Nuggets de Pollo (10 piezas)": "nuggets de pollo",
    "Papas Gus Familiares": "papas fritas porcion grande",
    "Ensalada Coleslaw": "ensalada coleslaw",
    "Fritada con Mote": "fritada con mote ecuador",
    "Seco de Chivo": "seco de chivo ecuatoriano",
    "Seco de Pollo": "seco de pollo ecuatoriano",
    "Chuleta a la Parrilla": "chuleta a la parrilla plato",
    "Lomo Asado": "lomo asado plato",
    "Parrillada Personal": "parrillada de carne plato",
    "Consomé de Pollo": "consome de pollo casero sopa",
    "Empanadas de Verde (3 piezas)": "empanadas de verde ecuador",
    "Helado de Mora en Paila": "helado de paila mora",
    "Helado de Guanábana": "helado de guanabana",
    "Café Americano": "taza de cafe americano",
    "Capuchino Clásico": "taza de capuchino",
    "Ensalada de Frutas": "ensalada de frutas",
    "Banana Split": "banana split helado",
    "Tostadas Mixtas": "tostadas mixtas queso jamon",
    "Jugo Natural de Naranja": "vaso de jugo de naranja natural",
    "Pan de Ajo Supremo": "pan de ajo supremo",
    "Encebollado Sencillo": "encebollado ecuatoriano plato",
    "Encebollado Mixto": "encebollado mixto ecuatoriano"
}

os.makedirs("wwwroot/images/products", exist_ok=True)
ddgs = DDGS()

mapping_csharp = []

with open("FixDB.cs", "w", encoding="utf-8") as f:
    f.write("using System.Linq;\n")
    f.write("using System;\n")
    f.write("public class Fixer {\n")
    f.write("  public static void Run(ApplicationDbContext context) {\n")

    for idx, (name, query) in enumerate(products.items()):
        try:
            results = ddgs.images(query, max_results=3)
            image_url = None
            for r in results:
                if 'image' in r and (r['image'].endswith('.jpg') or r['image'].endswith('.png') or r['image'].endswith('.jpeg')):
                    image_url = r['image']
                    break
            
            if not image_url and len(results) > 0:
                image_url = results[0]['image']
                
            if image_url:
                filename = f"prod_{idx}.jpg"
                filepath = f"wwwroot/images/products/{filename}"
                
                # Download
                img_data = requests.get(image_url, timeout=5).content
                with open(filepath, "wb") as img_file:
                    img_file.write(img_data)
                
                # C# code to update the db directly
                f.write(f'    var p_{idx} = context.Products.FirstOrDefault(p => p.Name == "{name}");\n')
                f.write(f'    if(p_{idx} != null) p_{idx}.ImagePath = "/images/products/{filename}";\n')
                print(f"Downloaded {name} -> {image_url}")
            time.sleep(0.5)
        except Exception as e:
            print(f"Failed {name}: {e}")

    f.write("    context.SaveChanges();\n")
    f.write("  }\n")
    f.write("}\n")
