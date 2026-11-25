import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

import lintTitle from '@commitlint/lint';
import loadConfig from '@commitlint/load';

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

const invalidStarts = [
    ['uppercase', 'fix: Update status'],
    ['digit', 'fix: 2 Updates pending'],
    ['symbol', 'fix: [API] Update status'],
    ['digit in a breaking change', 'feat(api)!: 2 Changes'],
    ['space', 'fix:  update status'],
];

for (const [description, message, rule] of [
    ...invalidStarts.map(([description, message]) => [`a description starting with ${description}`, message, 'subject-empty']),
    ['a header over the limit', `${longestHeader}a`, 'header-max-length'],
    ['a body line over the limit', `fix: update status\n\n${longestBodyLine}b`, 'body-max-line-length'],
]) {
    test(`reports ${rule} for ${description}`, () => {
        const { status, output } = lint(message);
        assert.notEqual(status, 0, output);
        assert.ok(output.includes(`[${rule}]`), output);
    });
}

test('accepts a lowercase Unicode description on a scoped breaking change', () => {
    const { status, output } = lint('feat(api)!: écrire le nouveau format\n\nBREAKING CHANGE: replace the old format');
    assert.equal(status, 0, output);
});

test('applies the same lowercase policy when linting a pull request title', async () => {
    const config = await loadConfig({}, { cwd: fileURLToPath(root) });
    const options = {
        parserOpts: config.parserPreset?.parserOpts,
        ignores: config.ignores,
        defaultIgnores: false,
    };
    assert.equal((await lintTitle('feat(api)!: écrire le nouveau format', config.rules, options)).valid, true);
    for (const [, title] of invalidStarts) {
        const result = await lintTitle(title, config.rules, options);
        assert.equal(result.valid, false, title);
        assert.ok(result.errors.some(error => error.name === 'subject-empty'), title);
    }
});
