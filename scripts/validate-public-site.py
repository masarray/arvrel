from __future__ import annotations

import json
import re
import sys
import xml.etree.ElementTree as ET
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
SITE = ROOT / "docs"
PUBLIC_BASE = "https://masarray.github.io/arvrel/"
SITEMAP_URL = f"{PUBLIC_BASE}sitemap.xml"


class PageParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.html_lang = ""
        self.title_depth = 0
        self.title_text: list[str] = []
        self.h1_count = 0
        self.main_count = 0
        self.missing_alt_count = 0
        self.stylesheet_count = 0
        self.description = ""
        self.canonical = ""
        self.viewport = ""
        self.references: list[tuple[str, str]] = []
        self.images: list[dict[str, str | bool]] = []
        self.ids: set[str] = set()
        self.duplicate_ids: set[str] = set()

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        attribute_names = {key.lower() for key, _ in attrs}
        data = {key.lower(): value or "" for key, value in attrs}
        tag = tag.lower()

        if tag == "html":
            self.html_lang = data.get("lang", "")
        elif tag == "title":
            self.title_depth += 1
        elif tag == "h1":
            self.h1_count += 1
        elif tag == "main":
            self.main_count += 1
        elif tag == "img":
            if "alt" not in attribute_names:
                self.missing_alt_count += 1
            self.images.append(
                {
                    "src": data.get("src", "").strip(),
                    "alt": data.get("alt", ""),
                    "alt_present": "alt" in attribute_names,
                    "width": data.get("width", "").strip(),
                    "height": data.get("height", "").strip(),
                }
            )
        elif tag == "meta":
            name = data.get("name", "").lower()
            if name == "description":
                self.description = data.get("content", "").strip()
            elif name == "viewport":
                self.viewport = data.get("content", "").strip()
        elif tag == "link":
            rel_tokens = data.get("rel", "").lower().split()
            if "canonical" in rel_tokens:
                self.canonical = data.get("href", "").strip()
            if "stylesheet" in rel_tokens:
                self.stylesheet_count += 1

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


def expected_canonical(page: Path) -> str:
    relative = page.relative_to(SITE).as_posix()
    if relative == "index.html":
        public_path = ""
    elif relative.endswith("/index.html"):
        public_path = relative[: -len("index.html")]
    else:
        public_path = relative
    return f"{PUBLIC_BASE}{public_path}"


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


def parse_positive_int(value: str) -> int | None:
    if not value.isdigit():
        return None
    parsed = int(value)
    return parsed if parsed > 0 else None


def webp_dimensions(path: Path) -> tuple[int, int]:
    data = path.read_bytes()
    if len(data) < 30 or data[:4] != b"RIFF" or data[8:12] != b"WEBP":
        raise ValueError("invalid WebP RIFF header")

    offset = 12
    while offset + 8 <= len(data):
        fourcc = data[offset : offset + 4]
        size = int.from_bytes(data[offset + 4 : offset + 8], "little")
        chunk = data[offset + 8 : offset + 8 + size]

        if fourcc == b"VP8 " and len(chunk) >= 10 and chunk[3:6] == b"\x9d\x01\x2a":
            width = int.from_bytes(chunk[6:8], "little") & 0x3FFF
            height = int.from_bytes(chunk[8:10], "little") & 0x3FFF
            return width, height
        if fourcc == b"VP8L" and len(chunk) >= 5 and chunk[0] == 0x2F:
            bits = int.from_bytes(chunk[1:5], "little")
            width = (bits & 0x3FFF) + 1
            height = ((bits >> 14) & 0x3FFF) + 1
            return width, height
        if fourcc == b"VP8X" and len(chunk) >= 10:
            width = int.from_bytes(chunk[4:7], "little") + 1
            height = int.from_bytes(chunk[7:10], "little") + 1
            return width, height

        offset += 8 + size + (size % 2)

    raise ValueError("WebP image dimensions were not found")


