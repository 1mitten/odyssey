#!/usr/bin/env python3
"""Build the Odyssey content wiki from the design data.

The wiki is GENERATED, never hand-edited. Its inputs are the single source of truth:

    docs/design/icon-keys.csv      every named thing the interface refers to
    docs/design/icon-map.csv       whether the owner's art can draw it
    docs/design/proper-nouns.csv   names that are not icon keys

Outputs, all under docs/wiki/:

    index.html      one searchable page, a complete HTML document, for hosting or opening from disk
    artifact.html   the same page without the document wrapper, for publishing as an Artifact
    *.md            one Markdown page per section, for reading and correcting on GitHub
    README.md       what this is, how to correct it, how to host it

Usage:
    python3 tools/wiki/build_wiki.py            # write everything
    python3 tools/wiki/build_wiki.py --check    # exit 1 if the wiki is stale; nothing written
"""
from __future__ import annotations
import csv, hashlib, json, os, sys
from collections import Counter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DESIGN = os.path.join(ROOT, "docs", "design")
OUT = os.path.join(ROOT, "docs", "wiki")

# id, title, namespaces, blurb
SECTIONS = [
    ("commodities", "Commodities", ["ui.res"],
     "What the colony stockpiles, hauls, cooks and trades. Salvage tiers replace the ore classes a "
     "wilderness colony sim would have, because the setting is a dead city and the ground is already "
     "full of manufactured things. These names appear in the resource ledger, in every bill and in "
     "every trade, so they are the names worth arguing about first."),
    ("items", "Items and equipment", ["ui.item"],
     "Things a colonist carries, wears or fights with, as distinct from the bulk commodities above. "
     "A commodity is counted; an item is an object with a quality and a history."),
    ("buildings", "Buildings and orders", ["ui.arch.category", "ui.arch.tool"],
     "Everything on the architect menu: what can be built, and the one-off orders that can be given "
     "to things that already exist. The vertical connectors matter more here than in a flat colony "
     "sim, because a stair occupies two cells and a ladder one."),
    ("commands", "Commands", ["ui.command", "ui.menu"],
     "What the player can tell a selected thing to do. Many are conventional; the ones that are not "
     "come from the setting, such as stripping a shell rather than mining a vein. A right-click on "
     "a thing with more than one answer, a weapon today, opens a small menu at the pointer whose "
     "rows are these commands; the menu's own words are listed after them."),
    ("work", "Work and skills", ["ui.work", "ui.skill", "ui.schedule", "ui.recipe", "ui.bill"],
     "The work types a colonist can be assigned, in priority order of urgency, the skills that "
     "govern how well they do them, and the schedule blocks that say when. Salvaging is ours: it "
     "sits beside mining because taking a ruin apart without wrecking what is inside it is a "
     "different craft from digging. Work and schedule share one tab and one table, so they share "
     "a page here."),
    ("colonists", "Colonists", ["ui.pawn", "ui.prisoner", "ui.need", "ui.mood", "ui.status", "ui.stat"],
     "Who is on the map, what they need, how they feel and what they are doing right now. This is "
     "the section with the least art: no sheet contains a human figure."),
    ("health", "Health and anatomy", ["ui.health"],
     "Body parts, injuries and conditions. The anatomy sheet covers this better than any other "
     "part of the game, which is either fortunate or ominous."),
    ("events", "Events", ["ui.alert", "ui.bulletin", "ui.toast", "ui.raid"],
     "Three channels, and the difference is not cosmetic. Alerts are conditions that persist until "
     "fixed. Bulletins are things that happened and are kept until dismissed. Toasts are things "
     "that happened and are gone in six seconds, which is the right home for anything that recurs "
     "often enough that clearing it by hand would become a chore. Alerts and bulletins carry the "
     "layer they occurred on and jump the camera there, which a flat colony sim never has to think "
     "about. A raid's mix names who a band of hostiles is made of (design 55)."),
    ("research", "Research", ["ui.research.category", "ui.research.project", "ui.research.status",
                              "ui.research.hud"],
     "What the colony can learn, grouped by field, and the words the Research tab uses about it. "
     "A project's description here is the body of its detail pane: the screen reads this column, "
     "so correcting a line here corrects the game. Only Power is listed yet, and the projects are "
     "placeholders until the research mechanism exists (design 34)."),
    ("world", "World and interface", ["ui.terrain", "ui.weather", "ui.overlay", "ui.layer",
                                      "ui.tab", "ui.speed", "ui.settings", "ui.inventory.hud",
                                      "ui.biome", "ui.hills", "ui.world"],
     "Weather, the data overlays, the layer controls and the rest of the interface furniture. The "
     "six layer visibility modes are decided: see ADR 0006. The planet's biomes and terrain are "
     "proposals for veto (design 59): only Meadow can be settled until more art arrives."),
]
NS_TITLES = {
    "ui.terrain": "Terrain",
    "ui.res": "Commodities", "ui.item": "Items and equipment",
    "ui.arch.category": "Architect categories", "ui.arch.tool": "Architect tools",
    "ui.command": "Commands", "ui.menu": "The context menu", "ui.work": "Work types", "ui.skill": "Skills",
    "ui.schedule": "Schedule blocks",
    "ui.recipe": "Recipes", "ui.bill": "Bills, and the words of a station's pane",
    "ui.pawn": "Kinds of pawn", "ui.prisoner": "What the colony does with a prisoner", "ui.need": "Needs", "ui.mood": "Mood states",
    "ui.status": "Current activity", "ui.stat": "Pace and what it is made of",
    "ui.health": "Body parts and conditions",
    "ui.alert": "Alerts", "ui.bulletin": "Bulletins", "ui.toast": "Toasts",
    "ui.raid": "Raid mixes",
    "ui.weather": "Weather",
    "ui.overlay": "Overlays", "ui.layer": "Layer controls", "ui.tab": "Tabs",
    "ui.speed": "Game speed", "ui.settings": "Settings",
    "ui.research.category": "Fields", "ui.research.project": "Projects",
    "ui.research.status": "Project states", "ui.research.hud": "The Research tab's words",
    "ui.inventory.hud": "The Inventory tab's words",
    "ui.biome": "Biomes", "ui.hills": "Terrain on the planet", "ui.world": "The World screen's words",
}
SHEET_NAMES = {
    "01": "raw materials", "02": "food", "03": "camp and crafting", "04": "manufactured",
    "05": "tools and weapons", "06": "action tiles", "07": "anatomy", "08": "salvage gear",
}


