"""One-shot DB initialization + [SAMPLE] seeding.

Usage (from the backend/ directory, inside the virtualenv):

    python scripts/init_db.py

Equivalent to what the API does on startup when AUTO_SEED=true.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from app import models  # noqa: E402, F401  (register tables)
from app.database.base import Base  # noqa: E402
from app.database.session import SessionLocal, engine  # noqa: E402
from app.seed.seed_demo_data import seed_demo_data  # noqa: E402


def main() -> None:
    print("Ensuring tables...")
    Base.metadata.create_all(bind=engine)
    with SessionLocal() as db:
        added = seed_demo_data(db)
    print(f"Done. Added {added} new [SAMPLE] document(s) (seed is idempotent).")


if __name__ == "__main__":
    main()