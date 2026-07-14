import re

mapping = {
    "Ceviche de Camarón": "https://loremflickr.com/600/400/shrimp,ceviche/all",
    "Ceviche Mixto": "https://loremflickr.com/600/400/seafood,ceviche/all",
    "Corvina Frita": "https://loremflickr.com/600/400/fried,fish/all",
    "Camarones Apanados": "https://loremflickr.com/600/400/fried,shrimp/all",
    "Arroz con Camarón": "https://loremflickr.com/600/400/rice,shrimp/all",
    "Arroz Marinero": "https://loremflickr.com/600/400/seafood,rice/all",
    "Pizza Margarita": "https://loremflickr.com/600/400/margherita,pizza/all",
    "Pizza Pepperoni": "https://loremflickr.com/600/400/pepperoni,pizza/all",
    "Pizza Hawaiana": "https://loremflickr.com/600/400/hawaiian,pizza/all",
    "Pizza Cuatro Quesos": "https://loremflickr.com/600/400/cheese,pizza/all",
    "Calzone Clásico": "https://loremflickr.com/600/400/calzone,food/all",
    "Lasagna Boloñesa": "https://loremflickr.com/600/400/lasagna,food/all",
    "Spaghetti a la Carbonara": "https://loremflickr.com/600/400/carbonara,pasta/all",
    "Combo Cuarto de Pollo": "https://loremflickr.com/600/400/roast,chicken/all",
    "Combo Medio Pollo": "https://loremflickr.com/600/400/roast,chicken,plate/all",
    "Pollo Entero Broaster": "https://loremflickr.com/600/400/fried,chicken,whole/all",
    "Alitas BBQ (6 piezas)": "https://loremflickr.com/600/400/bbq,wings/all",
    "Alitas Picantes (6 piezas)": "https://loremflickr.com/600/400/buffalo,wings/all",
    "Nuggets de Pollo (10 piezas)": "https://loremflickr.com/600/400/chicken,nuggets/all",
    "Papas Gus Familiares": "https://loremflickr.com/600/400/french,fries/all",
    "Ensalada Coleslaw": "https://loremflickr.com/600/400/coleslaw,salad/all",
    "Fritada con Mote": "https://loremflickr.com/600/400/fried,pork/all",
    "Seco de Chivo": "https://loremflickr.com/600/400/meat,stew/all",
    "Seco de Pollo": "https://loremflickr.com/600/400/chicken,stew/all",
    "Chuleta a la Parrilla": "https://loremflickr.com/600/400/pork,chop/all",
    "Lomo Asado": "https://loremflickr.com/600/400/grilled,steak/all",
    "Parrillada Personal": "https://loremflickr.com/600/400/bbq,meat/all",
    "Empanadas de Verde (3 piezas)": "https://loremflickr.com/600/400/empanada,food/all",
    "Helado de Mora en Paila": "https://loremflickr.com/600/400/icecream,berry/all",
    "Café Americano": "https://loremflickr.com/600/400/black,coffee/all",
    "Capuchino Clásico": "https://loremflickr.com/600/400/cappuccino/all",
    "Ensalada de Frutas": "https://loremflickr.com/600/400/fruit,salad/all",
    "Banana Split": "https://loremflickr.com/600/400/banana,split/all",
    "Tostadas Mixtas": "https://loremflickr.com/600/400/cheese,toast/all",
    "Jugo Natural de Naranja": "https://loremflickr.com/600/400/orange,juice/all"
}

with open("Program.cs", "r", encoding="utf-8") as f:
    content = f.read()

for name, url in mapping.items():
    # Replace the ImagePath URL for this specific product
    # We find the line containing Name = "..." and replace its ImagePath = "..."
    # A simple regex works because the structure is consistent
    pattern = r'(Name\s*=\s*"' + re.escape(name) + r'".*?ImagePath\s*=\s*")[^"]+(")'
    content = re.sub(pattern, r'\g<1>' + url + r'\g<2>', content)

with open("Program.cs", "w", encoding="utf-8") as f:
    f.write(content)

print("Replacement complete.")
