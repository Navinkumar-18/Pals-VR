"""Conservative OCR text cleaning (Phase E).

The pipeline must stay faithful to the source scan: we normalize whitespace /
line endings and remove *detectable* repeated headers/footers, but we never
rewrite historical text or "fix" uncertain words. Uncertain or empty results
are preserved as-is so Phase F can consume them directly.
"""
from __future__ import annotations

import re
import unicodedata

# Unicode normalization safe for OCR output: NFC + map common curly punctuation
# to their plain ASCII equivalents. This is presentation-only; no word content
# changes.
_PUNCT_MAP = str.maketrans(
    {
        "\u2018": "'",  # ‘
        "\u2019": "'",  # ’
        "\u201c": '"',  # “
        "\u201d": '"',  # ”
        "\u2013": "-",  # –
        "\u2014": "-",  # —
        "\u00a0": " ",  # non-breaking space
    }
)

_MULTI_BLANK_LINE = re.compile(r"\n[ \t]*\n(?:[ \t]*\n)+")
_MULTI_SPACE = re.compile(r"[ \t]{2,}")


def normalize_characters(text: str) -> str:
    """NFC normalization + mapping of common curly punctuation."""
    return unicodedata.normalize("NFC", text).translate(_PUNCT_MAP)


def fix_line_endings(text: str) -> str:
    """Unify CRLF/CR to LF without disturbing content."""
    return text.replace("\r\n", "\n").replace("\r", "\n")


def collapse_excessive_whitespace(text: str) -> str:
    """Collapse runs of blank lines to a single blank line and runs of spaces."""
    text = _MULTI_BLANK_LINE.sub("\n\n", text)
    # Trim trailing whitespace on each line, collapse interior runs of spaces.
    lines = [re.sub(r"[ \t]+$", "", line) for line in text.split("\n")]
    lines = [_MULTI_SPACE.sub(" ", line) for line in lines]
    return "\n".join(lines).strip("\n")


def detect_removable_running_heads(text: str) -> str:
    """Remove a *detectable* repeated running header/footer.

    A single line that repeats, non-continuously, at least 3 times is almost
    always a page header/footer or scanner banner. Only remove such lines;
    never touch body text. Conservative: requires >= 3 occurrences.
    """
    lines = text.split("\n")
    counts: dict[str, int] = {}
    for line in lines:
        stripped = line.strip()
        if stripped and len(stripped) <= 120:
            counts[stripped] = counts.get(stripped, 0) + 1
    removable = {line for line, n in counts.items() if n >= 3}
    if not removable:
        return text

    kept = [line for line in lines if line.strip() not in removable]
    return "\n".join(kept).strip("\n")


def clean_text(text: str | None) -> str:
    """Full cleaning pipeline for one page's raw OCR text.

    Ordered, conservative transforms:
      1. unify line endings
      2. normalize characters (NFC + curly punctuation)
      3. remove detectable repeated running headers/footers
      4. collapse excessive whitespace
    """
    if not text:
        return ""
    cleaned = fix_line_endings(text)
    cleaned = normalize_characters(cleaned)
    cleaned = detect_removable_running_heads(cleaned)
    cleaned = collapse_excessive_whitespace(cleaned)
    return cleaned


def aggregate_pages(page_texts: list[str]) -> str:
    """Join cleaned per-page text into one searchable transcript.

    Each page is separated by a blank line so page boundaries stay visible to
    Phase F consumers without flattening everything.
    """
    return "\n\n".join(t for t in page_texts if t and t.strip())