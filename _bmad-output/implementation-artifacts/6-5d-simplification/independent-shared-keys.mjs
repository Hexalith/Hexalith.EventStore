import fs from 'node:fs';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';

const answers = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
assert.deepEqual(Object.keys(answers.sharedKeys).sort(), ['command-execution-scope']);
const row = answers.sharedKeys['command-execution-scope'];
assert.equal(row.prefix, 'command-execution-scope:');
assert.equal(row.fields.length, 2);
const material = Buffer.concat(row.fields.map(([type, value]) => {
    assert.equal(type, 'U');
    const bytes = Buffer.from(value, 'utf8');
    assert.ok(bytes.length > 0 && bytes.length <= 1024);
    const length = Buffer.alloc(4);
    length.writeUInt32BE(bytes.length);
    return Buffer.concat([length, bytes]);
}));
assert.equal(material.toString('hex'), row.materialHex);
assert.equal(row.prefix + crypto.createHash('sha256').update(material).digest('hex'), row.value);
console.log('independent shared-key construction: 1 unframed command scope key');
