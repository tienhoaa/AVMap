import urllib.request
import urllib.parse
import time

q = """[out:json][timeout:25];
(
  way["highway"](12.268,109.196,12.270,109.198);
);
out geom qt;"""

url = "https://overpass-api.de/api/interpreter?data=" + urllib.parse.quote(q)

t0 = time.time()
try:
    req = urllib.request.Request(url, headers={'User-Agent': 'AVMap_AutoCAD_Civil/1.0 (contact: support@opencartis.com)'})
    resp = urllib.request.urlopen(req, timeout=15)
    data = resp.read()
    print("GET SUCCESS in", round(time.time() - t0, 2), "s, len:", len(data))
except Exception as e:
    print("GET FAIL:", e)
