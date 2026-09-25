"""OCR engine abstraction (Phase E).

The backend never binds to one OCR implementation. Any engine implementing
:class:`OcrEngine` can be plugged in (RapidOcrEngine ships now;
TesseractOcrEngine is available when the tesseract binary + tessdata exist).

Language policy: a requested language that the active engine cannot handle is
rejected with :class:`OcrLanguageUnavailableError` — the pipeline never
silently OCRs with the wrong language model.
"""
from __future__ import annotations

from abc import ABC, abstractmethod
from dataclasses import dataclass, field


class OcrError(Exception):
    """Base class for OCR pipeline errors."""


class OcrEngineUnavailableError(OcrError):
    """The configured OCR engine could not be loaded (missing dependency)."""


class OcrLanguageUnavailableError(OcrError):
    """The requested language has no model in the active OCR engine.

    This is a configuration error by design: OCR must never silently fall back
    to a wrong language model.
    """


@dataclass
class OcrPageResult:
    """Extracted text for one page of a document."""

    page_number: int
    text: str
    language: str = "en"
    confidence: float | None = None
    # Human label of the engine that produced the result.
    engine: str = ""

    def as_dict(self) -> dict:
        return {
            "page_number": self.page_number,
            "text": self.text,
            "language": self.language,
            "confidence": self.confidence,
            "engine": self.engine,
        }


class OcrEngine(ABC):
    """Contract for OCR providers.

    Implementations produce per-page results for a raw image or a PDF; multipage
    PDF handling lives in the engine so each provider can decide how to render
    pages (rapid: PyMuPDF render to PNG; tesseract: PDF handling may be added).
    """

    #: Machine name shown in logs/API responses (e.g. "rapid", "tesseract").
    name: str = "ocr"

    @property
    @abstractmethod
    def supported_language_codes(self) -> set[str]:
        """ISO codes the engine can actually OCR (e.g. {"en"})."""

    def language_available(self, language: str) -> bool:
        """True when the engine has a model for this language code."""
        return (language or "").lower() in self.supported_language_codes

    def require_language(self, language: str) -> str:
        normalized = (language or "").lower().strip() or "en"
        if not self.language_available(normalized):
            available = (
                ", ".join(
                    language_display_name(code)
                    for code in sorted(self.supported_language_codes)
                )
                or "(none)"
            )
            raise OcrLanguageUnavailableError(
                f"OCR engine '{self.name}' has no model for language '{language}'. "
                f"Available languages: {available}. Configure a matching engine "
                f"or install the required language model."
            )
        return normalized

    @abstractmethod
    def ocr_image(self, image_bytes: bytes, language: str = "en") -> OcrPageResult:
        """OCR a single page image (PNG/JPEG bytes). Page number is 1."""

    @abstractmethod
    def ocr_pdf(self, pdf_bytes: bytes, language: str = "en") -> list[OcrPageResult]:
        """OCR every page of a PDF into ordered per-page results."""


# Canonical language labels for the API (ISO code -> display name).
LANGUAGE_NAMES: dict[str, str] = {
    "en": "English",
    "hi": "Hindi",
    "ta": "Tamil",
}

DEFAULT_LANGUAGE = "en"

# The full set of languages the architecture can express (engines report which
# of these they can actually serve at runtime).
SUPPORTED_LANGUAGE_CODES: set[str] = set(LANGUAGE_NAMES)


def language_display_name(code: str) -> str:
    return LANGUAGE_NAMES.get((code or "").lower(), code or "")