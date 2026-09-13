# -*- coding: utf-8 -*-
import re
import json
from pathlib import Path

p = Path(r"D:\Project\MyWeb\Issue\.cursor\docs\02-第二階段－粉彩視覺風格\共用資料\工作追蹤－UIUX確認稿.html")
text = p.read_text(encoding="utf-8")
m = re.search(
    r'<script type="application/json" id="snapshot">\s*(\{.*?\})\s*</script>',
    text,
    re.S,
)
if not m:
    raise SystemExit("snapshot not found")
obj = json.loads(m.group(1))
print("member0", obj["members"][0])
print("major0", obj["majors"][0])
print("issue0", obj["issues"][0]["title"][:30])
co = next((x for x in obj["clientTree"] if x.get("id") == 1), None)
print("company1", co)
ok = obj["majors"][0]["name"] == "預約"
print("encoding_ok", ok)
