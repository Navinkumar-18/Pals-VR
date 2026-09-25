"""OCR engine factory (Phase E).

Selects the active engine from settings:
  * "rapid"     — RapidOcrEngine (default; pip-only, English)
  * "tesseract" — TesseractOcrEngine (needs binary + tessdata for en/hi/ta)
  * "auto"      — try rapid first, fall back to tesseract

The factory is cached so the (possibly slow) engine load happens once. Tests
inject a fake engine through :func:`override_ocr_engine`.
"""
from __future__ import annotations

import logging

from app.config.settings import get_settings

logger = logging.getLogger("ambedkar.api.ocr")

_override_engine = None  # test seam — shallow override without polluting settings
_cached_engine = None


def override_ocr_engine(engine) -> None:
    """Install a fake engine for tests (cleared via :-1:param:`None`)."""
    global _override_engine
    _override_engine = engine


def clear_ocr_engine_override() -> None:
    global _override_engine
    _override_engine = None


def _build_engine():
    from app.ocr.base import OcrEngineUnavailableError
    from app.ocr.rapid_engine import RapidOcrEngine

    configured = (get_settings().ocr_engine or "rapid").lower().strip()
    if configured == "tesseract":
        from app.ocr.tesseract_engine import TesseractOcrEngine

        return TesseractOcrEngine()

    if configured == "rapid":
        try:
            return RapidOcrEngine()
        except OcrEngineUnavailableError as exc:
            raise OcrEngineUnavailableError(
                "RapidOCR is configured but unavailable. "
                "Install rapidocr-onnxruntime or switch OCR_ENGINE=tesseract."
            ) from exc

    # "auto" (or anything unknown): prefer rapid, fall back to tesseract.
    try:
        return RapidOcrEngine()
    except OcrEngineUnavailableError:
        from app.ocr.tesseract_engine import TesseractOcrEngine

        try:
            return TesseractOcrEngine()
        except OcrEngineUnavailableError as exc:
            raise OcrEngineUnavailableError(
                "No OCR engine available: install rapidocr-onnxruntime "
                "or configure Tesseract OCR."
            ) from exc


def get_ocr_engine():
    """Return the configured OCR engine.

    The override (test seam) always wins; otherwise the engine is built once
    and cached.
    """
    if _override_engine is not None:
        return _override_engine
    global _cached_engine
    if _cached_engine is None:
        _cached_engine = _build_engine()
    return _cached_engine


def reset_ocr_engine_cache() -> None:
    """Discard the cached engine (used by tests that swap engines)."""
    global _cached_engine
    _cached_engine = None