def png_dimensions(path: Path) -> tuple[int, int]:
    data = path.read_bytes()
    if len(data) < 24 or data[:8] != b"\x89PNG\r\n\x1a\n" or data[12:16] != b"IHDR":
        raise ValueError("invalid PNG header")
    return int.from_bytes(data[16:20], "big"), int.from_bytes(data[20:24], "big")


def raster_dimensions(path: Path) -> tuple[int, int] | None:
    suffix = path.suffix.lower()
    if suffix == ".webp":
        return webp_dimensions(path)
    if suffix == ".png":
        return png_dimensions(path)
    return None


def validate_sitemap(canonicals: set[str], failures: list[str]) -> None:
    sitemap = SITE / "sitemap.xml"
    if not sitemap.exists():
        failures.append("docs/sitemap.xml: missing sitemap.")
        return

    try:
        root = ET.parse(sitemap).getroot()
    except ET.ParseError as exc:
        failures.append(f"docs/sitemap.xml: invalid XML: {exc}.")
        return

    namespace = {"sm": "http://www.sitemaps.org/schemas/sitemap/0.9"}
    locations = {
        (element.text or "").strip()
        for element in root.findall("sm:url/sm:loc", namespace)
        if (element.text or "").strip()
    }
    missing = sorted(canonicals - locations)
    extra = sorted(locations - canonicals)
    if missing:
        failures.append(f"docs/sitemap.xml: missing canonical routes: {', '.join(missing)}.")
    if extra:
        failures.append(f"docs/sitemap.xml: contains non-canonical routes: {', '.join(extra)}.")


def validate_robots(failures: list[str]) -> None:
    robots = SITE / "robots.txt"
    if not robots.exists():
        failures.append("docs/robots.txt: missing robots file.")
        return
    text = robots.read_text(encoding="utf-8")
    if f"Sitemap: {SITEMAP_URL}" not in text:
        failures.append(f"docs/robots.txt: must advertise {SITEMAP_URL}.")


def pinned_engine_commit(failures: list[str]) -> str:
    workflow = ROOT / ".github" / "workflows" / "release.yml"
    text = workflow.read_text(encoding="utf-8")
    match = re.search(
        r"repository:\s*masarray/ARIEC61850\s+ref:\s*([0-9a-f]{40})",
        text,
        flags=re.IGNORECASE,
    )
    if not match:
        failures.append(".github/workflows/release.yml: pinned ARIEC61850 commit was not found.")
        return ""
    return match.group(1).lower()


def validate_trust_manifest(failures: list[str]) -> None:
    path = SITE / "trust-manifest.json"
    if not path.exists():
        failures.append("docs/trust-manifest.json: missing public trust manifest.")
        return

    try:
        manifest = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        failures.append(f"docs/trust-manifest.json: invalid JSON: {exc}.")
        return

    version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    tag = f"v{version}"
    engine_commit = pinned_engine_commit(failures)
    expected_assets = [
        f"ARVREL-Setup-v{version}-win-x64.exe",
        f"ARVREL-v{version}-win-x64-portable.zip",
        "SHA256SUMS.txt",
    ]

    checks = {
        "version": version,
        "releaseTag": tag,
        "outputAuthority": "virtual-only",
    }
    for key, expected in checks.items():
        if manifest.get(key) != expected:
            failures.append(
                f'docs/trust-manifest.json: "{key}" must be {expected!r}, found {manifest.get(key)!r}.'
            )

    engine = manifest.get("engine") or {}
    if engine.get("repository") != "masarray/ARIEC61850":
        failures.append("docs/trust-manifest.json: engine repository must be masarray/ARIEC61850.")
    if engine_commit and str(engine.get("commit", "")).lower() != engine_commit:
        failures.append("docs/trust-manifest.json: engine commit does not match release.yml.")
    if manifest.get("requiredReleaseAssets") != expected_assets:
        failures.append("docs/trust-manifest.json: required release assets do not match VERSION.")


