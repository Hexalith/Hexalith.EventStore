"""Prepare immutable public inputs, never extracted binaries or running Dapr state.

The hashes pin the original Dapr v1.18.4 release assets and their upstream
checksum records. Checksums are integrity records, not publisher signatures.
Only ``prepare`` has a transport; materialization always refuses bad caches.
"""

import argparse
import contextlib
import ctypes
import hashlib
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import uuid


RELEASE = "https://github.com/dapr/dapr/releases/download/v1.18.4/"
# archive bytes/hash, followed by the upstream checksum-record bytes/hash.
PINS = (
    ("daprd_linux_amd64.tar.gz", 63795845,
     "d679a47789cddacc8cd38d96d0da7a75514475146ca3da3e5b02bff1ad259dfb", 91,
     "c548bfdb129da03a2cc497360e963aa0f50ea64b08e4752c8cdb905637af5a48"),
    ("placement_linux_amd64.tar.gz", 14384564,
     "81e515f5ef14ee8534696edc614593d3cf500f5b02d054c44339b297ff4277b5", 95,
     "b8aadcd569ed06462c8dfabed51c4e7ccf78162a9fb50759860e88159c082c2d"),
    ("scheduler_linux_amd64.tar.gz", 18467814,
     "7a206972e0066da1508def6f18fbe92324a89b9cb50cc5c0b0c44f5397da7eff", 95,
     "4ca24aba2743ef52d684a2e61e9b87c4640d39d065b1ea1646926428976d1a11"),
)
MARKER = "verified-inputs.json"


class InputError(Exception):
    """A prerequisite refusal, never a successful qualification outcome."""


def receipt(pins):
    return (json.dumps({"schema": 1, "release": RELEASE, "pins": pins},
                       sort_keys=True, separators=(",", ":")) + "\n").encode()


def expected_files(pins):
    return {name: (size, digest)
            for archive, size, digest, check_size, check_digest in pins
            for name, size, digest in ((archive, size, digest),
                                       (archive + ".sha256", check_size, check_digest))}


def verify_checksum_record(name, digest, record):
    # The original release uses GNU sha256sum binary records. Bind the exact
    # marker, filename and digest; neither whitespace nor paths are normalized.
    if record != (digest + " *" + name + "\n").encode("ascii"):
        raise InputError("Upstream archive checksum disagreement: " + name)


def verify_bodies(bodies, pins):
    expected = expected_files(pins)
    if set(bodies) != set(expected):
        raise InputError("Missing or foreign runtime input")
    for name, (size, digest) in expected.items():
        body = bodies[name]
        if len(body) != size or hashlib.sha256(body).hexdigest() != digest:
            raise InputError("Pinned runtime input mismatch: " + name)
    for name, _, digest, _, _ in pins:
        verify_checksum_record(name, digest, bodies[name + ".sha256"])


@contextlib.contextmanager
def directory(path):
    """Open every path component without following links, including ancestors."""
    raw = os.fspath(path)
    parts = raw.split("/")
    if not raw.startswith("/") or any(p in (".", "..") for p in parts):
        raise InputError("Runtime input paths must be absolute without dot components")
    flags = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW
    fd = os.open("/", flags)
    try:
        for part in filter(None, parts):
            next_fd = os.open(part, flags, dir_fd=fd)
            os.close(fd)
            fd = next_fd
        yield fd
    finally:
        os.close(fd)


def owned(info, *, regular):
    correct_type = stat.S_ISREG(info.st_mode) if regular else stat.S_ISDIR(info.st_mode)
    if (not correct_type or info.st_uid != os.getuid() or info.st_mode & 0o022
            or (regular and info.st_nlink != 1)):
        raise InputError("Linked, writable or foreign runtime input refused")


def identity(info):
    # Reads/listings may update atime; identity and mutation fields must not move.
    return (info.st_dev, info.st_ino, info.st_mode, info.st_uid, info.st_gid,
            info.st_nlink, info.st_size, info.st_mtime_ns, info.st_ctime_ns)


def read_file(fd, name, size):
    descriptor = os.open(name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK, dir_fd=fd)
    try:
        before = os.fstat(descriptor)
        owned(before, regular=True)
        if before.st_size != size:
            raise InputError("Runtime input size mismatch: " + name)
        with os.fdopen(descriptor, "rb", closefd=False) as source:
            body = source.read(size + 1)
        after = os.fstat(descriptor)
        if identity(before) != identity(after) or len(body) != size:
            raise InputError("Runtime input changed during verification: " + name)
        return body
    finally:
        os.close(descriptor)


