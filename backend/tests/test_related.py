"""Related-content endpoint tests."""


def test_related_returns_documents_and_types(client, seeded):
    r = client.get("/api/related/DOC-A-001")
    assert r.status_code == 200
    body = r.json()
    assert body["sourceId"] == "DOC-A-001"
    assert body["count"] >= 1
    items = body["items"]
    by_type = {item["relationshipType"] for item in items}
    assert "related" in by_type
    doc_ids = {item["document"]["id"] for item in items}
    assert "DOC-A-002" in doc_ids
    # event/person links do not leak into document-related results
    assert all(item["document"]["type"] in (1, 2, 3, 4, 5, 6, 7, 8, 9) for item in items)


def test_related_draft_source_is_hidden(client, seeded):
    assert client.get("/api/related/DOC-A-003").status_code == 404


def test_related_invalid_id_404(client, seeded):
    assert client.get("/api/related/NOPE").status_code == 404