def validate_citation_metadata(failures: list[str]) -> None:
    path = ROOT / "CITATION.cff"
    if not path.exists():
        failures.append("CITATION.cff: missing citation metadata.")
        return

    text = path.read_text(encoding="utf-8")
    version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    match = re.search(r'^version:\s*["\']?([^"\'\s]+)', text, flags=re.MULTILINE)
    if not match or match.group(1) != version:
        found = match.group(1) if match else "<missing>"
        failures.append(f"CITATION.cff: version must be {version!r}, found {found!r}.")
    required_tokens = [
        'repository-code: "https://github.com/masarray/arvrel"',
        'license: "GPL-3.0-or-later"',
        'title: "ARVREL — Virtual Protection Relay Laboratory"',
    ]
    for token in required_tokens:
        if token not in text:
            failures.append(f"CITATION.cff: missing required metadata token {token!r}.")


def validate_research_source_claims(failures: list[str]) -> None:
    feeder_path = ROOT / "src" / "Arvrel.Protection" / "FeederProtection.cs"
    controller_path = ROOT / "src" / "Arvrel.ProcessBus" / "SmvProcessBusController.cs"
    continuity_path = ROOT / "src" / "Arvrel.ProcessBus" / "SmvIngressContinuityGate.cs"
    for path in (feeder_path, controller_path, continuity_path):
        if not path.exists():
            failures.append(f"{path.relative_to(ROOT)}: required P3 source anchor is missing.")
            return

    feeder = feeder_path.read_text(encoding="utf-8")
    controller = controller_path.read_text(encoding="utf-8")
    continuity = continuity_path.read_text(encoding="utf-8")

    feeder_tokens = [
        "public static class FundamentalPhasorEstimator",
        "mean += samples[start + index]",
        "real += value * Math.Cos(angle)",
        "imaginary += value * Math.Sin(angle)",
        "var scale = Math.Sqrt(2) / samplesPerCycle",
        "phasors.PositiveSequenceCurrent",
        "phasors.PositiveSequenceVoltage",
        "phasors.ResidualCurrent",
        "phasors.ResidualVoltage",
        "var torque = Math.Cos(ToRadians(operatingAngle - characteristicAngleDegrees))",
    ]
    for token in feeder_tokens:
        if token not in feeder:
            failures.append(f"src/Arvrel.Protection/FeederProtection.cs: P3 claim anchor missing {token!r}.")

    continuity_tokens = [
        "SmvIngressDecision.DropDuplicate",
        "SmvIngressDecision.DropOutOfOrder",
        "SmvIngressDecision.RestartWindow",
        "forward < wrap / 2",
    ]
    for token in continuity_tokens:
        if token not in continuity:
            failures.append(f"src/Arvrel.ProcessBus/SmvIngressContinuityGate.cs: P3 claim anchor missing {token!r}.")

    controller_tokens = [
        "discarded before measurement",
        "holding coherent display during trust recovery",
        "must not clear protection timers or a latched trip",
    ]
    for token in controller_tokens:
        if token not in controller:
            failures.append(f"src/Arvrel.ProcessBus/SmvProcessBusController.cs: P3 claim anchor missing {token!r}.")

    research_pages = [
        SITE / "research" / "index.html",
        SITE / "research" / "signal-processing.html",
        SITE / "research" / "smv-continuity.html",
        SITE / "research" / "directional-protection.html",
        SITE / "research" / "validation.html",
        SITE / "research" / "related-work.html",
        SITE / "laboratory-exercises.html",
        SITE / "roadmap.html",
    ]
    for path in research_pages:
        if not path.exists():
            failures.append(f"{path.relative_to(ROOT)}: required P3 public route is missing.")

    signal_page = SITE / "research" / "signal-processing.html"
    if signal_page.exists():
        text = signal_page.read_text(encoding="utf-8").lower()
        for phrase in ("single-bin", "not compute or claim a full harmonic-spectrum fft", "complete one-cycle"):
            if phrase not in text:
                failures.append(f"docs/research/signal-processing.html: missing precise claim phrase {phrase!r}.")


