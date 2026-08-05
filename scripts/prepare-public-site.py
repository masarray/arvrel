from __future__ import annotations

import argparse
import html
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SITE = ROOT / "docs"
BASE = "https://masarray.github.io/arvrel/"
IMAGE = f"{BASE}assets/arvrel-main.webp"
ALT = "ARVREL engineering workspace with Sampled Values waveform, phasors, virtual relay faceplate, protection state, and evidence"
META_RE = re.compile(r'^\s*<meta\s+(?:property="og:[^"]+"|name="twitter:[^"]+")[^>]*>\s*\n?', re.I | re.M)


def attr(text: str, tag: str, key: str, value: str) -> str:
    match = re.search(rf'<{tag}\b[^>]*\b{key}="{re.escape(value)}"[^>]*>', text, re.I)
    if not match:
        return ""
    found = re.search(r'\bcontent="([^"]*)"', match.group(0), re.I)
    return html.unescape(found.group(1)) if found else ""


def canonical(text: str) -> str:
    match = re.search(r'<link\b[^>]*rel="canonical"[^>]*href="([^"]+)"[^>]*>', text, re.I)
    return html.unescape(match.group(1)) if match else ""


def title(text: str) -> str:
    match = re.search(r'<title>(.*?)</title>', text, re.I | re.S)
    return " ".join(html.unescape(match.group(1)).split()) if match else ""


def block(page_title: str, description: str, url: str) -> str:
    values = {"t": html.escape(page_title, quote=True), "d": html.escape(description, quote=True), "u": html.escape(url, quote=True)}
    return f'''  <meta property="og:type" content="website">\n  <meta property="og:site_name" content="ARVREL">\n  <meta property="og:title" content="{values['t']}">\n  <meta property="og:description" content="{values['d']}">\n  <meta property="og:url" content="{values['u']}">\n  <meta property="og:image" content="{IMAGE}">\n  <meta property="og:image:width" content="2258">\n  <meta property="og:image:height" content="1339">\n  <meta property="og:image:alt" content="{ALT}">\n  <meta name="twitter:card" content="summary_large_image">\n  <meta name="twitter:title" content="{values['t']}">\n  <meta name="twitter:description" content="{values['d']}">\n  <meta name="twitter:image" content="{IMAGE}">\n  <meta name="twitter:image:alt" content="{ALT}">\n'''


def prepare(path: Path, check: bool) -> bool:
    if path.name == "404.html":
        return False
    text = path.read_text(encoding="utf-8")
    page_title, description, url = title(text), attr(text, "meta", "name", "description"), canonical(text)
    if not page_title or not description or not url.startswith(BASE):
        raise ValueError(f"{path.relative_to(ROOT)}: title, description, and absolute canonical are required")
    updated = META_RE.sub("", text)
    updated = updated.replace("</head>", block(page_title, description, url) + "</head>", 1)
    changed = updated != text
    if changed and not check:
        path.write_text(updated, encoding="utf-8")
    return changed


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    changed = [p for p in sorted(SITE.rglob("*.html")) if prepare(p, args.check)]
    if args.check and changed:
        print("Public metadata preparation required:")
        for path in changed:
            print(f" - {path.relative_to(ROOT)}")
        return 1
    print(f"Public metadata prepared for {len(changed)} page(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
