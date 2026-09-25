"""Health endpoint used by Unity for connectivity / offline fallback checks."""
from datetime import datetime, timezone

from fastapi import APIRouter, Depends
from sqlalchemy import text
from sqlalchemy.orm import Session

from app.config.settings import get_settings
from app.database.session import get_db

router = APIRouter(tags=["health"])


@router.get("/health")
def health(db: Session = Depends(get_db)):
    db_status = "ok"
    try:
        db.execute(text("SELECT 1"))
    except Exception:
        db_status = "unavailable"
    settings = get_settings()
    return {
        "status": "ok" if db_status == "ok" else "degraded",
        "app": settings.app_name,
        "version": settings.app_version,
        "environment": settings.environment,
        "database": db_status,
        "time": datetime.now(timezone.utc).isoformat(),
    }