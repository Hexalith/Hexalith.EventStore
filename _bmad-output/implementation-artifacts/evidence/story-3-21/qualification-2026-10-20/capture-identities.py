#!/usr/bin/env python3
"""Resolve public candidate identities without Docker pulls or cluster access."""
import argparse
import datetime as dt
import hashlib
import json
from pathlib import Path
import urllib.request


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    receipts = []

    def fetch(url, filename=None, headers=None):
        started = dt.datetime.now(dt.timezone.utc).isoformat()
        req = urllib.request.Request(url, headers=headers or {"User-Agent": "story-3-21-read-only-identity"})
        with urllib.request.urlopen(req, timeout=30) as response:
            raw, response_headers = response.read(), dict(response.headers)
        digest = hashlib.sha256(raw).hexdigest()
        if filename:
            (args.output / filename).write_bytes(raw)
            receipts.append({"url": url, "file": filename, "sha256": digest,
                "startedUtc": started, "endedUtc": dt.datetime.now(dt.timezone.utc).isoformat()})
        return raw, response_headers

    images = []
    refs = [("ghcr.io", "dapr/" + name, "1.18.7") for name in
            ("daprd", "operator", "injector", "sentry", "placement", "scheduler")]
    refs += [("docker.io", "library/rabbitmq", "4.3.6-management"),
             ("docker.io", "apache/kafka", "4.3.1")]
    for registry, repo, tag in refs:
        if registry == "ghcr.io":
            token_url = "https://ghcr.io/token?scope=repository:" + repo + ":pull"
            token = json.loads(fetch(token_url)[0])["token"]
            manifest_url = "https://ghcr.io/v2/" + repo + "/manifests/" + tag
        else:
            token_url = "https://auth.docker.io/token?service=registry.docker.io&scope=repository:" + repo + ":pull"
            token = json.loads(fetch(token_url)[0])["token"]
            manifest_url = "https://registry-1.docker.io/v2/" + repo + "/manifests/" + tag
        filename = repo.replace("/", "-") + "-" + tag + ".manifest.json"
        raw, headers = fetch(manifest_url, filename, {"Authorization": "Bearer " + token,
            "Accept": "application/vnd.oci.image.index.v1+json, application/vnd.docker.distribution.manifest.list.v2+json"})
        digest = "sha256:" + hashlib.sha256(raw).hexdigest()
        reported = next(v for k, v in headers.items() if k.lower() == "docker-content-digest")
        if reported != digest:
            raise RuntimeError("registry digest disagrees with retained bytes: " + repo)
        doc = json.loads(raw)
        images.append({"tag": registry + "/" + repo + ":" + tag,
            "immutable": registry + "/" + repo + "@" + digest, "file": filename,
            "platforms": [{"platform": m.get("platform"), "digest": m["digest"]}
                          for m in doc.get("manifests", [])]})

    sources = []
    for repo, tag, files in [
        ("dapr/dapr", "v1.18.7", ["go.mod"]),
        ("dapr/components-contrib", "v1.18.6", ["pubsub/rabbitmq/rabbitmq.go",
            "pubsub/rabbitmq/metadata.go", "common/component/kafka/consumer.go",
            "secretstores/hashicorp/vault/vault.go"])]:
        filename = repo.replace("/", "-") + "-" + tag + "-gitref.json"
        ref = json.loads(fetch("https://api.github.com/repos/" + repo + "/git/ref/tags/" + tag, filename)[0])
        obj = ref["object"]
        while obj["type"] == "tag":
            obj = json.loads(fetch(obj["url"], filename + ".tag.json")[0])["object"]
        if obj["type"] != "commit":
            raise RuntimeError("tag did not resolve to a commit")
        commit = obj["sha"]
        sources.append({"repository": repo, "tag": tag, "commit": commit})
        for name in files:
            fetch("https://raw.githubusercontent.com/" + repo + "/" + commit + "/" + name,
                  repo.replace("/", "-") + "-" + name.replace("/", "-"))
    fetch("https://dapr.github.io/helm-charts/dapr-1.18.7.tgz", "dapr-1.18.7.tgz")
    record = {"observedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "authority": "public candidate identity observation only; no runtime/broker execution or production selection",
        "images": images, "sources": sources, "httpReceipts": receipts,
        "collectorSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
    (args.output / "identities.json").write_text(json.dumps(record, indent=2) + "\n")
    print(json.dumps({"output": str(args.output), "images": len(images), "sourceFiles": len(receipts)}))


if __name__ == "__main__":
    main()