def read_inputs(path, pins, *, prepared):
    expected = expected_files(pins)
    marker = receipt(pins)
    with directory(path) as fd:
        before = os.fstat(fd)
        owned(before, regular=False)
        names = set(expected) | ({MARKER} if prepared else set())
        if set(os.listdir(fd)) != names:
            raise InputError("Incomplete or foreign runtime input directory")
        if prepared and read_file(fd, MARKER, len(marker)) != marker:
            raise InputError("Foreign runtime input receipt")
        bodies = {name: read_file(fd, name, size) for name, (size, _) in expected.items()}
        verify_bodies(bodies, pins)
        if identity(os.fstat(fd)) != identity(before):
            raise InputError("Runtime input directory changed during verification")
        # A replaced/renamed root must not authorize a different named directory.
        with directory(path) as current:
            if identity(os.fstat(current)) != identity(before):
                raise InputError("Runtime input directory replaced during verification")
        return bodies


def rename_new(parent, source, destination):
    """Linux atomic directory publication without replacing an existing path."""
    libc = ctypes.CDLL(None, use_errno=True)
    rename = libc.renameat2
    rename.argtypes = [ctypes.c_int, ctypes.c_char_p, ctypes.c_int, ctypes.c_char_p, ctypes.c_uint]
    rename.restype = ctypes.c_int
    if rename(parent, os.fsencode(source), parent, os.fsencode(destination), 1) != 0:
        error = ctypes.get_errno()
        raise OSError(error, os.strerror(error))


def publish(destination, produce, pins, *, prepared):
    path = Path(destination)
    # Validate the original lexical spelling before Path can normalize it.
    if not os.fspath(destination).startswith("/") or any(
            p in (".", "..") for p in os.fspath(destination).split("/")):
        raise InputError("Runtime destination must be an absolute path without dot components")
    with directory(path.parent) as parent:
        if path.name in os.listdir(parent):
            raise InputError("Runtime destination already exists")
        stage = ".published-runtime-" + uuid.uuid4().hex
        os.mkdir(stage, 0o700, dir_fd=parent)
        fd = os.open(stage, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=parent)
        try:
            bodies = produce()
            verify_bodies(bodies, pins)
            if prepared:
                bodies = {**bodies, MARKER: receipt(pins)}
            for name, body in bodies.items():
                output = os.open(name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW,
                                 0o400, dir_fd=fd)
                with os.fdopen(output, "wb") as target:
                    target.write(body)
                    target.flush()
                    os.fsync(target.fileno())
            os.fchmod(fd, 0o500 if prepared else 0o700)
            os.fsync(fd)
            rename_new(parent, stage, path.name)
            os.fsync(parent)
        finally:
            # Only our uniquely created staging directory is eligible for cleanup.
            if stage in os.listdir(parent):
                current = os.stat(stage, dir_fd=parent, follow_symlinks=False)
                if (current.st_dev, current.st_ino) == (os.fstat(fd).st_dev, os.fstat(fd).st_ino):
                    os.fchmod(fd, 0o700)
                    for name in os.listdir(fd):
                        os.unlink(name, dir_fd=fd)
                    os.rmdir(stage, dir_fd=parent)
            os.close(fd)


def fetch(url, size):
    # No retry and no enlarged original 90-second request budget. Redirects
    # remain HTTPS; the committed exact bytes, not a redirect host, authorize use.
    try:
        result = subprocess.run(
            ["curl", "--disable", "--fail", "--location", "--proto", "=https", "--proto-redir", "=https",
             "--max-time", "90", "--max-filesize", str(size), "--silent", "--show-error", url],
            capture_output=True, timeout=90, check=False)
    except subprocess.TimeoutExpired as error:
        raise InputError("Runtime input transfer exceeded 90 seconds") from error
    if result.returncode != 0:
        raise InputError("Runtime input transfer failed (curl exit " + str(result.returncode) + ")")
    return result.stdout


def prepare(destination, *, transport=fetch, pins=PINS):
    def download():
        return {name: transport(RELEASE + name, size)
                for name, (size, _) in expected_files(pins).items()}
    publish(destination, download, pins, prepared=True)


def materialize(cache, destination, *, pins=PINS):
    publish(destination, lambda: read_inputs(cache, pins, prepared=True), pins, prepared=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("operation", choices=("prepare", "materialize", "verify-local"))
    parser.add_argument("--destination")
    parser.add_argument("--cache")
    parser.add_argument("--directory")
    args = parser.parse_args()
    try:
        if args.operation == "prepare" and args.destination and not (args.cache or args.directory):
            prepare(args.destination)
        elif args.operation == "materialize" and args.cache and args.destination and not args.directory:
            materialize(args.cache, args.destination)
        elif args.operation == "verify-local" and args.directory and not (args.cache or args.destination):
            read_inputs(args.directory, PINS, prepared=False)
        else:
            parser.error("Invalid arguments for selected operation")
    except (InputError, OSError, UnicodeError, AttributeError) as error:
        # Do not emit curl stderr, arbitrary file contents or redirect URLs.
        print("Private runtime inputs refused: " + type(error).__name__ + ": " + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
