import urllib.request
import os

urls = {
    "prod_27.jpg": "https://images.unsplash.com/photo-1547592180-85f173990554?w=600&q=80",
    "prod_37.jpg": "https://images.unsplash.com/photo-1573140247632-f8fd74997d5c?w=600&q=80",
    "prod_30.jpg": "https://images.unsplash.com/photo-1497034825429-c343d7c6a68f?w=600&q=80"
}

for filename, url in urls.items():
    print(f"Downloading {filename}...")
    req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
    data = urllib.request.urlopen(req).read()
    with open(f"wwwroot/images/products/v2/{filename}", "wb") as f:
        f.write(data)
    print("Success")
