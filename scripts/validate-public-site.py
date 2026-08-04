from __future__ import annotations

import sys
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
SITE = ROOT / "docs"


class PageParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.html_lang = ""
        self.title_depth = 0
        self.title_text: list[str] = []
        self.h1_count = 0
        self.description = ""
        self.canonical = ""
        self.viewport = ""
        self.references: list[tuple[str, str]] = []
        self.ids: set[str] = set()
        self.duplicate_ids: set[str] = set()

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        data = {key.lower(): value or "" for key, value in attrs}
        tag = tag.lower()
        if tag == "html":
            self.html_lang = data.get("lang", "")
        elif tag == "title":
            self.title_depth += 1
        elif tag == "h1":
            self.h1_count += 1
        elif tag == "meta":
            name = data.get("name", "").lower()
            if name == "description":
                self.description = data.get("content", "").strip()
            elif name == "viewport":
                self.viewport = data.get("content", "").strip()
        elif tag == "link" and "canonical" in data.get("rel", "").lower().split():
            self.canonical = data.get("href", "").strip()

        for attribute in ("href", "src"):
            value = data.get(attribute, "").strip()
            if value:
                self.references.append((attribute, value))

        node_id = data.get("id", "").strip()
        if node_id:
            if node_id in self.ids:
                self.duplicate_ids.add(node_id)
            self.ids.add(node_id)

    def handle_endtag(self, tag: str) -> None:
        if tag.lower() == "title" and self.title_depth:
            self.title_depth -= 1

    def handle_data(self, data: str) -> None:
        if self.title_depth:
            self.title_text.append(data)


def resolve_local_reference(page: Path, value: str) -> tuple[Path | None, str]:
    split = urlsplit(value)
    if split.scheme or split.netloc or value.startswith(("mailto:", "tel:", "javascript:")):
        return None, ""

    fragment = unquote(split.fragment)
    if not split.path:
        return page, fragment

    path_text = unquote(split.path)
    if path_text.startswith("/"):
        target = SITE / path_text.lstrip("/")
    else:
        target = page.parent / path_text

    if path_text.endswith("/"):
        target = target / "index.html"

    return target.resolve(), fragment


def parse_page(page: Path) -> PageParser:
    parser = PageParser()
    parser.feed(page.read_text(encoding="utf-8"))
    parser.close()
    return parser


def main() -> int:
    failures: list[str] = []
    pages = sorted(SITE.rglob("*.html"))
    if not pages:
        failures.append("No HTML pages found under docs/.")

    parsed: dict[Path, PageParser] = {}
    for page in pages:
        parser = parse_page(page)
        parsed[page.resolve()] = parser
        relative = page.relative_to(ROOT)

        if parser.html_lang.lower() != "en":
            failures.append(f"{relative}: expected <html lang=\"en\">.")
        if not "".join(parser.title_text).strip():
            failures.append(f"{relative}: missing non-empty <title>.")
        if not parser.description:
            failures.append(f"{relative}: missing meta description.")
        if not parser.viewport:
            failures.append(f"{relative}: missing viewport meta.")
        if not parser.canonical.startswith("https://masarray.github.io/arvrel/"):
            failures.append(f"{relative}: canonical URL is missing or outside the public site.")
        if parser.h1_count != 1:
            failures.append(f"{relative}: expected exactly one <h1>, found {parser.h1_count}.")
        if parser.duplicate_ids:
            failures.append(f"{relative}: duplicate id values: {', '.join(sorted(parser.duplicate_ids))}.")

    site_root = SITE.resolve()
    for page, parser in parsed.items():
        for attribute, value in parser.references:
            target, fragment = resolve_local_reference(page, value)
            if target is None:
                continue
            try:
                target.relative_to(site_root)
            except ValueError:
                failures.append(f"{page.relative_to(ROOT)}: {attribute}=\"{value}\" escapes docs/.")
                continue

            if not target.exists():
                failures.append(f"{page.relative_to(ROOT)}: broken local reference {attribute}=\"{value}\".")
                continue

            if fragment and target.suffix.lower() == ".html":
                target_parser = parsed.get(target)
                if target_parser is None:
                    target_parser = parse_page(target)
                    parsed[target] = target_parser
                if fragment not in target_parser.ids:
                    failures.append(
                        f"{page.relative_to(ROOT)}: fragment #{fragment} not found in {target.relative_to(ROOT)}."
                    )

    if failures:
        print("Public-site validation failed:", file=sys.stderr)
        for failure in failures:
            print(f"- {failure}", file=sys.stderr)
        return 1

    print(f"Validated {len(pages)} HTML pages and their local links under docs/.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
