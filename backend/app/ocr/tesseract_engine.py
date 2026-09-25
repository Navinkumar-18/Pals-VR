"""Tesseract OCR engine (optional — English/Hindi/Tamil).

Used when the tesseract binary and the matching tessdata language packs are
available on the host. RapidOCR is the default engine because it is
pip-installable with no system binary; this engine covers languages rapid's
bundled models do not (hin/tam).

Construction fails loudly (raise :class:`OcrEngineUnavailableError`) when the
binary is missing so ``auto`` selection can fall through to another engine
instead of silently degrading.
"""
from __future__ import annotations

import logging
import os
import shutil
import subprocess

from app.ocr.base import (
    OcrEngine,
    OcrEngineUnavailableError,
    OcrPageResult,
)
from app.ocr.pdf_renderer import render_pdf_pages

logger = logging.getLogger("ambedkar.api.ocr")

# ISO-639-1 -> tesseract three-letter tessdata name.
_TESS_LANG: dict[str, str] = {
    "en": "eng",
    "hi": "hin",
    "ta": "tam",
}


class TesseractOcrEngine(OcrEngine):
    name = "tesseract"

    def __init__(self, tesseract_cmd: str = "tesseract", tessdata_dir: str | None = None) -> None:
        self._cmd = shutil.which(tesseract_cmd)
        if not self._cmd:
            raise OcrEngineUnavailableError(
                "Tesseract binary not found. Install Tesseract OCR and set "
                "tessdata language packs, or use the default rapid engine."
            )
        self._tessdata_dir = tessdata_dir
        self._installed: set[str] | None = None

    @property
    def supported_language_codes(self) -> set[str]:
        if self._installed is None:
            self._installed = self._detect_languages()
        return self._installed

    def _detect_languages(self) -> set[str]:
        try:
            env = dict(self._env())
            output = subprocess.run(
                [self._cmd, "--list-langs"],
                capture_output=True, text=True, timeout=15, env=env,
            )
        except Exception as exc:  # pragma: no cover - host-specific
            logger.warning("Could not list tesseract languages: %s", exc)
            return set()
        installed_three = {
            line.strip()
            for line in output.stdout.splitlines()
            if line.strip() and not line.strip().startswith("List")
        }
        return {iso for iso, code in _TESS_LANG.items() if code in installed_three}

    def _env(self) -> dict:
        env = dict(os.environ)
        if self._tessdata_dir:
            env["TESSDATA_PREFIX"] = str(self._tessdata_dir)
        return env

    def ocr_image(self, image_bytes: bytes, language: str = "en") -> OcrPageResult:
        normalized = self.require_language(language)
        code = _TESS_LANG[normalized]
        try:
            import pytesseract
            from PIL import Image
            import io

            pytesseract.pytesseract.tesseract_cmd = self._cmd
            image = Image.open(io.BytesIO(image_bytes))
            data = pytesseract.image_to_data(
                image, lang=code, output_type=pytesseract.Output.DICT
            )
        except Exception as exc:  # pragma: no cover - engine runtime failure
            logger.warning("Tesseract failed on an image: %s", exc)
            raise OcrEngineUnavailableError(f"Tesseract processing failed: {exc}") from exc

        text_lines = []
        confidences: list[float] = []
        for i, level in enumerate(data.get("level", [])):
            text = (data.get("text") or [""])[i] if i < len(data.get("text", [])) else ""
            conf = (data.get("conf") or ["-1"])[i] if i < len(data.get("conf", [])) else "-1"
            if text and text.strip():
                text_lines.append(text.strip())
                try:
                    value = float(conf)
                    if value >= 0:
                        confidences.append(value)
                except (TypeError, ValueError):
                    pass

        confidence = round(sum(confidences) / len(confidences), 3) if confidences else None
        return OcrPageResult(
            page_number=1,
            text="\n".join(text_lines).strip(),
            language=normalized,
            confidence=confidence,
            engine=self.name,
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