def validate_research_scenarios(failures: list[str]) -> None:
    path = SITE / "data" / "research-scenarios.json"
    if not path.exists():
        failures.append("docs/data/research-scenarios.json: missing deterministic scenario catalog.")
        return

    try:
        catalog = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        failures.append(f"docs/data/research-scenarios.json: invalid JSON: {exc}.")
        return

    version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    if catalog.get("schemaVersion") != 1:
        failures.append("docs/data/research-scenarios.json: schemaVersion must be 1.")
    if catalog.get("product") != "ARVREL":
        failures.append("docs/data/research-scenarios.json: product must be ARVREL.")
    if catalog.get("version") != version:
        failures.append("docs/data/research-scenarios.json: version does not match VERSION.")
    if catalog.get("evidenceClass") != "deterministic-software-tests":
        failures.append("docs/data/research-scenarios.json: unexpected evidenceClass.")
    if not str(catalog.get("boundary", "")).strip():
        failures.append("docs/data/research-scenarios.json: boundary statement is required.")

    scenarios = catalog.get("scenarios")
    if not isinstance(scenarios, list) or not scenarios:
        failures.append("docs/data/research-scenarios.json: scenarios must be a non-empty list.")
        return

    allowed_outcomes = {"measure", "operate", "restrain", "block-trip"}
    seen: set[str] = set()
    validation_page = SITE / "research" / "validation.html"
    validation_text = validation_page.read_text(encoding="utf-8") if validation_page.exists() else ""

    for index, scenario in enumerate(scenarios, start=1):
        prefix = f"docs/data/research-scenarios.json: scenario {index}"
        if not isinstance(scenario, dict):
            failures.append(f"{prefix} must be an object.")
            continue

        scenario_id = str(scenario.get("id", "")).strip()
        if not re.fullmatch(r"[A-Z0-9]+(?:-[A-Z0-9]+)+", scenario_id):
            failures.append(f"{prefix} has invalid id {scenario_id!r}.")
        elif scenario_id in seen:
            failures.append(f"{prefix} duplicates id {scenario_id!r}.")
        else:
            seen.add(scenario_id)

        for key in ("title", "category", "input", "expected", "sourceFile", "testFile", "testMethod"):
            if not str(scenario.get(key, "")).strip():
                failures.append(f"{prefix} missing non-empty {key!r}.")

        outcome = scenario.get("outcome")
        if outcome not in allowed_outcomes:
            failures.append(f"{prefix} outcome must be one of {sorted(allowed_outcomes)}, found {outcome!r}.")

        source_relative = Path(str(scenario.get("sourceFile", "")))
        test_relative = Path(str(scenario.get("testFile", "")))
        source_path = (ROOT / source_relative).resolve()
        test_path = (ROOT / test_relative).resolve()
        try:
            source_path.relative_to(ROOT.resolve())
            test_path.relative_to(ROOT.resolve())
        except ValueError:
            failures.append(f"{prefix} source or test path escapes the repository.")
            continue

        if not source_relative.as_posix().startswith("src/"):
            failures.append(f"{prefix} sourceFile must be under src/.")
        if not test_relative.as_posix().startswith("tests/"):
            failures.append(f"{prefix} testFile must be under tests/.")
        if not source_path.exists():
            failures.append(f"{prefix} sourceFile does not exist: {source_relative}.")
        if not test_path.exists():
            failures.append(f"{prefix} testFile does not exist: {test_relative}.")
        elif str(scenario.get("testMethod", "")) not in test_path.read_text(encoding="utf-8"):
            failures.append(
                f"{prefix} testMethod {scenario.get('testMethod')!r} was not found in {test_relative}."
            )

        if scenario_id and scenario_id not in validation_text:
            failures.append(f"{prefix} id {scenario_id!r} is not published in docs/research/validation.html.")


