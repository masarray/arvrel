from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[1]
SITE = ROOT / "docs"
PUBLIC_BASE = "https://masarray.github.io/arvrel/"
SOCIAL_IMAGE = f"{PUBLIC_BASE}assets/arvrel-main.webp"

REQUIRED_OG = {
    "og:type",
    "og:site_name",
    "og:locale",
    "og:title",
    "og:description",
    "og:url",
    "og:image",
    "og:image:width",
    "og:image:height",
    "og:image:alt",
}
REQUIRED_TWITTER = {
    "twitter:card",
    "twitter:title",
    "twitter:description",
    "twitter:image",
    "twitter:image:alt",
}


class QualityParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.lang = ""
        self.title_depth = 0
        self.title_parts: list[str] = []
        self.h1_count = 0
        self.main_count = 0
        self.canonicals: list[str] = []
        self.meta_name: dict[str, list[str]] = {}
        self.meta_property: dict[str, list[str]] = {}

    @property
    def title(self) -> str:
        return " ".join("".join(self.title_parts).split())

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        data = {key.lower(): value or "" for key, value in attrs}
        tag = tag.lower()
        if tag == "html":
            self.lang = data.get("lang", "").strip().lower()
        elif tag == "title":
            self.title_depth += 1
        elif tag == "h1":
            self.h1_count += 1
        elif tag == "main":
            self.main_count += 1
        elif tag == "meta":
            name = data.get("name", "").strip().lower()
            prop = data.get("property", "").strip().lower()
            content = data.get("content", "").strip()
            if name:
                self.meta_name.setdefault(name, []).append(content)
            if prop:
                self.meta_property.setdefault(prop, []).append(content)
        elif tag == "link" and "canonical" in data.get("rel", "").lower().split():
            self.canonicals.append(data.get("href", "").strip())

    def handle_endtag(self, tag: str) -> None:
        if tag.lower() == "title" and self.title_depth:
            self.title_depth -= 1

    def handle_data(self, data: str) -> None:
        if self.title_depth:
            self.title_parts.append(data)


def parse(path: Path) -> QualityParser:
    parser = QualityParser()
    parser.feed(path.read_text(encoding="utf-8"))
    parser.close()
    return parser


def one(values: dict[str, list[str]], key: str, label: str, failures: list[str]) -> str:
    found = values.get(key, [])
    if len(found) != 1 or not found[0]:
        failures.append(f"{label}: expected exactly one non-empty {key}, found {len(found)}.")
        return ""
    return found[0]


def validate_pages(failures: list[str]) -> set[str]:
    indexable_canonicals: set[str] = set()
    pages = sorted(SITE.rglob("*.html"))
    if not pages:
        failures.append("docs/: no HTML pages found.")
        return indexable_canonicals

    for path in pages:
        relative = path.relative_to(ROOT)
        parser = parse(path)
        robots = ",".join(parser.meta_name.get("robots", [])).lower()
        is_404 = path == SITE / "404.html"

        if parser.lang != "en":
            failures.append(f'{relative}: expected html lang="en".')
        if not parser.title:
            failures.append(f"{relative}: title is empty.")
        if parser.h1_count != 1:
            failures.append(f"{relative}: expected one h1, found {parser.h1_count}.")
        if parser.main_count != 1:
            failures.append(f"{relative}: expected one main, found {parser.main_count}.")

        if is_404:
            if "noindex" not in robots or "follow" not in robots:
                failures.append("docs/404.html: robots must contain noindex,follow.")
            if parser.canonicals:
                failures.append("docs/404.html: a non-indexable error document must not declare a canonical URL.")
            continue

        if "noindex" in robots:
            failures.append(f"{relative}: indexable public page must not use noindex.")
        if len(parser.canonicals) != 1:
            failures.append(f"{relative}: expected exactly one canonical URL, found {len(parser.canonicals)}.")
            continue

        canonical = parser.canonicals[0]
        if not canonical.startswith(PUBLIC_BASE):
            failures.append(f"{relative}: canonical must use {PUBLIC_BASE}.")
        elif canonical in indexable_canonicals:
            failures.append(f"{relative}: duplicate canonical {canonical}.")
        else:
            indexable_canonicals.add(canonical)

        for key in sorted(REQUIRED_OG):
            one(parser.meta_property, key, str(relative), failures)
        for key in sorted(REQUIRED_TWITTER):
            one(parser.meta_name, key, str(relative), failures)

        if one(parser.meta_property, "og:url", str(relative), failures) != canonical:
            failures.append(f"{relative}: og:url must match canonical.")
        if one(parser.meta_property, "og:site_name", str(relative), failures) != "ARVREL":
            failures.append(f"{relative}: og:site_name must be ARVREL.")
        if one(parser.meta_property, "og:image", str(relative), failures) != SOCIAL_IMAGE:
            failures.append(f"{relative}: og:image must use the canonical same-origin screenshot.")
        if one(parser.meta_name, "twitter:image", str(relative), failures) != SOCIAL_IMAGE:
            failures.append(f"{relative}: twitter:image must use the canonical same-origin screenshot.")
        if one(parser.meta_name, "twitter:card", str(relative), failures) != "summary_large_image":
            failures.append(f"{relative}: twitter:card must be summary_large_image.")

    return indexable_canonicals


