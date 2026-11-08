"""Owned loopback TCP forwarding for Docker Desktop's changing published ports.

This transports bytes only. It never terminates TLS or understands a broker
protocol. Every new connection reads the current disposable container endpoint.
"""
from __future__ import annotations

import argparse
from pathlib import Path
import select
import socket
import socketserver


class Forwarder(socketserver.BaseRequestHandler):
    def handle(self):
        try:
            target = int(self.server.target_file.read_text())
            with socket.create_connection(("127.0.0.1", target), timeout=5) as remote:
                remote.settimeout(None)
                peers = {self.request: remote, remote: self.request}
                while True:
                    readable, _, _ = select.select(list(peers), [], [], 30)
                    for source in readable:
                        data = source.recv(65536)
                        if not data:
                            return
                        peers[source].sendall(data)
        except (OSError, ValueError):
            return


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, required=True)
    parser.add_argument("--target-file", type=Path, required=True)
    args = parser.parse_args()
    with socketserver.ThreadingTCPServer(("127.0.0.1", args.port), Forwarder) as server:
        server.daemon_threads = True
        server.target_file = args.target_file
        server.serve_forever()


if __name__ == "__main__":
    main()
