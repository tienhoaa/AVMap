import urllib.request
import urllib.parse
import time

q = """[out:json][timeout:30];
(
  way["highway"](12.265,109.195,12.270,109.200);
  way["building"](12.265,109.195,12.270,109.200);
  way["waterway"](12.265,109.195,12.270,109.200);
);
out geom qt;"""

url = "https://overpass.openstreetmap.fr/api/interpreter"
data = ("data=" + urllib.parse.quote(q)).encode('utf-8')

t0 = time.time()
req = urllib.request.Request(
    url, 
    data=data, 
    headers={
        'User-Agent': 'AVMap_AutoCAD_Civil/1.0 (contact: admin@opencartis.com)',
        'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8'
    }
)
try:
    with urllib.request.urlopen(req, timeout=20) as r:
        body = r.read()
        print(f"SUCCESS with buildings in {time.time()-t0:.2f}s, response size: {len(body)} bytes")
except Exception as e:
    print(f"FAIL: {e}")
