import re
mapping = {
    'Encebollado Sencillo': 'https://loremflickr.com/600/400/fish,soup/all',
    'Encebollado Mixto': 'https://loremflickr.com/600/400/fish,stew/all',
    'Consomé de Pollo': 'https://loremflickr.com/600/400/chicken,soup/all',
    'Helado de Guanábana': 'https://loremflickr.com/600/400/icecream/all',
    'Pan de Ajo Supremo': 'https://loremflickr.com/600/400/garlic,bread/all'
}
with open('Program.cs', 'r', encoding='utf-8') as f:
    content = f.read()
for name, url in mapping.items():
    pattern = r'(Name\s*=\s*\"' + name + r'\".*?ImagePath\s*=\s*\")[^\"]+(\")'
    content = re.sub(pattern, r'\g<1>' + url + r'\g<2>', content)
with open('Program.cs', 'w', encoding='utf-8') as f:
    f.write(content)
