#!/usr/bin/env python3
"""Archive every exact source selected by the final pinned PE/PDB audit."""
import json
from pathlib import Path
import shutil

packet = Path(__file__).resolve().parent
out = packet / 'bound-source'
index = []
for assembly in json.loads((packet / 'source-binding-final/all-pdb-documents.json').read_text()):
    for doc in assembly['documents']:
        digest = doc['sha256'].lower()
        source = Path(doc['path']) if doc['selectedFrom'] == 'disk' else packet / 'source-binding-final/embedded-sources' / (doc['sha256'] + '.cs')
        destination = out / (digest + '.source')
        if not destination.exists():
            shutil.copyfile(source, destination)
        index.append({'pe': assembly['pe'], 'path': doc['path'], 'sha256': digest, 'generated': doc['generated'], 'archive': str(destination.relative_to(packet))})
(packet / 'bound-source-index-final.json').write_text(json.dumps(index, indent=2) + '\n')
print(json.dumps({'document_copies': len(index), 'unique_qualified_source_hashes': len({r['sha256'] for r in index})}))
