import urllib.request
import urllib.parse
import time

endpoints = [
    "https://overpass-api.de/api/interpreter",
    "https://lz4.overpass-api.de/api/interpreter",
    "https://overpass.kumi.systems/api/interpreter"
]

q = """[out:json][timeout:15];
(
  way["highway"](12.268,109.196,12.270,109.198);
);
out geom qt;"""

data = ("data=" + urllib.parse.quote(q)).encode('utf-8')

for ep in endpoints:
    t0 = time.time()
    try:
        req = urllib.request.Request(
            ep, 
            data=data, 
            headers={
                'User-Agent': 'AVMap_AutoCAD_Civil/1.0 (contact: support@opencartis.com)',
                'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8'
            }
        )
        resp = urllib.request.urlopen(req, timeout=10)
        content = resp.read()
        print(f"SUCCESS {ep} in {time.time()-t0:.2f}s, len: {len(content)}")
    except Exception as e:
        print(f"FAIL {ep}: {e}")
