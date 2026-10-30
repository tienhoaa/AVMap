import urllib.request
import urllib.parse
import concurrent.futures
import time

mirrors = [
    "https://overpass.kumi.systems/api/interpreter",
    "https://overpass.private.coffee/api/interpreter",
    "https://overpass.osm.ch/api/interpreter",
    "https://overpass-api.de/api/interpreter",
    "https://lz4.overpass-api.de/api/interpreter",
    "https://z.overpass-api.de/api/interpreter",
    "https://overpass.openstreetmap.ru/cgi/interpreter",
    "https://overpass.nchc.org.tw/api/interpreter",
    "https://overpass.openstreetmap.fr/api/interpreter",
    "https://overpass.osm.rambler.ru/cgi/interpreter"
]

q = "[out:json][timeout:10];node(12.268,109.196,12.269,109.197);out;"
data = ("data=" + urllib.parse.quote(q)).encode('utf-8')

def test_mirror(url):
    t0 = time.time()
    try:
        req = urllib.request.Request(
            url, 
            data=data, 
            headers={'User-Agent': 'AVMap_AutoCAD/1.0 (contact: admin@opencartis.com)'}
        )
        with urllib.request.urlopen(req, timeout=8) as r:
            body = r.read()
            return f"ALIVE: {url} in {time.time()-t0:.2f}s (len={len(body)})"
    except Exception as e:
        return f"DEAD: {url} -> {e}"

with concurrent.futures.ThreadPoolExecutor(max_workers=10) as executor:
    futures = [executor.submit(test_mirror, m) for m in mirrors]
    for f in concurrent.futures.as_completed(futures):
        print(f.result())
