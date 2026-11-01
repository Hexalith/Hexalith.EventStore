#!/usr/bin/env node
// Read-only finite maintenance qualification. Use the installed, lockfile-pinned
// native analyzer and core Git/version helpers; never run release lifecycle hooks.
import { execFileSync, spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';
import { isDeepStrictEqual } from 'node:util';
import { cosmiconfig } from 'cosmiconfig';
import semver from 'semver';
import { analyzeCommits } from '@semantic-release/commit-analyzer';
import getCommits from 'semantic-release/lib/get-commits.js';
import getNextVersion from 'semantic-release/lib/get-next-version.js';
import getLastRelease from 'semantic-release/lib/get-last-release.js';
import getBranchTags from 'semantic-release/lib/branches/get-tags.js';
import { release as normalizeRelease } from 'semantic-release/lib/branches/normalize.js';

const maintenance = 'release/parties-sdk-3.130.2';
const baseTag = 'v3.130.1';
const baseHead = '1de42b706f52af0da6513f0b7663002606d17156';
const expectedBranches = [{ name: maintenance, channel: false }, { name: 'main', channel: false }];
const git = (...args) => execFileSync('git', args, { encoding: 'utf8' }).trim();
const fail = (message) => { throw new Error(message); };
const logger = { log() {}, error() {}, success() {} };
const hasRef = (ref) => spawnSync('git', ['show-ref', '--verify', '--quiet', ref]).status === 0;

export async function qualifyCandidate(env = process.env) {
  const repository = env.REPOSITORY ?? env.GITHUB_REPOSITORY;
  const ref = env.DISPATCH_REF ?? env.GITHUB_REF;
  const source = env.DISPATCH_SHA ?? env.GITHUB_SHA;
  if (repository !== 'Hexalith/Hexalith.EventStore') fail('Unapproved release repository.');
  if (ref !== `refs/heads/${maintenance}`) fail('Only the exact maintenance ref can qualify a finite candidate.');
  if (!/^[0-9a-f]{40}$/.test(source ?? '') || git('rev-parse', 'HEAD') !== source) fail('Wrong exact candidate source.');
  if (git('rev-parse', '--is-shallow-repository') !== 'false') fail('Complete committed history is required.');
  const configBytes = readFileSync('.releaserc.json', 'utf8');
  if (git('show', `${source}:.releaserc.json`) !== configBytes.trim()) fail('Release configuration differs from committed source.');
  const packageConfig = JSON.parse(readFileSync('package.json', 'utf8'));
  const alternateConfigs = ['.releaserc', '.releaserc.yaml', '.releaserc.yml', '.releaserc.js', '.releaserc.cjs', '.releaserc.mjs',
    '.releaserc.ts', '.releaserc.cts', '.releaserc.mts', 'release.config.js', 'release.config.cjs', 'release.config.mjs', 'release.config.ts', 'release.config.cts', 'release.config.mts'];
  if (packageConfig.release !== undefined || alternateConfigs.some((path) => existsSync(path)))
    fail('Unsupported competing release configuration.');
  const config = JSON.parse(configBytes);
  if (JSON.stringify(config.branches) !== JSON.stringify(expectedBranches) || config.tagFormat !== 'v${version}')
    fail('Unsupported release branch or tag configuration.');
  const pluginNames = config.plugins?.map((plugin) => Array.isArray(plugin) ? plugin[0] : plugin);
  if (JSON.stringify(pluginNames) !== JSON.stringify(['@semantic-release/commit-analyzer', '@semantic-release/release-notes-generator', '@semantic-release/exec', '@semantic-release/github'])
      || Object.keys(config).some((key) => !['branches', 'tagFormat', 'plugins'].includes(key))
      || Object.keys(config.plugins[2][1]).some((key) => !['verifyReleaseCmd', 'prepareCmd', 'publishCmd'].includes(key)))
    fail('Unsupported release/analyzer configuration.');
  if (config.extends || config.analyzeCommits || config.preset || config.parserOpts || config.releaseRules || config.plugins?.[0] !== '@semantic-release/commit-analyzer'
      || config.plugins.filter((plugin) => (Array.isArray(plugin) ? plugin[0] : plugin) === '@semantic-release/commit-analyzer').length !== 1)
    fail('Unsupported analyzer configuration.');
  const lock = JSON.parse(readFileSync(new URL('../package-lock.json', import.meta.url), 'utf8'));
  for (const [name, version] of [['semantic-release', '25.0.9'], ['@semantic-release/commit-analyzer', '13.0.1'], ['cosmiconfig', '9.0.2']]) {
    const installed = JSON.parse(readFileSync(new URL(`../node_modules/${name}/package.json`, import.meta.url), 'utf8'));
    if (installed.version !== version || lock.packages[`node_modules/${name}`]?.version !== version)
      fail('Native analyzer/core identity differs from the reviewed lockfile.');
  }
  // Resolve exactly as the pinned native core does, without loading release
  // plugins or invoking any credential/publication lifecycle hook. Bind both
  // the effective JSON and its origin; cosmiconfig metadata can redirect search.
  const effective = await cosmiconfig('release').search(process.cwd());
  if (!effective || effective.filepath !== resolve('.releaserc.json') || !isDeepStrictEqual(effective.config, config))
    fail('Native effective release configuration differs from committed .releaserc.json.');
  const configuration = { path: '.releaserc.json', sha256: createHash('sha256').update(JSON.stringify(effective.config)).digest('hex') };
  const native = { semanticRelease: '25.0.9', commitAnalyzer: '13.0.1', cosmiconfig: '9.0.2' };

  // Work only with already-fetched refs and pure native tag/normalization
  // helpers. Never call getBranches/fetch/fetchNotes or mutate user refs.
  const fetchedBranches = config.branches.map((branch) => {
    const refs = [`refs/remotes/origin/${branch.name}`, `refs/heads/${branch.name}`];
    const ref = refs.find(hasRef);
    if (!ref) fail(`Missing already-fetched configured branch: ${branch.name}.`);
    return { ...branch, name: ref };
  });
  const selectedHead = git('rev-parse', fetchedBranches[0].name);
  if (selectedHead !== source) fail('Fetched maintenance branch differs from exact candidate source.');
  const tagged = await getBranchTags({ cwd: process.cwd(), env, options: config }, fetchedBranches);
  const branches = normalizeRelease({ release: tagged.map((branch, index) => ({ ...branch, name: config.branches[index].name })) });
  const branch = branches.find((entry) => entry.name === maintenance);
  if (!branch || branch.type !== 'release') fail('Unsupported native maintenance branch type.');
  const lastRelease = getLastRelease({ branch, options: config });
  const resolvedBase = git('rev-parse', `${baseTag}^{commit}`);
  if (resolvedBase !== baseHead || lastRelease.gitTag !== baseTag || lastRelease.version !== '3.130.1')
    fail('Wrong latest reachable published base source.');
  git('merge-base', '--is-ancestor', resolvedBase, source);
  const context = { cwd: process.cwd(), env, logger, options: config,
    lastRelease: { ...lastRelease, gitHead: resolvedBase }, nextRelease: { gitHead: source, channel: branch.channel }, branch };
  const commits = await getCommits(context);
  const type = await analyzeCommits({}, { ...context, commits });
  if (type !== 'patch') fail(`The real candidate must be patch 3.130.2; analyzer returned ${type ?? 'no release'}.`);
  context.nextRelease.type = type;
  const version = getNextVersion(context);
  if (version !== '3.130.2') fail('The native core candidate is not 3.130.2.');
  if (!branch.accept.includes(type) || !semver.satisfies(version, branch.range))
    fail(`Candidate is outside native branch type/range: ${branch.range}.`);
  if (hasRef(`refs/tags/v${version}`)) fail('The candidate tag already exists in the global fetched tag inventory.');
  return { source, base: resolvedBase, type, version, branch: maintenance, range: branch.range, configuration, native };
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  try { process.stdout.write(`${JSON.stringify(await qualifyCandidate())}\n`); }
  catch (error) { process.stderr.write(`[release-candidate] ${error.message}\n`); process.exitCode = 1; }
}