def load(name):
    with open(os.path.join(DESIGN, name), newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def ns_of(key):
    p = key.split(".")
    return ".".join(p[:3]) if key.startswith("ui.arch.") else ".".join(p[:2])


def gather():
    keys = load("icon-keys.csv")
    mapping = {r["key"]: r for r in load("icon-map.csv")}
    nouns = load("proper-nouns.csv")
    names = load("colonist-names.csv")
    entries = []
    for k in keys:
        m = mapping.get(k["key"], {})
        gap = m.get("status") == "gap"
        entries.append({
            "key": k["key"], "name": k["label"], "ns": k["namespace"],
            "milestone": k["milestone"], "desc": k["tooltip_seed"],
            "art": "none" if gap or not m else "yes",
            "sheet": "" if gap or not m else m.get("sheet", ""),
            "conf": "" if gap or not m else m.get("confidence", ""),
            "need": m.get("cell_description", "") if gap else "",
        })
    by_ns = {}
    for e in entries:
        by_ns.setdefault(e["ns"], []).append(e)
    return entries, by_ns, nouns, names


def section_entries(sec, by_ns):
    out = []
    for ns in sec[2]:
        out.extend(by_ns.get(ns, []))
    return out


# ----------------------------------------------------------------- markdown

def build_markdown(entries, by_ns, nouns, names):
    files = {}
    total, gaps = len(entries), sum(1 for e in entries if e["art"] == "none")
    idx = ["# Odyssey content wiki", "",
           "Every named thing in the game, generated from the design data. **Do not edit these files "
           "by hand**: they are rebuilt by `tools/wiki/build_wiki.py` and your changes would be "
           "overwritten. Corrections go in the CSVs named at the bottom of each page.", "",
           f"{total} named entries, of which **{gaps} have no icon art** in the owner's sheets.", "",
           "| Section | Entries | Without art |", "|---|---:|---:|"]
    for sec in SECTIONS:
        es = section_entries(sec, by_ns)
        g = sum(1 for e in es if e["art"] == "none")
        idx.append(f"| [{sec[1]}]({sec[0]}.md) | {len(es)} | {g} |")
    idx += [f"| [Proper nouns](proper-nouns.md) | {len(nouns)} | — |",
            f"| [Colonist names](colonist-names.md) | {len(names)} | — |", "",
            "The single-page searchable version is `index.html`. `README.md` explains how to host it."]
    files["index.md"] = "\n".join(idx) + "\n"

    for sec in SECTIONS:
        es = section_entries(sec, by_ns)
        g = sum(1 for e in es if e["art"] == "none")
        L = [f"# {sec[1]}", "", sec[3], "",
             f"{len(es)} entries, {g} without art. Names are what the player sees; the key beside "
             f"each is the stable identifier — cite it when proposing a change.", ""]
        for ns in sec[2]:
            group = by_ns.get(ns, [])
            if not group:
                continue
            if len(sec[2]) > 1:
                L += [f"## {NS_TITLES.get(ns, ns)}", ""]
            L += ["| Name | Key | What it is | Art | Milestone |", "|---|---|---|---|---|"]
            for e in group:
                art = ("no art" if e["art"] == "none"
                       else f"sheet {e['sheet']} ({SHEET_NAMES.get(e['sheet'], '?')}), {e['conf']}")
                desc = e["desc"].replace("|", "\\|")
                if e["art"] == "none" and e["need"]:
                    desc += f" <br>**Needs:** {e['need']}"
                L.append(f"| **{e['name']}** | `{e['key']}` | {desc} | {art} | {e['milestone']} |")
            L.append("")
        L += ["---", "", "Generated from `docs/design/icon-keys.csv` and `docs/design/icon-map.csv`. "
              "To change a name or a description, edit `icon-keys.csv` and rerun "
              "`python3 tools/wiki/build_wiki.py`."]
        files[f"{sec[0]}.md"] = "\n".join(L) + "\n"

    L = ["# Proper nouns", "",
         "Names that are not interface keys: people, places, factions, creatures, the calendar. "
         "The brief requires every one of these to be ours, so this page exists to make the empty "
         "rows visible.", "",
         "| Category | Name | Status | Notes |", "|---|---|---|---|"]
    for n in nouns:
        nm = f"**{n['name']}**" if n["name"] else "—"
        L.append(f"| {n['category']} | {nm} | {n['status']}  | {n['notes']} |")
    needed = sum(1 for n in nouns if n["status"] == "needed")
    L += ["", f"**{needed} of {len(nouns)} rows have no name at all.** Those are not oversights to "
          "tidy up later: a storyteller and a faction with no name cannot be written about, and the "
          "city the whole prototype is set in is currently called nothing.", "",
          "---", "", "Generated from `docs/design/proper-nouns.csv`."]
    files["proper-nouns.md"] = "\n".join(L) + "\n"

    # ---- the colonist name pool -----------------------------------------------------------
    #
    # Its own page rather than a row of proper-nouns.csv: 244 given names would drown that
    # table, and the reason this is in the wiki at all is so the owner can read every one and
    # strike the ones they do not want.
    registers = [
        ("settled", "Settled", "Ordinary given names, the register most of a colony is drawn in."),
        ("frontier", "Frontier", "Invented and uncommon names, including the eight promoted from "
                                 "the original mockups."),
        ("yard", "Yard", "Nicknames and what people actually get called. Informal, and the "
                         "register that makes a colony sound like a place with a history."),
    ]
    genders = {"m": "m", "f": "f", "n": "any"}

    L = ["# Colonist names", "",
         "The pool every colonist's given name is drawn from. A name is chosen by arithmetic on "
         "a colonist's saved seed and their slot, so the **order of this list is load-bearing**: "
         "reordering it renames every colonist in every existing save. Add to the end; never "
         "sort.", "",
         f"{len(names)} names — " + ", ".join(
             f"{title.lower()} {sum(1 for n in names if n['register'] == reg)}"
             for reg, title, _ in registers) + ".", ""]

    for reg, title, blurb in registers:
        group = [n for n in names if n["register"] == reg]
        if not group:
            continue
        L += [f"## {title}", "", blurb, "",
              "| Name | Gender | Name | Gender | Name | Gender | Name | Gender |",
              "|---|---|---|---|---|---|---|---|"]
        for i in range(0, len(group), 4):
            row = group[i:i + 4]
            cells = []
            for n in row:
                cells += [f"**{n['name']}**", genders.get(n["gender"], n["gender"])]
            cells += ["", ""] * (4 - len(row))
            L.append("| " + " | ".join(cells) + " |")
        L.append("")

    L += ["---", "",
          "**Gender is recorded and nothing reads it.** No pawn in the simulation has a gender, "
          "and the drawn colonist is one of sixty-one Synty models in a single undifferentiated "
          "family — so a gendered name could not yet be made to agree with the figure beside "
          "it. The column is here because it cannot be re-derived cheaply later, and because the "
          "wiki is where the owner corrects it.", "",
          "Generated from `docs/design/colonist-names.csv`."]
    files["colonist-names.md"] = "\n".join(L) + "\n"
    return files


# ----------------------------------------------------------------- html

CSS = """
:root{
  color-scheme:light;
  --bg:#f4f7f9; --surface:#ffffff; --surface2:#e9eff3; --line:#cfdae2; --line2:#b4c4d0;
  --ink:#131e28; --ink2:#48596a; --ink3:#71818e; --accent:#0d7186; --accent-ink:#ffffff;
  --miss:#9d1a9d; --miss-bg:#f7e6f7; --good:#2c7a41; --warn:#8a5d14;
  --shadow:0 1px 2px rgba(16,32,48,.07),0 8px 24px rgba(16,32,48,.05);
  --display:"Chakra Petch",ui-sans-serif,system-ui,sans-serif;
  --body:"IBM Plex Sans",ui-sans-serif,system-ui,sans-serif;
  --mono:"IBM Plex Mono",ui-monospace,SFMono-Regular,Menlo,monospace;
}
@media (prefers-color-scheme:dark){:root:not([data-theme="light"]){
  color-scheme:dark;
  --bg:#0b1016; --surface:#141c25; --surface2:#1b2530; --line:#2a3846; --line2:#3b4e60;
  --ink:#dde6ee; --ink2:#93a4b5; --ink3:#66788c; --accent:#5ec8dc; --accent-ink:#06171c;
  --miss:#e874e8; --miss-bg:#2a1630; --good:#6fbf7f; --warn:#d9a23b;
  --shadow:0 1px 2px rgba(0,0,0,.4),0 10px 30px rgba(0,0,0,.28);
}}
:root[data-theme="dark"]{
  color-scheme:dark;
  --bg:#0b1016; --surface:#141c25; --surface2:#1b2530; --line:#2a3846; --line2:#3b4e60;
  --ink:#dde6ee; --ink2:#93a4b5; --ink3:#66788c; --accent:#5ec8dc; --accent-ink:#06171c;
  --miss:#e874e8; --miss-bg:#2a1630; --good:#6fbf7f; --warn:#d9a23b;
  --shadow:0 1px 2px rgba(0,0,0,.4),0 10px 30px rgba(0,0,0,.28);
}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--ink);font-family:var(--body);
     font-size:15px;line-height:1.55;-webkit-text-size-adjust:100%}
h1,h2,h3{font-family:var(--display);font-weight:600;text-wrap:balance;letter-spacing:-.01em}
a{color:var(--accent);text-underline-offset:2px}
code,.k{font-family:var(--mono)}

.top{position:sticky;top:env(safe-area-inset-top,0px);z-index:20;background:var(--surface);
     border-bottom:1px solid var(--line);box-shadow:var(--shadow)}
.top-in{max-width:1180px;margin:0 auto;padding-block:10px;padding-left:16px;padding-right:16px;
        display:flex;gap:14px;align-items:center;flex-wrap:wrap}
.mark{font-family:var(--display);font-weight:700;font-size:17px;letter-spacing:.01em;
      display:flex;gap:9px;align-items:baseline}
.mark span{font-family:var(--body);font-weight:400;font-size:11px;color:var(--ink3);
           text-transform:uppercase;letter-spacing:.1em}
.tools{margin-left:auto;display:flex;gap:8px;align-items:center;flex-wrap:wrap}
#q{font:inherit;font-family:var(--mono);font-size:13px;color:var(--ink);background:var(--bg);
   border:1px solid var(--line2);border-radius:7px;padding:7px 11px;width:min(46vw,290px)}
#q::placeholder{color:var(--ink3)}
#q:focus-visible,button:focus-visible,a:focus-visible{outline:2px solid var(--accent);outline-offset:2px}
.tog{font:inherit;font-size:12px;color:var(--ink2);background:var(--bg);border:1px solid var(--line2);
     border-radius:7px;padding:7px 11px;cursor:pointer;white-space:nowrap}
.tog:hover{border-color:var(--accent);color:var(--ink)}
.tog[aria-pressed="true"]{background:var(--accent);border-color:var(--accent);color:var(--accent-ink)}

.wrap{max-width:1180px;margin:0 auto;padding-block:26px 64px;padding-left:16px;padding-right:16px;
      display:grid;grid-template-columns:214px minmax(0,1fr);gap:34px;align-items:start}
@media (max-width:860px){.wrap{grid-template-columns:minmax(0,1fr);gap:20px}}

nav{position:sticky;top:calc(env(safe-area-inset-top,0px) + 74px)}
@media (max-width:860px){nav{position:static}}
nav h2{font-size:11px;text-transform:uppercase;letter-spacing:.11em;color:var(--ink3);
       font-family:var(--body);font-weight:600;margin:0 0 8px}
.navlist{display:flex;flex-direction:column;gap:1px;list-style:none;margin:0;padding:0}
@media (max-width:860px){.navlist{flex-direction:row;overflow-x:auto;gap:6px;padding-bottom:6px;
  scrollbar-width:thin}}
.navlist a{display:flex;justify-content:space-between;gap:10px;padding:5px 9px;border-radius:6px;
           color:var(--ink2);text-decoration:none;font-size:13.5px;white-space:nowrap}
.navlist a:hover{background:var(--surface2);color:var(--ink)}
.navlist a.on{background:var(--accent);color:var(--accent-ink)}
.navlist b{font-weight:500;font-family:var(--mono);font-size:11.5px;opacity:.78;
           font-variant-numeric:tabular-nums}

.lede{margin-bottom:26px;max-width:66ch}
.lede h1{font-size:clamp(25px,4.4vw,36px);margin:0 0 10px}
.lede p{margin:0 0 10px;color:var(--ink2)}
.stats{display:grid;grid-template-columns:repeat(auto-fit,minmax(122px,1fr));gap:1px;
       background:var(--line);border:1px solid var(--line);border-radius:9px;overflow:hidden;
       margin:20px 0 30px}
.stat{background:var(--surface);padding:12px 14px}
.stat b{display:block;font-family:var(--display);font-size:24px;font-weight:600;line-height:1.1;
        font-variant-numeric:tabular-nums}
.stat i{display:block;font-style:normal;font-size:11px;text-transform:uppercase;
        letter-spacing:.08em;color:var(--ink3);margin-top:3px}
.stat.m b{color:var(--miss)}

section{margin-bottom:42px;scroll-margin-top:calc(env(safe-area-inset-top,0px) + 84px)}
section>h2{font-size:21px;margin:0 0 3px;display:flex;gap:10px;align-items:baseline;flex-wrap:wrap}
section>h2 em{font-style:normal;font-family:var(--mono);font-size:12px;color:var(--ink3);
              font-variant-numeric:tabular-nums}
.blurb{color:var(--ink2);max-width:68ch;margin:0 0 16px;font-size:14px}
.nsh{font-family:var(--body);font-size:11px;font-weight:600;text-transform:uppercase;
     letter-spacing:.1em;color:var(--ink3);margin:22px 0 7px}

.tbl{border:1px solid var(--line);border-radius:9px;overflow:hidden;background:var(--surface)}
.row{display:grid;grid-template-columns:minmax(120px,1.05fr) minmax(0,2.15fr) 132px 46px;
     gap:14px;padding:9px 14px;border-top:1px solid var(--line);align-items:baseline}
.row:first-child{border-top:0}
.row.head{background:var(--surface2);border-top:0;font-size:10.5px;text-transform:uppercase;
          letter-spacing:.09em;color:var(--ink3);font-weight:600}
@media (max-width:700px){
  .row{grid-template-columns:minmax(0,1fr);gap:4px;padding:11px 13px}
  .row.head{display:none}
  .row .art{justify-self:start}
}
.nm{font-weight:600}
.nm .k{display:block;font-size:11px;color:var(--ink3);font-weight:400;word-break:break-all}
.dsc{color:var(--ink2);font-size:13.5px}
.dsc .need{display:block;color:var(--miss);font-size:12.5px;margin-top:3px}
.art{font-size:11.5px;color:var(--ink3);font-family:var(--mono);display:flex;
     flex-direction:column;gap:3px;align-items:flex-start;line-height:1.3}
.art .pill{display:inline-block;padding:1px 7px;border-radius:99px;border:1px solid var(--line2);
           font-size:10.5px;letter-spacing:.02em}
.art .pill.no{color:var(--miss);border-color:var(--miss);background:var(--miss-bg)}
.art .pill.low{color:var(--warn);border-color:var(--warn)}
.ms{font-family:var(--mono);font-size:11.5px;color:var(--ink3);text-align:right}
.row.gap{background:linear-gradient(90deg,var(--miss-bg),transparent 42%)}
:root:not([data-theme="light"]) .row.gap{background:linear-gradient(90deg,var(--miss-bg),transparent 52%)}

.nouns .row{grid-template-columns:minmax(110px,.9fr) minmax(90px,.7fr) minmax(0,1.9fr) 84px}
@media (max-width:700px){.nouns .row{grid-template-columns:minmax(0,1fr)}}
.nouns .st{font-family:var(--mono);font-size:11px;text-align:right}
.nouns .st.needed{color:var(--miss)}
.nouns .st.placeholder{color:var(--warn)}
.noname{color:var(--miss);font-family:var(--mono);font-size:12px}

.empty{padding:22px 14px;color:var(--ink3);font-size:13.5px}
footer{max-width:1180px;margin:0 auto;padding-block:26px 0;padding-left:16px;padding-right:16px;
       border-top:1px solid var(--line);color:var(--ink3);font-size:12.5px}
footer p{max-width:72ch}
footer code{font-size:11.5px;color:var(--ink2)}
@media (prefers-reduced-motion:reduce){*{transition:none!important;animation:none!important}}
"""

JS = """
(function(){
  var D = window.WIKI;
  var q = document.getElementById("q");
  var gapsOnly = document.getElementById("gaps");
  var theme = document.getElementById("theme");
  var rows = Array.prototype.slice.call(document.querySelectorAll(".row[data-hay]"));
  var secs = Array.prototype.slice.call(document.querySelectorAll("section[data-sec]"));

  function store(k, v){ try{ localStorage.setItem(k, v); }catch(e){} }
  function read(k){ try{ return localStorage.getItem(k); }catch(e){ return null; } }

  function apply(){
    var t = (q.value || "").trim().toLowerCase();
    var only = gapsOnly.getAttribute("aria-pressed") === "true";
    rows.forEach(function(r){
      var ok = (!t || r.dataset.hay.indexOf(t) !== -1) && (!only || r.dataset.gap === "1");
      r.hidden = !ok;
    });
    secs.forEach(function(s){
      var vis = s.querySelectorAll(".row[data-hay]:not([hidden])").length;
      var count = s.querySelector(".count");
      if (count) count.textContent = vis + (vis === 1 ? " entry" : " entries");
      s.hidden = vis === 0;
      var em = s.querySelector(".empty");
      if (em) em.hidden = vis !== 0;
      var link = document.querySelector('.navlist a[href="#' + s.id + '"] b');
      if (link) link.textContent = vis;
    });
    var none = document.getElementById("nohits");
    none.hidden = rows.some(function(r){ return !r.hidden; });
  }

  q.addEventListener("input", apply);
  gapsOnly.addEventListener("click", function(){
    var on = gapsOnly.getAttribute("aria-pressed") === "true";
    gapsOnly.setAttribute("aria-pressed", String(!on));
    store("odyssey.wiki.gaps", String(!on));
    apply();
  });
  theme.addEventListener("click", function(){
    var dark = document.documentElement.getAttribute("data-theme") === "dark"
      || (!document.documentElement.getAttribute("data-theme")
          && window.matchMedia("(prefers-color-scheme: dark)").matches);
    var next = dark ? "light" : "dark";
    document.documentElement.setAttribute("data-theme", next);
    theme.textContent = next === "dark" ? "Light" : "Dark";
    store("odyssey.wiki.theme", next);
  });
  var saved = read("odyssey.wiki.theme");
  if (saved === "dark" || saved === "light") {
    document.documentElement.setAttribute("data-theme", saved);
    theme.textContent = saved === "dark" ? "Light" : "Dark";
  }
  if (read("odyssey.wiki.gaps") === "true") gapsOnly.setAttribute("aria-pressed", "true");

  // highlight the section in view
  var obs = new IntersectionObserver(function(es){
    es.forEach(function(e){
      if (!e.isIntersecting) return;
      document.querySelectorAll(".navlist a").forEach(function(a){
        a.classList.toggle("on", a.getAttribute("href") === "#" + e.target.id);
      });
    });
  }, {rootMargin: "-90px 0px -70% 0px"});
  secs.forEach(function(s){ obs.observe(s); });

  document.addEventListener("keydown", function(e){
    if (e.key === "/" && document.activeElement !== q){ e.preventDefault(); q.focus(); q.select(); }
    if (e.key === "Escape" && document.activeElement === q){ q.value = ""; apply(); q.blur(); }
  });
  apply();
})();
"""


def esc(s):
    return (str(s).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
            .replace('"', "&quot;"))


def build_html(entries, by_ns, nouns, standalone):
    total = len(entries)
    gaps = sum(1 for e in entries if e["art"] == "none")
    low = sum(1 for e in entries if e["conf"] == "low")
    needed = sum(1 for n in nouns if n["status"] == "needed")

    P = []
    P.append("<title>Odyssey Content Wiki</title>")
    P.append('<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>')
    P.append('<link rel="stylesheet" href="https://fonts.googleapis.com/css2?'
             'family=Chakra+Petch:wght@500;600;700&family=IBM+Plex+Mono:wght@400;500&'
             'family=IBM+Plex+Sans:wght@400;500;600&display=swap">')
    P.append("<style>" + CSS + "</style>")

    # ---- header
    P.append('<header class="top"><div class="top-in">')
    P.append('<div class="mark">Odyssey<span>content wiki</span></div>')
    P.append('<div class="tools">')
    P.append('<input id="q" type="search" placeholder="Search names, keys, descriptions  ( / )" '
             'aria-label="Search the wiki" autocomplete="off">')
    P.append('<button class="tog" id="gaps" aria-pressed="false">Missing art only</button>')
    P.append('<button class="tog" id="theme">Dark</button>')
    P.append("</div></div></header>")

    P.append('<div class="wrap">')
    # ---- nav
    P.append('<nav aria-label="Sections"><h2>Sections</h2><ul class="navlist">')
    for sec in SECTIONS:
        es = section_entries(sec, by_ns)
        P.append(f'<li><a href="#{sec[0]}">{esc(sec[1])}<b>{len(es)}</b></a></li>')
    P.append(f'<li><a href="#proper-nouns">Proper nouns<b>{len(nouns)}</b></a></li>')
    P.append("</ul></nav>")

    P.append("<main>")
    P.append('<div class="lede"><h1>Every named thing in the game</h1>')
    P.append("<p>This page is the naming reference for the prototype: every commodity, building, "
             "command, work type, body part and alert the interface can refer to, with the stable "
             "key beside it. It exists to be corrected. If a name is wrong, cite its key.</p>")
    P.append("<p>It is generated from the design data, so it cannot drift from the game. Names and "
             "descriptions come from <code>docs/design/icon-keys.csv</code>; whether the owner's "
             "pixel-art sheets can draw a thing comes from <code>docs/design/icon-map.csv</code>; "
             "people, places and factions come from <code>docs/design/proper-nouns.csv</code>.</p>")
    P.append("</div>")

    P.append('<div class="stats">')
    P.append(f'<div class="stat"><b>{total}</b><i>named entries</i></div>')
    P.append(f'<div class="stat"><b>{total - gaps}</b><i>with icon art</i></div>')
    P.append(f'<div class="stat m"><b>{gaps}</b><i>no art yet</i></div>')
    P.append(f'<div class="stat"><b>{low}</b><i>low confidence</i></div>')
    P.append(f'<div class="stat m"><b>{needed}</b><i>proper nouns unnamed</i></div>')
    P.append("</div>")

    P.append('<p id="nohits" class="empty" hidden>Nothing matches that search.</p>')

    for sec in SECTIONS:
        es = section_entries(sec, by_ns)
        P.append(f'<section id="{sec[0]}" data-sec="1">')
        P.append(f'<h2>{esc(sec[1])} <em class="count">{len(es)} entries</em></h2>')
        P.append(f'<p class="blurb">{esc(sec[3])}</p>')
        for ns in sec[2]:
            group = by_ns.get(ns, [])
            if not group:
                continue
            if len(sec[2]) > 1:
                P.append(f'<h3 class="nsh">{esc(NS_TITLES.get(ns, ns))}</h3>')
            P.append('<div class="tbl">')
            P.append('<div class="row head"><div>Name</div><div>What it is</div>'
                     '<div>Art</div><div class="ms">M</div></div>')
            for e in group:
                hay = esc(" ".join([e["name"], e["key"], e["desc"], e["need"]]).lower())
                gp = "1" if e["art"] == "none" else "0"
                P.append(f'<div class="row{" gap" if gp == "1" else ""}" id="{esc(e["key"])}" '
                         f'data-hay="{hay}" data-gap="{gp}">')
                P.append(f'<div class="nm">{esc(e["name"])}<span class="k">{esc(e["key"])}</span></div>')
                d = f'<div class="dsc">{esc(e["desc"])}'
                if e["need"]:
                    d += f'<span class="need">Needs drawing: {esc(e["need"])}</span>'
                P.append(d + "</div>")
                if e["art"] == "none":
                    P.append('<div class="art"><span class="pill no">no art</span></div>')
                else:
                    cls = " low" if e["conf"] == "low" else ""
                    sheet = SHEET_NAMES.get(e["sheet"], e["sheet"])
                    P.append(f'<div class="art"><span class="pill{cls}">{esc(e["conf"])}</span> '
                             f'{esc(sheet)}</div>')
                P.append(f'<div class="ms">{esc(e["milestone"])}</div>')
                P.append("</div>")
            P.append("</div>")
        P.append('<p class="empty" hidden>No entries in this section match.</p>')
        P.append("</section>")

    # ---- proper nouns
    P.append('<section id="proper-nouns" data-sec="1" class="nouns">')
    P.append(f'<h2>Proper nouns <em class="count">{len(nouns)} entries</em></h2>')
    P.append('<p class="blurb">Names that are not interface keys: people, places, factions, '
             'creatures, the calendar. The brief requires every one of these to be ours rather '
             'than borrowed, so this table exists to make the empty rows impossible to miss. '
             f'<strong>{needed} of {len(nouns)} have no name at all</strong>, including the city '
             'the whole prototype is set in.</p>')
    P.append('<div class="tbl">')
    P.append('<div class="row head"><div>Category</div><div>Name</div><div>Notes</div>'
             '<div class="st">Status</div></div>')
    for n in nouns:
        hay = esc(" ".join([n["category"], n["name"], n["notes"], n["status"]]).lower())
        gp = "1" if n["status"] == "needed" else "0"
        P.append(f'<div class="row{" gap" if gp == "1" else ""}" id="{esc(n["id"])}" '
                 f'data-hay="{hay}" data-gap="{gp}">')
        P.append(f'<div class="nm">{esc(n["category"])}<span class="k">{esc(n["id"])}</span></div>')
        if n["name"]:
            P.append(f'<div>{esc(n["name"])}</div>')
        else:
            P.append('<div><span class="noname">not named</span></div>')
        P.append(f'<div class="dsc">{esc(n["notes"])}</div>')
        P.append(f'<div class="st {esc(n["status"])}">{esc(n["status"])}</div>')
        P.append("</div>")
    P.append("</div>")
    P.append('<p class="empty" hidden>No proper nouns match.</p>')
    P.append("</section>")

    P.append("</main></div>")

    P.append("<footer><p><strong>How to correct something.</strong> Every row has a stable key or "
             "id, shown under its name. Cite it. To change a name or description, edit "
             "<code>docs/design/icon-keys.csv</code> or <code>docs/design/proper-nouns.csv</code> "
             "and run <code>python3 tools/wiki/build_wiki.py</code>. Editing these pages directly "
             "does nothing: they are overwritten on the next build.</p>")
    P.append("<p>Art status reflects the owner's eight pixel-art sheets, mapped in "
             "<code>docs/design/icon-map.csv</code>. A confidence of <em>low</em> means the cell "
             "was identified from a small image and wants checking against a contact sheet. "
             "Milestone columns follow the roadmap in the brief.</p>")
    P.append("<p>Generated by <code>tools/wiki/build_wiki.py</code>. Design decisions live in "
             "<code>docs/design/</code> and <code>docs/adr/</code>; this wiki is content and "
             "names, not mechanics.</p></footer>")

    P.append("<script>window.WIKI=" + json.dumps({"total": total, "gaps": gaps}) + ";</script>")
    P.append("<script>" + JS + "</script>")

    body = "\n".join(P)
    if not standalone:
        # Artifact publishes wrap the file in their own document skeleton, so ship the content bare.
        return body + "\n"
    return ('<!doctype html>\n<html lang="en-GB">\n<head>\n<meta charset="utf-8">\n'
            '<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">\n'
            '<style>img{max-width:100%}[hidden]{display:none!important}</style>\n'
            + body + "\n</head>\n<body></body>\n</html>\n")


README = """# docs/wiki

The content wiki: every named thing in the game, so the names can be reviewed and corrected.

**Generated. Never edit these files by hand.** They are rebuilt from the design data by
`tools/wiki/build_wiki.py`, and a hand edit is silently overwritten on the next build.

| File | What it is |
|---|---|
| `index.html` | The whole wiki as one searchable page. A complete HTML document: open it from disk, or host it |
| `artifact.html` | The same page without the document wrapper, for publishing as a Claude Artifact |
| `index.md`, and one `.md` per section | The same content as Markdown, which GitHub renders directly. Easier to quote and correct in a pull request |

## Correcting a name

1. Find the row. Every entry carries a stable key (`ui.res.girder`) or id (`faction.raiders`).
2. Edit the source: names and descriptions in `docs/design/icon-keys.csv`, people and places in
   `docs/design/proper-nouns.csv`. Art assignments are in `docs/design/icon-map.csv`.
3. Rebuild: `python3 tools/wiki/build_wiki.py`.
4. Commit the CSV and the regenerated wiki together.

## Keeping it current

`python3 tools/wiki/build_wiki.py --check` rebuilds in memory and exits non-zero if what is on disk
differs. That makes staleness a failure rather than a habit, which is the point: a content reference
nobody trusts is worse than none. `CLAUDE.md` requires the check to pass in any commit that touches
game content.

## Hosting

`index.html` is self-contained apart from two Google Fonts stylesheets, and falls back to system
faces if those are blocked. Three ways to put it somewhere:

- **GitHub Pages**, the least work. Repository settings, Pages, deploy from a branch, folder
  `/docs`. The wiki lands at `https://<owner>.github.io/odyssey/wiki/`.
- **Any static host.** Copy `docs/wiki/` anywhere. There is no build step and no server code.
- **A Claude Artifact.** Publish `artifact.html` for a private link that needs no hosting at all.
  Already done: **<https://claude.ai/artifact/JsYRQk1vnza2wFWSNfpQwr>**. It is a snapshot, so
  republish to that same URL after a rebuild. The HUD mockup is published the same way, at
  <https://claude.ai/artifact/PcHWoujGDvd4rzcH1LAwFG>.
"""


def main(argv=None):
    argv = sys.argv[1:] if argv is None else argv
    check = "--check" in argv
    entries, by_ns, nouns, names = gather()

    files = build_markdown(entries, by_ns, nouns, names)
    files["index.html"] = build_html(entries, by_ns, nouns, standalone=True)
    files["artifact.html"] = build_html(entries, by_ns, nouns, standalone=False)
    files["README.md"] = README

    if check:
        stale = []
        for name, body in sorted(files.items()):
            path = os.path.join(OUT, name)
            if not os.path.exists(path):
                stale.append(f"{name}: missing")
            else:
                with open(path, encoding="utf-8", newline="") as f:
                    on_disk = f.read()
                # Compare the text, not the bytes. The generator emits LF; a Windows checkout
                # holds these files with CRLF, because git translates on checkout. Comparing raw
                # meant --check failed on every Windows clone with a wiki that was in fact
                # byte-identical to the committed one, and the only way to "fix" it was to commit
                # a rebuild that changed nothing but line endings. CI runs the fast tier on Linux,
                # so this failed only on the machine the owner works on.
                if on_disk.replace("\r\n", "\n") != body.replace("\r\n", "\n"):
                    stale.append(f"{name}: out of date")
        if stale:
            print("The wiki is stale. Run: python3 tools/wiki/build_wiki.py")
            for s in stale:
                print("  " + s)
            return 1
        print(f"ok: docs/wiki is current ({len(files)} files, {len(entries)} entries)")
        return 0

    os.makedirs(OUT, exist_ok=True)
    for name, body in sorted(files.items()):
        # UTF-8 and LF whatever the machine, so a wiki built on Windows and checked on Linux
        # compare equal byte for byte: the CI gate found them disagreeing on exactly this.
        with open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n") as f:
            f.write(body)
    gaps = sum(1 for e in entries if e["art"] == "none")
    print(f"ok: wrote {len(files)} files to docs/wiki")
    print(f"    {len(entries)} entries, {gaps} without art, {len(nouns)} proper nouns "
          f"({sum(1 for n in nouns if n['status']=='needed')} unnamed)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
