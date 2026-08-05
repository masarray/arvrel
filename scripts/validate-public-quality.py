from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from html.parser import HTMLParser
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SITE = ROOT / "docs"
BASE = "https://masarray.github.io/arvrel/"
IMAGE = f"{BASE}assets/arvrel-main.webp"


class Page(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.title = ""; self.description = ""; self.robots = ""; self.canonical = ""
        self.names: dict[str, list[str]] = {}; self.props: dict[str, list[str]] = {}
        self.h1 = 0; self.main = 0; self._title = False

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        data = {k.lower(): v or "" for k, v in attrs}; tag = tag.lower()
        if tag == "title": self._title = True
        elif tag == "h1": self.h1 += 1
        elif tag == "main": self.main += 1
        elif tag == "meta":
            name, prop, content = data.get("name", "").lower(), data.get("property", "").lower(), data.get("content", "").strip()
            if name:
                self.names.setdefault(name, []).append(content)
                if name == "description": self.description = content
                if name == "robots": self.robots = content.lower()
            if prop: self.props.setdefault(prop, []).append(content)
        elif tag == "link" and "canonical" in data.get("rel", "").lower().split(): self.canonical = data.get("href", "").strip()

    def handle_endtag(self, tag: str) -> None:
        if tag.lower() == "title": self._title = False

    def handle_data(self, data: str) -> None:
        if self._title: self.title += data


def parse(path: Path) -> Page:
    page = Page(); page.feed(path.read_text(encoding="utf-8")); page.close(); page.title = " ".join(page.title.split()); return page


def one(mapping: dict[str, list[str]], key: str, expected: str, label: str, failures: list[str]) -> None:
    if mapping.get(key, []) != [expected]: failures.append(f"{label}: {key} must occur once with {expected!r}.")


def pages(failures: list[str]) -> set[str]:
    urls: set[str] = set()
    constants = {"og:type": "website", "og:site_name": "ARVREL", "og:image": IMAGE, "og:image:width": "2258", "og:image:height": "1339"}
    for path in sorted(SITE.rglob("*.html")):
        label, page = path.relative_to(ROOT).as_posix(), parse(path)
        if page.h1 != 1: failures.append(f"{label}: expected one h1, found {page.h1}.")
        if page.main != 1: failures.append(f"{label}: expected one main, found {page.main}.")
        if path.name == "404.html":
            if "noindex" not in page.robots or "follow" not in page.robots: failures.append(f"{label}: 404 must use noindex,follow.")
            continue
        if "noindex" in page.robots: failures.append(f"{label}: indexable page uses noindex.")
        if not page.title or not page.description or not page.canonical.startswith(BASE): failures.append(f"{label}: title, description, and public canonical are required.")
        elif page.canonical in urls: failures.append(f"{label}: duplicate canonical {page.canonical}.")
        else: urls.add(page.canonical)
        for key, expected in constants.items(): one(page.props, key, expected, label, failures)
        for key, expected in {"og:title": page.title, "og:description": page.description, "og:url": page.canonical}.items(): one(page.props, key, expected, label, failures)
        for key, expected in {"twitter:card": "summary_large_image", "twitter:title": page.title, "twitter:description": page.description, "twitter:image": IMAGE}.items(): one(page.names, key, expected, label, failures)
        if len(page.props.get("og:image:alt", [])) != 1 or not page.props["og:image:alt"][0]: failures.append(f"{label}: og:image:alt is required once.")
        if len(page.names.get("twitter:image:alt", [])) != 1 or not page.names["twitter:image:alt"][0]: failures.append(f"{label}: twitter:image:alt is required once.")
    return urls


def sitemap(urls: set[str], failures: list[str]) -> None:
    try: root = ET.parse(SITE / "sitemap.xml").getroot()
    except (FileNotFoundError, ET.ParseError) as exc: failures.append(f"docs/sitemap.xml: {exc}."); return
    ns = {"s": "http://www.sitemaps.org/schemas/sitemap/0.9"}; found: set[str] = set()
    for item in root.findall("s:url", ns):
        loc = (item.findtext("s:loc", default="", namespaces=ns) or "").strip()
        if loc: found.add(loc)
        if item.find("s:changefreq", ns) is not None or item.find("s:priority", ns) is not None: failures.append(f"docs/sitemap.xml: {loc} contains ignored metadata.")
    if f"{BASE}404.html" in found: failures.append("docs/sitemap.xml: 404 must not be listed.")
    if urls - found: failures.append(f"docs/sitemap.xml: missing routes: {', '.join(sorted(urls-found))}.")
    if found - urls: failures.append(f"docs/sitemap.xml: extra routes: {', '.join(sorted(found-urls))}.")


def main() -> int:
    failures: list[str] = []
    if not (SITE / "404.html").exists(): failures.append("docs/404.html: custom error page is required.")
    urls = pages(failures); sitemap(urls, failures)
    if failures:
        print("Public quality validation failed:")
        for item in failures: print(f" - {item}")
        return 1
    print(f"Public quality validation passed for {len(urls)} indexable routes plus 404.")
    return 0


if __name__ == "__main__": raise SystemExit(main())
