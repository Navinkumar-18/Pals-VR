"""OCR package (Phase E).

Engine abstraction + concrete engines + the factory that selects the active
one. See ``app.services.ocr_service`` for the ingestion pipeline.
"""
from app.ocr.base import (
    DEFAULT_LANGUAGE,
    OcrEngine,
    OcrEngineUnavailableError,
    OcrError,
    OcrLanguageUnavailableError,
    OcrPageResult,
    language_display_name,
)
from app.ocr.factory import (
    clear_ocr_engine_override,
    get_ocr_engine,
    override_ocr_engine,
    reset_ocr_engine_cache,
)

__all__ = [
    "DEFAULT_LANGUAGE",
    "OcrEngine",
    "OcrEngineUnavailableError",
    "OcrError",
    "OcrLanguageUnavailableError",
    "OcrPageResult",
    "clear_ocr_engine_override",
    "get_ocr_engine",
    "language_display_name",
    "override_ocr_engine",
    "reset_ocr_engine_cache",
]