def main() -> int:
    failures: list[str] = []
    pages = sorted(SITE.rglob("*.html"))
    if not pages:
        failures.append("No HTML pages found under docs/.")

    parsed: dict[Path, PageParser] = {}
    canonical_owners: dict[str, Path] = {}

    for page in pages:
        parser = parse_page(page)
        resolved_page = page.resolve()
        parsed[resolved_page] = parser
        relative = page.relative_to(ROOT)

        if parser.html_lang.lower() != "en":
            failures.append(f'{relative}: expected <html lang="en">.')
        if not "".join(parser.title_text).strip():
            failures.append(f"{relative}: missing non-empty <title>.")
        if not parser.description:
            failures.append(f"{relative}: missing meta description.")
        if not parser.viewport:
            failures.append(f"{relative}: missing viewport meta.")

        canonical_expected = expected_canonical(page)
        if parser.canonical != canonical_expected:
            failures.append(
                f'{relative}: canonical URL must be "{canonical_expected}", found "{parser.canonical}".'
            )
        elif parser.canonical in canonical_owners:
            owner = canonical_owners[parser.canonical].relative_to(ROOT)
            failures.append(f"{relative}: canonical URL duplicates {owner}.")
        else:
            canonical_owners[parser.canonical] = page

        if parser.h1_count != 1:
            failures.append(f"{relative}: expected exactly one <h1>, found {parser.h1_count}.")
        if parser.main_count != 1:
            failures.append(f"{relative}: expected exactly one <main>, found {parser.main_count}.")
        if parser.stylesheet_count < 1:
            failures.append(f"{relative}: missing stylesheet link.")
        if parser.missing_alt_count:
            failures.append(f"{relative}: {parser.missing_alt_count} image(s) missing an alt attribute.")
        if parser.duplicate_ids:
            failures.append(f"{relative}: duplicate id values: {', '.join(sorted(parser.duplicate_ids))}.")

        for image in parser.images:
            if not image["alt_present"] or not str(image["alt"]).strip():
                continue
            src = str(image["src"])
            target, _ = resolve_local_reference(page, src)
            if target is None:
                continue
            declared_width = parse_positive_int(str(image["width"]))
            declared_height = parse_positive_int(str(image["height"]))
            if declared_width is None or declared_height is None:
                failures.append(
                    f'{relative}: content image src="{src}" must declare positive native width and height.'
                )
                continue
            if not target.exists():
                continue
            try:
                actual = raster_dimensions(target)
            except ValueError as exc:
                failures.append(f"{relative}: cannot read dimensions for {src}: {exc}.")
                continue
            if actual is not None and actual != (declared_width, declared_height):
                failures.append(
                    f"{relative}: {src} declares {declared_width}x{declared_height} but file is {actual[0]}x{actual[1]}."
                )

    site_root = SITE.resolve()
    for page, parser in parsed.items():
        for attribute, value in parser.references:
            target, fragment = resolve_local_reference(page, value)
            if target is None:
                continue
            try:
                target.relative_to(site_root)
            except ValueError:
                failures.append(f'{page.relative_to(ROOT)}: {attribute}="{value}" escapes docs/.')
                continue

            if not target.exists():
                failures.append(f'{page.relative_to(ROOT)}: broken local reference {attribute}="{value}".')
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

    validate_sitemap(set(canonical_owners), failures)
    validate_robots(failures)
    validate_trust_manifest(failures)
    validate_citation_metadata(failures)
    validate_research_source_claims(failures)
    validate_research_scenarios(failures)

    if failures:
        print("Public-site validation failed:", file=sys.stderr)
        for failure in failures:
            print(f"- {failure}", file=sys.stderr)
        return 1

    print(
        f"Validated {len(pages)} HTML pages, native image geometry, canonical routes, sitemap, trust manifest, citation metadata, research source anchors, deterministic scenarios, accessibility basics, and local links."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
