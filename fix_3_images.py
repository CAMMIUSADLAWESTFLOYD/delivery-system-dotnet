import json
import urllib.request
import os

from duckduckgo_search import DDGS

queries = {
    "prod_27.jpg": "homemade clear chicken soup broth in bowl realistic",
    "prod_37.jpg": "garlic bread slices cheese melted realistic food photography",
    "prod_30.jpg": "white soursop ice cream in bowl realistic photo"
}

ddgs = DDGS()

for filename, query in queries.items():
    print(f"Searching for {query}...")
    results = ddgs.images(query, max_results=3, size="Medium")
    for r in results:
        url = r["image"]
        try:
            print(f"Downloading {url}...")
            req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
            data = urllib.request.urlopen(req, timeout=10).read()
            if len(data) > 10000:
                with open(f"wwwroot/images/products/v2/{filename}", "wb") as f:
                    f.write(data)
                print(f"Success for {filename}!")
                break
        except Exception as e:
            print(f"Failed: {e}")
