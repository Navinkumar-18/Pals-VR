"""PDF page rendering (Phase E).

Converts a PDF into ordered page images (PNG bytes) using PyMuPDF so OCR
engines only ever see plain raster pages. PyMuPDF ships wheels for Windows and
needs no external poppler binary, keeping the backend self-contained.
"""
from __future__ import annotations

import logging

from app.ocr.base import OcrError

logger = logging.getLogger("ambedkar.api.ocr")

RENDER_DPI = 200
PNG_FORMAT = "png"


def render_pdf_pages(pdf_bytes: bytes) -> list[bytes]:
    """Render every page of ``pdf_bytes`` to PNG bytes, in page order.

    Raises :class:`OcrError` for corrupt/empty input so callers can mark the
    document FAILED with a clear message.
    """
    if not pdf_bytes or not pdf_bytes.strip():
        raise OcrError("PDF is empty — cannot render pages.")

    try:
        import pymupdf  # PyMuPDF

        pages: list[bytes] = []
        with pymupdf.open(stream=pdf_bytes, filetype="pdf") as doc:
            if doc.page_count == 0:
                raise OcrError("PDF contains no pages.")
            for page in doc:
                pix = page.get_pixmap(dpi=RENDER_DPI)
                pages.append(pix.tobytes(PNG_FORMAT))
        return pages
    except OcrError:
        raise
    except Exception as exc:  # pymupdf.FileDataError, runtime errors, etc.
        logger.warning("PDF page rendering failed: %s", exc)
        raise OcrError(f"Unsupported or corrupted PDF: {exc}") from exc