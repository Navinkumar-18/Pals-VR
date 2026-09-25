"""Application configuration.

All values come from environment variables / a local `.env` file so the
backend never hard-codes credentials. Unit/integration tests override
`DATABASE_URL` before importing the app and call `reset_settings()`.
"""
from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    app_name: str = "Ambedkar Digital Heritage API"
    app_version: str = "0.1.0"
    environment: str = "development"

    # PostgreSQL for production; SQLite is accepted for local prototype/test
    # runs only.
    database_url: str = (
        "postgresql+psycopg://archive:archive@localhost:5432/ambedkar_heritage"
    )

    # Empty string = dev mode (administrative endpoints open with a warning).
    admin_api_key: str = ""

    auto_seed: bool = True

    # Comma separated; "*" = allow all (dev convenience).
    cors_origins: str = "*"

    api_prefix: str = "/api"

    # ------------------------------------------------------------------
    # OCR / document ingestion (Phase E)
    # ------------------------------------------------------------------
    # Root folder for uploaded original scans (relative to the backend cwd).
    # Originals are preserved byte-for-byte and never modified.
    storage_root: str = "storage"

    # Max accepted upload size in megabytes.
    max_upload_mb: int = 25

    # OCR engine: "rapid" (default, pip-only, English) | "tesseract"
    # (needs the tesseract binary + tessdata for en/hi/ta) | "auto".
    ocr_engine: str = "rapid"

    # Default OCR language (ISO 639-1). Supported set depends on the engine
    # and installed models; requesting an unavailable language returns a
    # clear configuration error instead of silently using another language.
    ocr_default_language: str = "en"

    # Run OCR inline (false) or on a background thread (true). Tests set this
    # to false so upload/OCR flows are deterministic.
    ocr_async: bool = True


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    return Settings()


def reset_settings() -> None:
    """Discard the cached settings so tests can re-read environment values."""
    get_settings.cache_clear()