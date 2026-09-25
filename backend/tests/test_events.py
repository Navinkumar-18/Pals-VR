"""Event endpoint tests."""


def test_list_events(client, seeded):
    r = client.get("/api/events")
    assert r.status_code == 200
    body = r.json()
    ids = [e["id"] for e in body["items"]]
    assert "EVT-1" in ids
    assert body["count"] >= 1


def test_get_event(client, seeded):
    r = client.get("/api/events/EVT-1")
    assert r.status_code == 200
    e = r.json()
    assert e["title"] == "Test Event"
    assert e["date"] == "1916"


def test_get_missing_event_404(client, seeded):
    assert client.get("/api/events/NOPE").status_code == 404