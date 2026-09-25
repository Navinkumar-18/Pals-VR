"""FastAPI application factory and entry point.

Public API prefix: /api  (matches Unity's MuseumApp.ApiBaseUrl which ends in
"/api"). OpenAPI docs: /docs and /openapi.json (enabled by default).
"""
import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.api.routes import (
    documents,
    events,
    health,
    media,
    people,
    related,
    search,
    topics,
)
from app.config.settings import get_settings
from app.database.base import Base
from app.database.session import SessionLocal, engine
from app import models  # noqa: F401  (registers all tables on Base.metadata)

logger = logging.getLogger("ambedkar.api")
logging.basicConfig(level=logging.INFO)


def init_database() -> None:
    """Create tables (idempotent) and optionally seed [SAMPLE] data."""
    settings = get_settings()
    try:
        Base.metadata.create_all(bind=engine)
        logger.info("Database tables ensured (engine: %s)", settings.database_url)
    except Exception as exc:  # operational error, e.g. PostgreSQL not running
        logger.error(
            "Database unavailable (%s). Tables NOT created — set DATABASE_URL to "
            "a reachable PostgreSQL (or sqlite:/// for local prototype work).",
            exc,
        )
        return
    if settings.auto_seed:
        try:
            from app.seed.seed_demo_data import seed_demo_data

            with SessionLocal() as db:
                added = seed_demo_data(db)
            if added:
                logger.info("Seeded %d new [SAMPLE] document(s).", added)
        except Exception as exc:  # pragma: no cover - defensive
            logger.error("Seed step failed: %s", exc)


@asynccontextmanager
async def lifespan(app: FastAPI):
    init_database()
    yield


def create_app() -> FastAPI:
    settings = get_settings()
    app = FastAPI(
        title=settings.app_name,
        version=settings.app_version,
        lifespan=lifespan,
    )

    origins = [o.strip() for o in settings.cors_origins.split(",") if o.strip()]
    app.add_middleware(
        CORSMiddleware,
        allow_origins=origins or ["*"],
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    prefix = settings.api_prefix
    for router in (
        health.router,
        documents.router,
        events.router,
        media.router,
        people.router,
        topics.router,
        related.router,
        search.router,
    ):
        app.include_router(router, prefix=prefix)

    if not settings.admin_api_key:
        logger.warning(
            "ADMIN_API_KEY is empty — administrative endpoints (POST/PUT/DELETE, "
            "non-published reads) are OPEN. Set it before exposing the API publicly."
        )
    return app


app = create_app()