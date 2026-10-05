import json, re, io, os, base64
D = r"C:\Users\user\translate\misttrain\_work\deliver"
TEXT_DIRS = [r"C:\Users\user\misttraingirlsx\BepInEx\Translation\ko\Text",
             r"E:\work\MistTrainX_Offline\BepInEx\Translation\ko\Text"]
STORY_DIRS = [r"C:\Users\user\misttraingirlsx\BepInEx\Translation\ko\Story",
              r"E:\work\MistTrainX_Offline\BepInEx\Translation\ko\Story"]

def load(f):
    p = os.path.join(D, f)
    return json.load(open(p, encoding="utf-8")) if os.path.isfile(p) else {}

names   = load("master_names_ko.json")
ep      = load("master_epithets_ko.json")
ui1     = load("master_ui_ko.json");  ui2 = load("master_ui_new_ko.json")
ui3     = load("master_ui_new2_ko.json")
ui4     = load("ui_static_missing_ko.json")
t1      = load("master_ui_templated_ko.json"); t2 = load("master_ui_new_templated_ko.json")
t3      = load("ui_static_missing_templated_ko.json")
mdata   = load("master_data_ko.json")

PH = re.compile(r'\{\d+\}')
def norm_nl(s):
    s = s.replace('\r\n', '\n').replace('\r', '\n')
    s = s.replace('/n', '\n').replace('\\n', '\n')
    return s
def esc(s):
    return norm_nl(s).replace('\n', '\\n')

static = {}
def add(k, v):
    if not v or k == v: return
    static[k] = v

for k, v in names.items(): add(k, v)
for src in (ui1, ui2, ui3, ui4):
    for k, v in src.items():
        if PH.search(k) or '{N}' in k: continue
        add(k, v)
for k, v in ep.items():
    if not v: continue
    static[k] = v
    ks = k[1:-1] if k.startswith('[') and k.endswith(']') else k
    vs = v[1:-1] if v.startswith('[') and v.endswith(']') else v
    if ks and ks != vs: static.setdefault(ks, vs)

regex_lines = []

tmpl = {**t1, **t2, **t3, **{k:v for k,v in ui3.items() if "{N}" in k}}
for k, v in tmpl.items():
    if k.count('{N}') == 0 or not v: continue
    parts = k.split('{N}')
    pat = '^' + '(\\d+)'.join(re.escape(p) for p in parts) + '$'
    rep = v
    for i in range(1, v.count('{N}') + 1): rep = rep.replace('{N}', f'${i}', 1)
    regex_lines.append(f'r:"{pat}"={rep}')

mstatic = mph = 0
for k, v in mdata.items():
    if not v: continue
    if PH.search(k):

        parts = PH.split(norm_nl(k))
        pat = '^' + '(.+?)'.join(re.escape(p) for p in parts) + '$'

        rep = PH.sub(lambda m: '$' + str(int(m.group(0)[1:-1]) + 1), norm_nl(v)).replace('\n', '\\n')
        regex_lines.append(f'r:"{pat.replace(chr(10), chr(92)+chr(110))}"={rep}')
        mph += 1
    else:
        add(k, v); mstatic += 1

import sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from crypto_mistx import derive_keys, encrypt as mx_encrypt
PASS = io.open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "secret_passphrase.txt"),
               encoding="utf-8").read().strip()
AK, MK = derive_keys(PASS)

stxt = "\n".join(f"{esc(k)}={esc(v)}" for k, v in static.items()) + "\n"
rtxt = "\n".join(regex_lines) + "\n"
story = load("main_ko.json")
story_b64 = ("\n".join(base64.b64encode(k.encode()).decode() + "\t" + base64.b64encode(v.encode()).decode()
                       for k, v in story.items() if k and v) + "\n") if story else ""
n_story = sum(1 for k, v in story.items() if k and v) if story else 0

ONLINE  = r"C:\Users\user\misttraingirlsx\BepInEx\Translation\ko"
OFFLINE = r"E:\work\MistTrainX_Offline\BepInEx\Translation\ko"

def _rm(*paths):
    for p in paths:
        if os.path.isfile(p): os.remove(p)

oti, osd = os.path.join(OFFLINE, "Text"), os.path.join(OFFLINE, "Story")
os.makedirs(oti, exist_ok=True); os.makedirs(osd, exist_ok=True)
io.open(os.path.join(oti, "_ko_static.txt"), "w", encoding="utf-8").write(stxt)
io.open(os.path.join(oti, "_ko_regex.txt"),  "w", encoding="utf-8").write(rtxt)
if story_b64: io.open(os.path.join(osd, "story_ko.b64"), "w", encoding="ascii").write(story_b64)
_rm(os.path.join(oti,"master_x_ko.enc"), os.path.join(oti,"_ko_x.txt"),
    os.path.join(oti,"_ko_static.enc"), os.path.join(oti,"_ko_regex.enc"), os.path.join(osd,"story.enc"))

nti, nsd = os.path.join(ONLINE, "Text"), os.path.join(ONLINE, "Story")
os.makedirs(nti, exist_ok=True); os.makedirs(nsd, exist_ok=True)
open(os.path.join(nti, "_ko_static.enc"), "wb").write(mx_encrypt(stxt.encode("utf-8"), AK, MK))
open(os.path.join(nti, "_ko_regex.enc"),  "wb").write(mx_encrypt(rtxt.encode("utf-8"), AK, MK))
if story_b64: open(os.path.join(nsd, "story.enc"), "wb").write(mx_encrypt(story_b64.encode("utf-8"), AK, MK))
_rm(os.path.join(nti,"_ko_static.txt"), os.path.join(nti,"_ko_regex.txt"), os.path.join(nti,"master_x_ko.enc"),
    os.path.join(nti,"_ko_x.txt"), os.path.join(nsd,"story_ko.b64"))

print(f"암호화(온라인): _ko_static.enc/_ko_regex.enc/story.enc (R18 포함 전체, AES-256-CBC+HMAC)")
print(f"평문(오프라인): _ko_static.txt/_ko_regex.txt/story_ko.b64")
print(f"static={len(static)} (names{len(names)}+epithets{len(ep)}x2+UI+master{mstatic})")
print(f"regex={len(regex_lines)} (UI-templated + master-{{0}}={mph})")
print(f"story lines={n_story}")
