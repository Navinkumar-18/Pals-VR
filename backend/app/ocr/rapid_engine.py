"""RapidOCR engine (default, pip-only).

Uses ``rapidocr-onnxruntime`` which ships ONNX models and sklearn-free deps in
the wheel — no system binary required. English is supported out of the box;
requests for languages without a bundled model raise
:class:`OcrLanguageUnavailableError` (never a silent wrong-language run).

Note: RapidOCR's default bundle is English. Hindi/Tamil support requires a
provider with those models (see the Tesseract engine) — the abstraction keeps
the rest of the pipeline unchanged.
"""
from __future__ import annotations

import logging

from app.ocr.base import (
    OcrEngine,
    OcrEngineUnavailableError,
    OcrPageResult,
)
from app.ocr.pdf_renderer import render_pdf_pages

logger = logging.getLogger("ambedkar.api.ocr")


class RapidOcrEngine(OcrEngine):
    name = "rapid"

    @property
    def supported_language_codes(self) -> set[str]:
        # Default bundled models cover English; expose only what we guarantee.
        return {"en"}

    def __init__(self) -> None:
        self._client = None

    def _get_client(self):
        if self._client is None:
            try:
                from rapidocr_onnxruntime import RapidOCR

                self._client = RapidOCR()
            except Exception as exc:  # pragma: no cover - dependency failure
                raise OcrEngineUnavailableError(
                    f"RapidOCR engine could not start: {exc}"
                ) from exc
        return self._client

    def ocr_image(self, image_bytes: bytes, language: str = "en") -> OcrPageResult:
        normalized = self.require_language(language)
        client = self._get_client()
        try:
            result, _elapsed = client(image_bytes)
        except Exception as exc:  # pragma: no cover - engine runtime failure
            logger.warning("RapidOCR failed on an image: %s", exc)
            raise OcrEngineUnavailableError(f"RapidOCR processing failed: {exc}") from exc

        if not result:
            # Empty OCR result is valid (blank page / no text found).
            return OcrPageResult(page_number=1, text="", language=normalized, engine=self.name)

        # rapidocr returns [[box, text, score], ...].
        lines = [str(item[1]) for item in result if len(item) > 1]
        scores = [float(item[2]) for item in result if len(item) > 2]
        confidence = round(sum(scores) / len(scores), 3) if scores else None
        text = "\n".join(lines).strip()
        return OcrPageResult(
            page_number=1, text=text, language=normalized,
            confidence=confidence, engine=self.name,
        )

    def ocr_pdf(self, pdf_bytes: bytes, language: str = "en") -> list[OcrPageResult]:
        normalized = self.require_language(language)
        images = render_pdf_pages(pdf_bytes)
        pages: list[OcrPageResult] = []
        for page_number, image_bytes in enumerate(images, start=1):
            result = self.ocr_image(image_bytes, normalized)
            result.page_number = page_number
            pages.append(result)
        return pages