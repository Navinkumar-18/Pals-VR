"""Shared API dependencies."""
from typing import Optional

from fastapi import Header, HTTPException

from app.config.settings import get_settings


def admin_key_is_open() -> bool:
    """True when no ADMIN_API_KEY is configured (development mode)."""
    return not get_settings().admin_api_key


def check_admin(x_api_key: Optional[str] = Header(default=None)) -> None:
    """Reject administrative calls unless the configured key matches.

    When ADMIN_API_KEY is empty the API runs in open development mode (a
    warning is logged at startup); the README is explicit that a key must be
    set before public exposure.
    """
    key = get_settings().admin_api_key
    if key and x_api_key != key:
        raise HTTPException(
            status_code=403, detail="Admin API key required for this operation"
        )


def require_admin(x_api_key: Optional[str] = Header(default=None)) -> None:
    check_admin(x_api_key)
    return None