"""Metadata search endpoint tests (Phase D: text/metadata filtering only)."""


def test_search_by_text(client, seeded):
    r = client.get("/api/search", params={"q": "constitutional"})
    assert r.status_code == 200
    ids = [d["id"] for d in r.json()["items"]]
    assert "DOC-A-001" in ids


def test_search_matches_description(client, seeded):
    r = client.get("/api/search", params={"q": "handwritten"})
    assert r.status_code == 200
    assert "DOC-A-002" in [d["id"] for d in r.json()["items"]]


def test_search_filters_by_type(client, seeded):
    r = client.get("/api/search", params={"type": "MANUSCRIPT"})
    items = r.json()["items"]
    assert len(items) == 1
    assert items[0]["id"] == "DOC-A-002"


def test_search_filters_by_category_and_hides_drafts(client, seeded):
    r = client.get("/api/search", params={"category": "Constitutional Work"})
    ids = [d["id"] for d in r.json()["items"]]
    assert "DOC-A-001" in ids
    assert "DOC-A-003" not in ids  # DRAFT never surfaces publicly


def test_search_no_matches(client, seeded):
    r = client.get("/api/search", params={"q": "zzzznothing"})
    assert r.status_code == 200
    assert r.json()["count"] == 0