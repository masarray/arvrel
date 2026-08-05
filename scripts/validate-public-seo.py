from __future__ import annotations

import json
import re
import sys
import xml.etree.ElementTree as ET
from datetime import date
from html.parser import HTMLParser
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
SITE = ROOT / "docs"
PUBLIC_BASE = "https://masarray.github.io/arvrel/"
REQUIRED_ROUTES = {
    f"{PUBLIC_BASE}",
    f"{PUBLIC_BASE}documentation.html",
    f"{PUBLIC_BASE}faq.html",
    f"{PUBLIC_BASE}download.html",
    f"{PUBLIC_BASE}quick-start.html",
    f"{PUBLIC_BASE}research/",
}


class SeoParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.meta_by_name: dict[str, str] = {}
        self.meta_by_property: dict[str, str] = {}
        self.canonical = ""
        self.json_ld_blocks: list[str] = []
        self._json_depth = 0
        self._json_parts: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        data = {key.lower(): value or "" for key, value in attrs}
        tag = tag.lower()
        if tag == "meta":
            name = data.get("name", "").strip().lower()
            prop = data.get("property", "").strip().lower()
            content = data.get("content", "").strip()
            if name:
                self.meta_by_name[name] = content
            if prop:
                self.meta_by_property[prop] = content
        elif tag == "link" and "canonical" in data.get("rel", "").lower().split():
            self.canonical = data.get("href", "").strip()
        elif tag == "script" and data.get("type", "").lower() == "application/ld+json":
            self._json_depth = 1
            self._json_parts = []

    def handle_data(self, data: str) -> None:
        if self._json_depth:
            self._json_parts.append(data)

    def handle_endtag(self, tag: str) -> None:
        if tag.lower() == "script" and self._json_depth:
            self.json_ld_blocks.append("".join(self._json_parts).strip())
            self._json_depth = 0
            self._json_parts = []


def fail(failures: list[str], message: str) -> None:
    failures.append(message)


def parse_html(path: Path) -> SeoParser:
    parser = SeoParser()
    parser.feed(path.read_text(encoding="utf-8"))
    parser.close()
    return parser


def flatten_entities(value: Any) -> list[dict[str, Any]]:
    entities: list[dict[str, Any]] = []
    if isinstance(value, dict):
        graph = value.get("@graph")
        if isinstance(graph, list):
            entities.extend(item for item in graph if isinstance(item, dict))
        else:
            entities.append(value)
    elif isinstance(value, list):
        entities.extend(item for item in value if isinstance(item, dict))
    return entities


def entity_has_type(entity: dict[str, Any], expected: str) -> bool:
    value = entity.get("@type")
    if isinstance(value, str):
        return value == expected
    if isinstance(value, list):
        return expected in value
    return False


def validate_public_routes(failures: list[str]) -> None:
    for relative in ("documentation.html", "faq.html"):
        path = SITE / relative
        if not path.exists():
            fail(failures, f"docs/{relative}: required public route is missing.")

    for path in sorted(SITE.rglob("*.html")):
        parser = parse_html(path)
        robots = parser.meta_by_name.get("robots", "").lower()
        if "noindex" in robots:
            fail(failures, f"{path.relative_to(ROOT)}: public HTML must not use noindex.")


