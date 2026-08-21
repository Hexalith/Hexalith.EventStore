"""Compatibility facade for the trusted Story 3.14 v3 live handler.

The command-line verifier performs schema/version/digest dispatch before loading
this implementation. Direct imports used by focused tests keep their historical
API through this facade; retained packet modules are never imported.
"""

try:
    from .release_evidence_handlers.v3 import *  # noqa: F403
except ImportError:
    from release_evidence_handlers.v3 import *  # noqa: F403
