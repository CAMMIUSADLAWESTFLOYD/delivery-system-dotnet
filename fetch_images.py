import urllib.request
import json
import urllib.parse
import time

products = [
    "Ceviche", "Ceviche", "Encebollado", "Encebollado", "Fried fish", "Fried shrimp", "Arroz con camarones", "Arroz marinero",
    "Pizza Margherita", "Pepperoni", "Hawaiian pizza", "Quattro formaggi", "Calzone", "Lasagne", "Carbonara", "Garlic bread",
    "Roast chicken", "Roast chicken", "Fried chicken", "Buffalo wing", "Buffalo wing", "Chicken nugget", "French fries", "Coleslaw",
    "Fritada", "Seco (food)", "Seco (food)", "Pork chop", "Roast beef", "Churrasco", "Chicken soup", "Empanada",
    "Ice cream", "Ice cream", "Caffè Americano", "Cappuccino", "Fruit salad", "Banana split", "Toast", "Orange juice"
]

def get_wiki_image(query, lang='en'):
    url = f'https://{lang}.wikipedia.org/w/api.php?action=query&prop=pageimages&format=json&piprop=original&titles={urllib.parse.quote(query)}'
    req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
    try:
        with urllib.request.urlopen(req) as response:
            data = json.loads(response.read())
            pages = data['query']['pages']
            for page_id in pages:
                if 'original' in pages[page_id]:
                    return pages[page_id]['original']['source']
    except Exception as e:
        pass
    return None

results = []
for p in products:
    url = get_wiki_image(p, 'en')
    if not url:
        url = get_wiki_image(p, 'es')
    results.append(url)
    time.sleep(0.1)

for i, p in enumerate(products):
    print(f"{p}: {results[i]}")
