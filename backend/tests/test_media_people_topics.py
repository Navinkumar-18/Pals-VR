"""Media / people / topics endpoint tests."""


def test_list_media(client, seeded):
    r = client.get("/api/media")
    assert r.status_code == 200
    body = r.json()
    ids = [m["id"] for m in body["items"]]
    assert "DOC-A-001-med-1" in ids
    item = body["items"][0]
    assert item["mediaType"] == "IMAGE"
    assert item["reference"] == "images/doc-a-001.jpg"
    assert item["documentId"] == "DOC-A-001"


def test_list_media_filtered(client, seeded):
    r = client.get("/api/media", params={"media_type": "IMAGE"})
    assert all(m["mediaType"] == "IMAGE" for m in r.json()["items"])
    r2 = client.get("/api/media", params={"document_id": "DOC-A-001"})
    assert r2.json()["count"] == 1


def test_list_people(client, seeded):
    r = client.get("/api/people")
    assert r.status_code == 200
    names = [p["fullName"] for p in r.json()["items"]]
    assert "Test Person" in names


def test_list_topics(client, seeded):
    r = client.get("/api/topics")
    assert r.status_code == 200
    names = [t["name"] for t in r.json()["items"]]
    assert "Constitution" in names