#!/usr/bin/env python3
"""Disposable HTTP subscriber. No provider SDK, credentials, or production path.

The fsynced receipt is written before the injected process crash. Effects are
deduplicated by CloudEvent ID; /readback reopens the on-disk ledger each time.
"""
import argparse
import hmac
import json
import os
import threading
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, required=True)
    parser.add_argument("--store", type=Path, required=True)
    args = parser.parse_args()
    args.store.mkdir(parents=True, exist_ok=True)
    lock = threading.Lock()
    token = os.environ["APP_API_TOKEN"]

    def rows():
        path = args.store / "ledger.jsonl"
        return [json.loads(line) for line in path.read_text().splitlines()] if path.exists() else []

    def append(value):
        with (args.store / "ledger.jsonl").open("a") as handle:
            handle.write(json.dumps(value, sort_keys=True) + "\n")
            handle.flush()
            os.fsync(handle.fileno())

    def state(value=None):
        endpoint = (args.store / "state-endpoint").read_text().strip()
        request = urllib.request.Request(endpoint + "/v1.0/state/qualification-state" + ("/effects" if value is None else ""),
            data=None if value is None else json.dumps([{"key": "effects", "value": value}]).encode(),
            headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(request, timeout=5) as response:
            body = response.read()
            return {"status": response.status, "value": json.loads(body) if body else [], "etag": response.headers.get("ETag")}

    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_args):
            pass

        def respond(self, status, payload):
            body = json.dumps(payload).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def do_GET(self):
            if self.path == "/readback":
                with lock:
                    try:
                        persisted = state()
                        self.respond(200, {"kind": "persisted-application-http-readback", "stateComponent": "qualification-state", "stateReadback": persisted, "ledger": rows() + persisted["value"]})
                    except (OSError, urllib.error.URLError):
                        self.respond(503, {"error": "Dapr state readback unavailable"})
            elif self.path == "/dapr/subscribe":
                self.respond(200, [])
            else:
                self.respond(200, {"ready": True})

        def do_POST(self):
            if not hmac.compare_digest(self.headers.get("dapr-api-token", ""), token):
                self.respond(403, {"status": "RETRY"})
                return
            event = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
            event_id = event["id"]
            with lock:
                ledger = rows()
                append({"kind": "receipt", "id": event_id, "messageId": event.get("data", {}).get("messageId"), "route": self.path})
                if event.get("data", {}).get("crashOnce") and not any(x["id"] == event_id for x in ledger):
                    os._exit(86)
                if self.path == "/poison":
                    self.respond(500, {"status": "RETRY"})
                    return
                if self.path == "/quarantine" and (args.store / "quarantine-blocked").exists():
                    self.respond(500, {"status": "RETRY"})
                    return
                try:
                    effects = state()["value"]
                    if not any(x["id"] == event_id for x in effects):
                        effects.append({"kind": "effect", "id": event_id, "messageId": event.get("data", {}).get("messageId"), "route": self.path})
                        state(effects)
                except (OSError, urllib.error.URLError):
                    self.respond(500, {"status": "RETRY"})
                    return
                self.respond(200, {"status": "SUCCESS"})

    ThreadingHTTPServer(("127.0.0.1", args.port), Handler).serve_forever()


if __name__ == "__main__":
    main()
