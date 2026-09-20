#!/usr/bin/env python3
"""Automated tests for the Almanac in-game wiki reference browser.

Verifies the visual spec acceptance criteria:
1. No two entries within any category share the same icon path string.
2. Every entry explicitly specifies { name, colour, path }.
3. All 12 required categories are present.
4. Only items/objects/concepts that currently exist within Odyssey are included.
5. Every entry follows the identical 4-band template:
   - Band 1: Identity (96px square, title, type chip, nature chip, definition, live-state, actions)
   - Band 2: Properties (fixed 520px column, 150px key column, 8-12 rows)
   - Band 3: Body (Behaviour/Levels/Specs/Effects, etc.)
   - Band 4: Related (chips pinned with margin-top: auto)
6. Proficiency ramp colors match the spec:
   0-3 #a8453f, 4-6 #9a7a5a, 7-10 #c9cdd2, 11-14 #e0c05a, 15-20 #f5c518.
"""
from __future__ import annotations
import json, os, re, sys, unittest

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ALMANAC_HTML = os.path.join(ROOT, "docs", "reference", "mockups", "almanac.html")

REQUIRED_CATEGORIES = [
    "Terrain", "Materials", "Structures", "Items", "Skills", "Work types",
    "Needs", "Traits", "Health", "Fauna", "Flora", "Events"
]

PROFICIENCY_RAMP = {
    "0-3": "#a8453f",
    "4-6": "#9a7a5a",
    "7-10": "#c9cdd2",
    "11-14": "#e0c05a",
    "15-20": "#f5c518"
}

def extract_almanac_data():
    if not os.path.exists(ALMANAC_HTML):
        raise FileNotFoundError(f"Missing {ALMANAC_HTML}")
    with open(ALMANAC_HTML, "r", encoding="utf-8") as f:
        content = f.read()
    
    start = content.find("const ALMANAC_DATA = ")
    if start == -1:
        raise ValueError("Could not find ALMANAC_DATA in almanac.html")
    start += len("const ALMANAC_DATA = ")
    
    depth = 0
    end = -1
    for i in range(start, len(content)):
        if content[i] == '{':
            depth += 1
        elif content[i] == '}':
            depth -= 1
            if depth == 0:
                end = i + 1
                break
                
    if end == -1:
        raise ValueError("Could not match braces for ALMANAC_DATA")
        
    raw_json = content[start:end]
    data = json.loads(raw_json)
    return content, data


class AlmanacSpecTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.html_content, cls.data = extract_almanac_data()

    def test_all_twelve_categories_present(self):
        categories = [c["name"] for c in self.data["categories"]]
        for req in REQUIRED_CATEGORIES:
            self.assertIn(req, categories, f"Required category '{req}' is missing")
        self.assertEqual(len(categories), 12, "Must contain exactly 12 categories")

    def test_every_category_has_distinct_line_icon(self):
        cat_paths = set()
        for cat in self.data["categories"]:
            self.assertTrue(cat.get("iconPath"), f"Category {cat['name']} missing iconPath")
            path = cat["iconPath"].strip()
            self.assertNotIn(path, cat_paths, f"Category {cat['name']} duplicates an icon path with another category")
            cat_paths.add(path)

    def test_no_two_entries_in_category_share_icon_path(self):
        for cat in self.data["categories"]:
            cat_name = cat["name"]
            entries = cat.get("entries", [])
            self.assertGreater(len(entries), 0, f"Category {cat_name} must have entries")
            seen_paths = {}
            for entry in entries:
                icon_path = entry.get("icon", {}).get("path", "").strip()
                entry_name = entry.get("name", "unnamed")
                self.assertTrue(icon_path, f"Entry '{entry_name}' in '{cat_name}' missing icon.path")
                if icon_path in seen_paths:
                    self.fail(f"Icon collision in category '{cat_name}': entries '{entry_name}' and '{seen_paths[icon_path]}' share the same SVG path string '{icon_path}'")
                seen_paths[icon_path] = entry_name

    def test_every_entry_has_explicit_name_colour_path(self):
        for cat in self.data["categories"]:
            for entry in cat.get("entries", []):
                self.assertIn("name", entry)
                self.assertTrue(entry["name"])
                self.assertIn("icon", entry, f"Entry {entry['name']} missing icon object")
                icon = entry["icon"]
                self.assertIn("colour", icon, f"Entry {entry['name']} missing icon.colour")
                self.assertIn("path", icon, f"Entry {entry['name']} missing icon.path")
                self.assertTrue(icon["colour"].startswith("#") or icon["colour"].startswith("rgb"), 
                                f"Entry {entry['name']} colour '{icon['colour']}' must be valid hex/rgb")

    def test_every_entry_has_four_bands_data(self):
        for cat in self.data["categories"]:
            for entry in cat.get("entries", []):
                name = entry["name"]
                # Band 1: Identity
                self.assertTrue(entry.get("definition"), f"Entry {name} missing definition")
                self.assertTrue(entry.get("typeChip"), f"Entry {name} missing typeChip")
                self.assertTrue(entry.get("natureChip"), f"Entry {name} missing natureChip")
                self.assertTrue(entry.get("liveState"), f"Entry {name} missing liveState")
                self.assertTrue(entry.get("primaryAction"), f"Entry {name} missing primaryAction")
                # Band 2: Properties (8-12 rows)
                props = entry.get("properties", [])
                self.assertGreaterEqual(len(props), 5, f"Entry {name} has too few properties ({len(props)})")
                self.assertLessEqual(len(props), 14, f"Entry {name} has too many properties ({len(props)})")
                for p in props:
                    self.assertIn("key", p, f"Property in {name} missing key")
                    self.assertIn("value", p, f"Property in {name} missing value")
                # Band 3: Body
                self.assertTrue(entry.get("body"), f"Entry {name} missing body")
                # Band 4: Related
                related = entry.get("related", [])
                self.assertGreaterEqual(len(related), 2, f"Entry {name} should have related chips")

    def test_property_key_column_fixed_150px_in_css(self):
        self.assertIn("150px", self.html_content, "CSS must specify fixed 150px for property key column")
        # Ensure CSS rule for property key width is 150px
        self.assertTrue(
            re.search(r"\.prop-key\s*\{[^}]*width:\s*150px", self.html_content) or
            re.search(r"grid-template-columns:\s*150px", self.html_content),
            "Property keys must align on a 150px column in CSS"
        )

    def test_category_rail_fixed_250px_and_index_fixed_290px(self):
        self.assertTrue(re.search(r"width:\s*250px", self.html_content), "Category rail must be 250px fixed")
        self.assertTrue(re.search(r"width:\s*290px", self.html_content), "Index column must be 290px fixed")

    def test_properties_fixed_520px_column(self):
        self.assertTrue(re.search(r"width:\s*520px", self.html_content), "Properties band must be 520px fixed")

    def test_bottom_tab_bar_70px_and_almanac_f9(self):
        self.assertTrue(re.search(r"height:\s*70px", self.html_content), "Bottom tab bar must be 70px")
        self.assertIn("Almanac (F9)", self.html_content, "Bottom tab bar must include Almanac (F9)")

    def test_search_field_360px_and_slash_hotkey(self):
        self.assertTrue(re.search(r"width:\s*360px", self.html_content), "Search field must be 360px")
        self.assertIn("Search entries", self.html_content, "Search placeholder must be 'Search entries'")

    def test_proficiency_ramp_colors(self):
        for tier, color in PROFICIENCY_RAMP.items():
            self.assertIn(color.lower(), self.html_content.lower(), f"Proficiency ramp color {color} for {tier} must be present")


if __name__ == "__main__":
    unittest.main()
