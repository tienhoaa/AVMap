import urllib.request
import urllib.parse
import time

endpoints = [
    "https://overpass.private.coffee/api/interpreter",
    "https://overpass.kumi.systems/api/interpreter",
    "https://maps.mail.ru/osm/tools/overpass/api/interpreter",
    "https://overpass.openstreetmap.fr/api/interpreter",
    "https://overpass-api.de/api/interpreter",
    "https://lz4.overpass-api.de/api/interpreter"
]

q = """[out:json][timeout:20];
(
  way["highway"](12.265,109.195,12.270,109.200);
);
out geom qt;"""

data = urllib.parse.urlencode({'data': q}).encode('utf-8')

for ep in endpoints:
    t0 = time.time()
    try:
        req = urllib.request.Request(ep, data=data, headers={'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)'})
        resp = urllib.request.urlopen(req, timeout=10)
        content = resp.read()
        print(f"SUCCESS {ep} in {time.time()-t0:.2f}s, len: {len(content)}")
    except Exception as e:
        print(f"FAIL {ep}: {e}")