def validate_sitemap(indexable_canonicals: set[str], failures: list[str]) -> None:
    path = SITE / "sitemap.xml"
    try:
        root = ET.parse(path).getroot()
    except (FileNotFoundError, ET.ParseError) as exc:
        failures.append(f"docs/sitemap.xml: cannot parse sitemap: {exc}.")
        return

    namespace = {"sm": "http://www.sitemaps.org/schemas/sitemap/0.9"}
    locations: list[str] = []
    for entry in root.findall("sm:url", namespace):
        loc = (entry.findtext("sm:loc", default="", namespaces=namespace) or "").strip()
        if not loc:
            failures.append("docs/sitemap.xml: every url entry needs a loc.")
            continue
        locations.append(loc)
        if entry.find("sm:changefreq", namespace) is not None:
            failures.append(f"docs/sitemap.xml: remove unsupported changefreq hint for {loc}.")
        if entry.find("sm:priority", namespace) is not None:
            failures.append(f"docs/sitemap.xml: remove unsupported priority hint for {loc}.")

    location_set = set(locations)
    if len(location_set) != len(locations):
        failures.append("docs/sitemap.xml: duplicate loc entries found.")
    if f"{PUBLIC_BASE}404.html" in location_set:
        failures.append("docs/sitemap.xml: 404.html must not be indexed.")
    if location_set != indexable_canonicals:
        missing = sorted(indexable_canonicals - location_set)
        extra = sorted(location_set - indexable_canonicals)
        if missing:
            failures.append(f"docs/sitemap.xml: missing indexable routes: {', '.join(missing)}.")
        if extra:
            failures.append(f"docs/sitemap.xml: contains non-canonical routes: {', '.join(extra)}.")


def validate_llms(failures: list[str]) -> None:
    path = SITE / "llms.txt"
    if not path.exists():
        failures.append("docs/llms.txt: missing AI/search-readable project guide.")
        return
    text = path.read_text(encoding="utf-8")
    required = (
        "# ARVREL",
        "IEC 61850 Sampled Values",
        "Virtual output only",
        f"{PUBLIC_BASE}documentation.html",
        f"{PUBLIC_BASE}research/",
        "https://github.com/masarray/arvrel",
    )
    for token in required:
        if token not in text:
            failures.append(f"docs/llms.txt: missing required token {token!r}.")


def validate_security_txt(failures: list[str]) -> None:
    path = SITE / ".well-known" / "security.txt"
    if not path.exists():
        failures.append("docs/.well-known/security.txt: missing security contact document.")
        return

    fields: dict[str, list[str]] = {}
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or ":" not in line:
            continue
        key, value = line.split(":", 1)
        fields.setdefault(key.strip(), []).append(value.strip())

    for key in ("Contact", "Expires", "Preferred-Languages", "Canonical", "Policy"):
        if not fields.get(key):
            failures.append(f"docs/.well-known/security.txt: missing {key} field.")

    canonical = fields.get("Canonical", [""])[0]
    expected = f"{PUBLIC_BASE}.well-known/security.txt"
    if canonical != expected:
        failures.append(f"docs/.well-known/security.txt: Canonical must be {expected}.")

    expires = fields.get("Expires", [""])[0]
    try:
        expiry = datetime.fromisoformat(expires.replace("Z", "+00:00"))
    except ValueError:
        failures.append("docs/.well-known/security.txt: Expires must be an ISO-8601 timestamp.")
    else:
        if expiry <= datetime.now(timezone.utc):
            failures.append("docs/.well-known/security.txt: Expires must be in the future.")

    for value in fields.get("Contact", []) + fields.get("Policy", []):
        split = urlsplit(value)
        if split.scheme not in {"https", "mailto"}:
            failures.append(f"docs/.well-known/security.txt: unsupported contact/policy URI {value!r}.")


def main() -> int:
    failures: list[str] = []
    indexable = validate_pages(failures)
    validate_sitemap(indexable, failures)
    validate_llms(failures)
    validate_security_txt(failures)

    if failures:
        print("Public quality validation failed:", file=sys.stderr)
        for failure in failures:
            print(f"- {failure}", file=sys.stderr)
        return 1

    print(
        f"Validated {len(indexable)} indexable pages, complete social metadata, custom 404 behavior, sitemap integrity, llms.txt, and security.txt."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