def validate_homepage(failures: list[str]) -> None:
    path = SITE / "index.html"
    if not path.exists():
        fail(failures, "docs/index.html: homepage is missing.")
        return

    parser = parse_html(path)
    if parser.canonical != PUBLIC_BASE:
        fail(failures, f"docs/index.html: canonical must be {PUBLIC_BASE!r}.")

    required_properties = {
        "og:type": "website",
        "og:site_name": "ARVREL",
        "og:url": PUBLIC_BASE,
        "og:image": f"{PUBLIC_BASE}assets/arvrel-main.webp",
    }
    for key, expected in required_properties.items():
        found = parser.meta_by_property.get(key)
        if found != expected:
            fail(failures, f"docs/index.html: {key} must be {expected!r}, found {found!r}.")

    for key in ("twitter:card", "twitter:title", "twitter:description", "twitter:image"):
        if not parser.meta_by_name.get(key):
            fail(failures, f"docs/index.html: missing {key} metadata.")

    entities: list[dict[str, Any]] = []
    for index, block in enumerate(parser.json_ld_blocks, start=1):
        try:
            payload = json.loads(block)
        except json.JSONDecodeError as exc:
            fail(failures, f"docs/index.html: JSON-LD block {index} is invalid: {exc}.")
            continue
        entities.extend(flatten_entities(payload))

    website = next((item for item in entities if entity_has_type(item, "WebSite")), None)
    if website is None:
        fail(failures, "docs/index.html: WebSite structured data is required.")
    else:
        if website.get("name") != "ARVREL":
            fail(failures, "docs/index.html: WebSite name must be ARVREL.")
        if website.get("url") != PUBLIC_BASE:
            fail(failures, "docs/index.html: WebSite URL must match the public canonical base.")

    software = next((item for item in entities if entity_has_type(item, "SoftwareApplication")), None)
    if software is None:
        fail(failures, "docs/index.html: SoftwareApplication structured data is required.")
        return

    version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    expected = {
        "name": "ARVREL",
        "softwareVersion": version,
        "url": PUBLIC_BASE,
        "downloadUrl": "https://github.com/masarray/arvrel/releases",
        "softwareHelp": f"{PUBLIC_BASE}documentation.html",
        "screenshot": f"{PUBLIC_BASE}assets/arvrel-main.webp",
    }
    for key, value in expected.items():
        if software.get(key) != value:
            fail(
                failures,
                f"docs/index.html: SoftwareApplication {key} must be {value!r}, found {software.get(key)!r}.",
            )

    operating_system = str(software.get("operatingSystem", ""))
    if "Windows" not in operating_system:
        fail(failures, "docs/index.html: SoftwareApplication operatingSystem must identify Windows.")

    features = software.get("featureList")
    if not isinstance(features, list) or len(features) < 5:
        fail(failures, "docs/index.html: SoftwareApplication featureList must contain at least five items.")

    offer = software.get("offers")
    if not isinstance(offer, dict):
        fail(failures, "docs/index.html: SoftwareApplication offers must be an Offer object.")
        return
    if offer.get("@type") != "Offer":
        fail(failures, "docs/index.html: offers @type must be Offer.")
    if str(offer.get("price")) not in {"0", "0.0"}:
        fail(failures, "docs/index.html: free public offer price must be 0.")
    if offer.get("priceCurrency") != "USD":
        fail(failures, "docs/index.html: free public offer priceCurrency must be USD.")
    if offer.get("url") != "https://github.com/masarray/arvrel/releases":
        fail(failures, "docs/index.html: Offer URL must point to GitHub Releases.")


def validate_sitemap_dates_and_routes(failures: list[str]) -> None:
    path = SITE / "sitemap.xml"
    try:
        root = ET.parse(path).getroot()
    except (FileNotFoundError, ET.ParseError) as exc:
        fail(failures, f"docs/sitemap.xml: cannot validate sitemap: {exc}.")
        return

    namespace = {"sm": "http://www.sitemaps.org/schemas/sitemap/0.9"}
    locations: set[str] = set()
    for element in root.findall("sm:url", namespace):
        loc = (element.findtext("sm:loc", default="", namespaces=namespace) or "").strip()
        lastmod = (element.findtext("sm:lastmod", default="", namespaces=namespace) or "").strip()
        if loc:
            locations.add(loc)
        if not re.fullmatch(r"\d{4}-\d{2}-\d{2}", lastmod):
            fail(failures, f"docs/sitemap.xml: {loc or '<missing loc>'} has invalid lastmod {lastmod!r}.")
            continue
        try:
            parsed = date.fromisoformat(lastmod)
        except ValueError:
            fail(failures, f"docs/sitemap.xml: {loc or '<missing loc>'} has impossible lastmod {lastmod!r}.")
            continue
        if parsed > date.today():
            fail(failures, f"docs/sitemap.xml: {loc} has future lastmod {lastmod}.")

    missing = sorted(REQUIRED_ROUTES - locations)
    if missing:
        fail(failures, f"docs/sitemap.xml: missing required routes: {', '.join(missing)}.")


def validate_readme_links(failures: list[str]) -> None:
    text = (ROOT / "README.md").read_text(encoding="utf-8")
    required = [
        "https://masarray.github.io/arvrel/",
        "https://masarray.github.io/arvrel/documentation.html",
        "https://masarray.github.io/arvrel/faq.html",
        "docs/USER_GUIDE.md",
        "docs/PUBLIC_SITE.md",
    ]
    for token in required:
        if token not in text:
            fail(failures, f"README.md: missing public documentation token {token!r}.")


def main() -> int:
    failures: list[str] = []
    validate_public_routes(failures)
    validate_homepage(failures)
    validate_sitemap_dates_and_routes(failures)
    validate_readme_links(failures)

    if failures:
        print("Public SEO validation failed:")
        for item in failures:
            print(f" - {item}")
        return 1

    print("Public SEO validation passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
