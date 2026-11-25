import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const root = new URL('../../', import.meta.url);
const cli = fileURLToPath(new URL('node_modules/@commitlint/cli/cli.js', root));

function lint(message) {
    const result = spawnSync(process.execPath, [cli, '--verbose'], {
        cwd: root,
        encoding: 'utf8',
        input: `${message}\n`,
        timeout: 15_000,
    });
    assert.ifError(result.error);
    return { status: result.status, output: result.stdout + result.stderr };
}

const prefix = 'fix(server): ';
const longestHeader = prefix + 'a'.repeat(200 - prefix.length);
const longestBodyLine = 'b'.repeat(200);

test('accepts a conventional message at the header and body line limits', () => {
    const { status, output } = lint(`${longestHeader}\n\n${longestBodyLine}`);
    assert.equal(status, 0, output);
});

for (const [message, rule] of [
    ['fix: UPDATE STATUS', 'subject-case'],
    [`${longestHeader}a`, 'header-max-length'],
    [`fix: update status\n\n${longestBodyLine}b`, 'body-max-line-length'],
]) {
    test(`rejects a message that violates ${rule}`, () => {
        const { status, output } = lint(message);
        assert.notEqual(status, 0, output);
        assert.ok(output.includes(`[${rule}]`), output);
    });
}
