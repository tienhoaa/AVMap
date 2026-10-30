import urllib.request
import urllib.parse
import json

q = """[out:json][timeout:15];
(
  way["building"](12.265,109.195,12.272,109.202);
  relation["building"](12.265,109.195,12.272,109.202);
);
out geom;"""

url = "https://overpass-api.de/api/interpreter"
req = urllib.request.Request(url, data=urllib.parse.urlencode({'data': q}).encode('utf-8'), headers={'User-Agent': 'AVMap_AutoCAD_Plugin/1.0 (contact: admin@opencartis.com)'})
try:
    resp = urllib.request.urlopen(req, timeout=15)
    data = json.loads(resp.read().decode('utf-8'))
    print("OSM buildings count:", len(data.get("elements", [])))
except Exception as e:
    print("Error:", e)
