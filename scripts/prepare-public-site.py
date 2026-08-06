from __future__ import annotations

import html
import re
import shutil
from html.parser import HTMLParser
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SITE = ROOT / "docs"
ICON_SOURCE = ROOT / "Asset" / "icon"
ICON_TARGET = SITE / "assets" / "icon"
PUBLIC_BASE = "https://masarray.github.io/arvrel/"
SOCIAL_IMAGE = f"{PUBLIC_BASE}assets/arvrel-main.webp"
ACCESSIBILITY_STYLESHEET = "site-accessibility.css"

ICON_FILES = (
    "favicon.ico",
    "favicon.svg",
    "favicon-96x96.png",
    "apple-touch-icon.png",
    "web-app-manifest-192x192.png",
    "web-app-manifest-512x512.png",
    "site.webmanifest",
)

SOCIAL_META_RE = re.compile(
    r'\s*<meta\b(?=[^>]*\b(?:property=["\']og:[^"\']+["\']|name=["\']twitter:[^"\']+["\']))[^>]*>\s*',
    flags=re.IGNORECASE,
)
CANONICAL_TAG_RE = re.compile(
    r'(<link\b(?=[^>]*\brel=["\'][^"\']*\bcanonical\b[^"\']*["\'])[^>]*>)',
    flags=re.IGNORECASE,
)
SITE_STYLESHEET_RE = re.compile(
    r'(<link\b(?=[^>]*\brel=["\'][^"\']*\bstylesheet\b[^"\']*["\'])'
    r'(?=[^>]*\bhref=["\'](?P<href>[^"\']*assets/site\.css)["\'])[^>]*>)',
    flags=re.IGNORECASE,
)
HTML_TAG_RE = re.compile(r'<(?P<tag>div|pre)\b(?P<attrs>[^>]*)>', flags=re.IGNORECASE)
CLASS_RE = re.compile(r'\bclass=["\'](?P<classes>[^"\']+)["\']', flags=re.IGNORECASE)
TABINDEX_RE = re.compile(r'\btabindex\s*=', flags=re.IGNORECASE)


class MetadataParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.title_depth = 0
        self.title_parts: list[str] = []
        self.description = ""
        self.canonical = ""
        self.robots = ""

    @property
    def title(self) -> str:
        return " ".join("".join(self.title_parts).split())

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        data = {key.lower(): value or "" for key, value in attrs}
        tag = tag.lower()
        if tag == "title":
            self.title_depth += 1
        elif tag == "meta":
            name = data.get("name", "").strip().lower()
            if name == "description":
                self.description = data.get("content", "").strip()
            elif name == "robots":
                self.robots = data.get("content", "").strip().lower()
        elif tag == "link" and "canonical" in data.get("rel", "").lower().split():
            self.canonical = data.get("href", "").strip()

    def handle_endtag(self, tag: str) -> None:
        if tag.lower() == "title" and self.title_depth:
            self.title_depth -= 1

    def handle_data(self, data: str) -> None:
        if self.title_depth:
            self.title_parts.append(data)


def stage_icons() -> int:
    ICON_TARGET.mkdir(parents=True, exist_ok=True)
    copied = 0
    for filename in ICON_FILES:
        source = ICON_SOURCE / filename
        if not source.exists():
            raise FileNotFoundError(f"Required brand asset is missing: {source.relative_to(ROOT)}")
        target = ICON_TARGET / filename
        shutil.copy2(source, target)
        copied += 1
    return copied


def parse_metadata(path: Path) -> MetadataParser:
    parser = MetadataParser()
    parser.feed(path.read_text(encoding="utf-8"))
    parser.close()
    return parser


def social_block(path: Path, metadata: MetadataParser) -> str:
    relative = path.relative_to(SITE).as_posix()
    og_type = "article" if relative.startswith("research/") else "website"
    title = html.escape(metadata.title, quote=True)
    description = html.escape(metadata.description, quote=True)
    canonical = html.escape(metadata.canonical, quote=True)
    image_alt = html.escape(f"{metadata.title} — ARVREL engineering software", quote=True)

    return "\n".join(
        (
            f'  <meta property="og:type" content="{og_type}">',
            '  <meta property="og:site_name" content="ARVREL">',
            '  <meta property="og:locale" content="en_US">',
            f'  <meta property="og:title" content="{title}">',
            f'  <meta property="og:description" content="{description}">',
            f'  <meta property="og:url" content="{canonical}">',
            f'  <meta property="og:image" content="{SOCIAL_IMAGE}">',
            '  <meta property="og:image:width" content="2258">',
            '  <meta property="og:image:height" content="1339">',
            f'  <meta property="og:image:alt" content="{image_alt}">',
            '  <meta name="twitter:card" content="summary_large_image">',
            f'  <meta name="twitter:title" content="{title}">',
            f'  <meta name="twitter:description" content="{description}">',
            f'  <meta name="twitter:image" content="{SOCIAL_IMAGE}">',
            f'  <meta name="twitter:image:alt" content="{image_alt}">',
        )
    )


def inject_accessibility_stylesheet(text: str) -> str:
    if ACCESSIBILITY_STYLESHEET in text:
        return text

    match = SITE_STYLESHEET_RE.search(text)
    if not match:
        raise ValueError("The canonical assets/site.css link was not found.")

    href = match.group("href")
    accessibility_href = href.rsplit("/", 1)[0] + f"/{ACCESSIBILITY_STYLESHEET}"
    link = f'\n  <link rel="stylesheet" href="{accessibility_href}">'
    return text[: match.end()] + link + text[match.end():]


def make_scroll_regions_focusable(text: str) -> str:
    def replace(match: re.Match[str]) -> str:
        attrs = match.group("attrs")
        class_match = CLASS_RE.search(attrs)
        if not class_match or TABINDEX_RE.search(attrs):
            return match.group(0)

        classes = set(class_match.group("classes").split())
        if not classes.intersection({"matrix", "code"}):
            return match.group(0)

        return f'<{match.group("tag")}{attrs} tabindex="0">'

    return HTML_TAG_RE.sub(replace, text)


def normalize_social_metadata(path: Path, text: str, metadata: MetadataParser) -> str:
    if path.name == "404.html" or "noindex" in metadata.robots:
        return text
    if not metadata.title or not metadata.description or not metadata.canonical:
        relative = path.relative_to(ROOT)
        raise ValueError(f"{relative}: title, description, and canonical are required before social normalization.")

    cleaned = SOCIAL_META_RE.sub("\n", text)
    match = CANONICAL_TAG_RE.search(cleaned)
    if not match:
        raise ValueError(f"{path.relative_to(ROOT)}: canonical tag was not found.")

    block = social_block(path, metadata)
    return cleaned[: match.end()] + "\n" + block + cleaned[match.end():]


def prepare_page(path: Path) -> bool:
    original = path.read_text(encoding="utf-8")
    metadata = parse_metadata(path)
    prepared = inject_accessibility_stylesheet(original)
    prepared = make_scroll_regions_focusable(prepared)
    prepared = normalize_social_metadata(path, prepared, metadata)

    if prepared == original:
        return False
    path.write_text(prepared, encoding="utf-8", newline="\n")
    return True


def main() -> int:
    icon_count = stage_icons()
    changed = sum(prepare_page(path) for path in sorted(SITE.rglob("*.html")))
    print(f"Prepared public site: staged {icon_count} icons and normalized {changed} HTML pages.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
