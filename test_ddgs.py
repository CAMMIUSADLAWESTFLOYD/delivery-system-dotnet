import time
from ddgs import DDGS

ddgs = DDGS()
print("Starting DDGS...")
try:
    results = ddgs.images("ceviche de camaron", max_results=2)
    for r in results:
        print(r['image'])
except Exception as e:
    print(e)
