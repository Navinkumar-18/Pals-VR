"""Document CRUD + published filtering + admin-key tests."""


def _new_doc_payload(**overrides):
    payload = {
        "id": "NEW-DOC-1",
        "type": "DOCUMENT",
        "title": "Brand New Sample Document",
        "description": "Created through the API.",
        "category": "Constitutional Work",
        "date": "1927",
        "language": "en",
        "source": "api-test",
        "status": "PUBLISHED",
        "is_sample_data": True,
        "tags": ["api", "sample-demo"],
        "media": [
            {"media_type": "PDF", "reference": "documents/new-doc-1.pdf"},
            {"media_type": "DOCUMENT_SCAN", "reference": "images/new-doc-1-p1.jpg", "sort_order": 1},
        ],
        "related_ids": ["DOC-A-001"],
    }
    payload.update(overrides)
    return payload


def test_list_defaults_to_published_only(client, seeded):
    r = client.get("/api/documents")
    assert r.status_code == 200
    body = r.json()
    ids = [d["id"] for d in body["items"]]
    assert "DOC-A-001" in ids
    assert "DOC-A-002" in ids
    assert "DOC-A-003" not in ids  # DRAFT is hidden from the public API
    assert body["count"] >= 2
    # Unity contract: camelCase keys + numeric type
    published = next(d for d in body["items"] if d["id"] == "DOC-A-001")
    assert published["isSampleData"] is True
    assert published["type"] == 5  # DOCUMENT
    assert "DOC-A-002" in published["relatedIds"]
    assert published["image"] == "images/doc-a-001.jpg"
    assert published["status"] == "PUBLISHED"


def test_get_published_document(client, seeded):
    r = client.get("/api/documents/DOC-A-001")
    assert r.status_code == 200
    d = r.json()
    assert d["title"] == "Sample Law Document"
    assert d["type"] == 5
    assert d["media"][0]["reference"] == "images/doc-a-001.jpg"


def test_get_draft_is_hidden_publicly(client, seeded):
    r = client.get("/api/documents/DOC-A-003")
    assert r.status_code == 404


def test_get_invalid_id_returns_404(client, seeded):
    r = client.get("/api/documents/DOES-NOT-EXIST")
    assert r.status_code == 404


def test_create_document(client, seeded):
    r = client.post("/api/documents", json=_new_doc_payload())
    assert r.status_code == 201
    d = r.json()
    assert d["id"] == "NEW-DOC-1"
    assert d["document"] == "documents/new-doc-1.pdf"
    assert d["pages"] == ["images/new-doc-1-p1.jpg"]
    assert "DOC-A-001" in d["relatedIds"]

    fetched = client.get("/api/documents/NEW-DOC-1")
    assert fetched.status_code == 200
    assert fetched.json()["title"] == "Brand New Sample Document"


def test_create_duplicate_id_409(client, seeded):
    r = client.post("/api/documents", json=_new_doc_payload(id="DOC-A-001"))
    assert r.status_code == 409


def test_create_invalid_input_422(client, seeded):
    assert client.post("/api/documents", json=_new_doc_payload(id="")) .status_code == 422
    assert client.post("/api/documents", json=_new_doc_payload(title="")).status_code == 422
    assert client.post("/api/documents", json=_new_doc_payload(type="NOT_A_TYPE")).status_code == 422


def test_update_document(client, seeded):
    r = client.put("/api/documents/DOC-A-001", json={"title": "Updated Title"})
    assert r.status_code == 200
    assert r.json()["title"] == "Updated Title"
    # untouched fields survive
    assert r.json()["category"] == "Constitutional Work"
    assert client.put("/api/documents/MISSING", json={"title": "x"}).status_code == 404


def test_delete_document(client, seeded):
    r = client.delete("/api/documents/DOC-A-002")
    assert r.status_code == 200
    assert r.json()["message"]
    assert client.get("/api/documents/DOC-A-002").status_code == 404
    # related link from DOC-A-001 to DOC-A-002 was cleaned up
    related = client.get("/api/related/DOC-A-001").json()["items"]
    assert all(item["document"]["id"] != "DOC-A-002" for item in related)
    assert client.delete("/api/documents/MISSING").status_code == 404


def test_admin_key_gates_write_operations(client, seeded, monkeypatch):
    from app.config.settings import get_settings

    monkeypatch.setattr(get_settings(), "admin_api_key", "test-secret")
    try:
        # without the key: writes are forbidden
        assert client.post("/api/documents", json=_new_doc_payload(id="ADMIN-DOC-1")).status_code == 403
        assert client.put("/api/documents/DOC-A-001", json={"title": "x"}).status_code == 403
        assert client.delete("/api/documents/DOC-A-001").status_code == 403
        # with the key: writes succeed
        headers = {"X-API-Key": "test-secret"}
        assert (
            client.post("/api/documents", json=_new_doc_payload(id="ADMIN-DOC-1"), headers=headers).status_code
            == 201
        )
        assert (
            client.put("/api/documents/DOC-A-001", json={"title": "Admin edit"}, headers=headers).status_code
            == 200
        )
        # non-published read also requires the key
        assert client.get("/api/documents/DOC-A-003").status_code == 404
        assert client.get("/api/documents/DOC-A-003", headers=headers).status_code == 200
        # status filter honours the key too
        drafts = client.get("/api/documents", params={"status": "DRAFT"}, headers=headers)
        assert drafts.status_code == 200
        assert "DOC-A-003" in [d["id"] for d in drafts.json()["items"]]
        assert client.get("/api/documents", params={"status": "DRAFT"}).status_code == 403
    finally:
        monkeypatch.setattr(get_settings(), "admin_api_key", "")