#!/usr/bin/env node
// Standard-version postbump hook: auto-regenerate CompatibilitySuppressions.xml
// and validate that only SemVer-legal suppressions appear for the bump type.
//
// Lifecycle: standard-version runs this AFTER bumping package.json to the new version
// but BEFORE creating the release commit and tag. At this point:
//   - package.json already has the NEW version
//   - The most recent v* git tag is the PREVIOUS release
//
// What this script does:
//   1. Determines bump type (patch/minor/major) by comparing old (tag) and new versions.
//   2. Runs `dotnet pack` with ApiCompatGenerateSuppressionFile=true to regenerate
//      CompatibilitySuppressions.xml for each packable src project.
//   3. Validates the suppression contents per SemVer rules:
//      - PATCH/MINOR: only CP0001 (type added), CP0007 (member added) with
//        IsBaselineSuppression=true, or inter-TFM diffs (no IsBaselineSuppression).
//        Breaking codes (CP0002/CP0006/CP0008/CP0009/CP0011/CP0031) with
//        IsBaselineSuppression=true abort the release.
//      - MAJOR: all suppression types are allowed (intentional breaking changes).
//   4. git adds the modified files so standard-version's commit includes them.
//   5. Prints a clear summary of what was regenerated.
//
// commit-all is enabled in .versionrc.json so the git-added files ride into the
// standard-version release commit.

import { execSync, spawnSync } from 'node:child_process';
import { readFileSync, existsSync } from 'node:fs';
import { parseArgs } from 'node:util';

const BREAKING_CODES = new Set(['CP0002', 'CP0006', 'CP0008', 'CP0009', 'CP0011', 'CP0031']);
const ADDITIVE_CODES = new Set(['CP0001', 'CP0007']);

const PACKABLE_PROJECTS = [
    'src/QuerySpec.Core/QuerySpec.Core.csproj',
    'src/QuerySpec.EFCore/QuerySpec.EFCore.csproj',
    'src/QuerySpec.DependencyInjection/QuerySpec.DependencyInjection.csproj',
];

const SUPPRESSION_FILES = [
    'src/QuerySpec.Core/CompatibilitySuppressions.xml',
    'src/QuerySpec.EFCore/CompatibilitySuppressions.xml',
    'src/QuerySpec.DependencyInjection/CompatibilitySuppressions.xml',
];

function parseSemVer(v) {
    const m = v.match(/^(\d+)\.(\d+)\.(\d+)/);
    if (!m) return null;
    return { major: +m[1], minor: +m[2], patch: +m[3] };
}

function bumpType(oldVer, newVer) {
    const o = parseSemVer(oldVer);
    const n = parseSemVer(newVer);
    if (!o || !n) return 'unknown';
    if (n.major > o.major) return 'major';
    if (n.minor > o.minor) return 'minor';
    return 'patch';
}

function getNewVersion() {
    try {
        const pkg = JSON.parse(readFileSync('package.json', 'utf8'));
        return pkg.version;
    } catch {
        return null;
    }
}

function getOldVersion() {
    try {
        const raw = execSync('git tag -l --sort=-v:refname', { encoding: 'utf8' }).trim();
        const tags = raw ? raw.split('\n').filter(Boolean) : [];
        const semverTags = tags.filter(t => /^v\d+\.\d+\.\d+/.test(t));
        if (semverTags.length === 0) return null;
        return semverTags[0].slice(1);
    } catch {
        return null;
    }
}

function runPack(version) {
    console.log(`postbump-apicompat: running dotnet pack to regenerate suppressions for v${version}...`);
    const args = [
        'pack', 'QuerySpec.sln',
        '--configuration', 'Release',
        `-p:Version=${version}`,
        '-p:ApiCompatGenerateSuppressionFile=true',
        '-p:ApiCompatGenerateSuppressionFileForChangedAssemblies=true',
        '--output', '/tmp/apicompat-regen',
        '--nologo',
    ];
    const result = spawnSync('dotnet', args, {
        encoding: 'utf8',
        stdio: ['ignore', 'pipe', 'pipe'],
    });

    if (result.status !== 0) {
        console.error('postbump-apicompat: dotnet pack failed. Output:');
        console.error(result.stdout || '');
        console.error(result.stderr || '');
        process.exit(1);
    }
}

function parseSuppressions(xmlContent) {
    const entries = [];
    const suppressionRe = /<Suppression>([\s\S]*?)<\/Suppression>/g;
    let match;
    while ((match = suppressionRe.exec(xmlContent)) !== null) {
        const block = match[1];
        const diagId = (block.match(/<DiagnosticId>([^<]+)<\/DiagnosticId>/) || [])[1] || '';
        const target = (block.match(/<Target>([^<]+)<\/Target>/) || [])[1] || '';
        const left = (block.match(/<Left>([^<]+)<\/Left>/) || [])[1] || '';
        const right = (block.match(/<Right>([^<]+)<\/Right>/) || [])[1] || '';
        const isBaseline = /<IsBaselineSuppression>true<\/IsBaselineSuppression>/.test(block);
        entries.push({ diagId, target, left, right, isBaseline });
    }
    return entries;
}

function validateSuppressions(filePath, entries, bump, version) {
    if (bump === 'major') {
        return { valid: true, additive: [], breaking: entries.filter(e => BREAKING_CODES.has(e.diagId) && e.isBaseline) };
    }

    const violations = [];
    for (const entry of entries) {
        if (!entry.isBaseline) continue;
        if (BREAKING_CODES.has(entry.diagId)) {
            violations.push(entry);
        }
    }

    if (violations.length > 0) {
        console.error('');
        console.error('postbump-apicompat: ABORT — breaking-change suppressions detected for a non-major bump.');
        console.error(`  Version bump type: ${bump} (${version})`);
        console.error(`  File: ${filePath}`);
        console.error('');
        console.error('  Breaking entries (IsBaselineSuppression=true with breaking diagnostic code):');
        for (const v of violations) {
            console.error(`    ${v.diagId}  ${v.target}`);
            console.error(`      left:  ${v.left}`);
            console.error(`      right: ${v.right}`);
        }
        console.error('');
        console.error('  These diagnostics indicate you removed or changed public API surface that');
        console.error('  existing consumers depend on. Options:');
        console.error('    1. Revert the breaking change and keep the minor/patch version.');
        console.error(`    2. Cut a major release instead of a ${bump} bump.`);
        console.error('       Run: npm run release -- --release-as major');
        console.error('');
        return { valid: false, additive: [], breaking: violations };
    }

    const additive = entries.filter(e => e.isBaseline && (ADDITIVE_CODES.has(e.diagId)));
    return { valid: true, additive, breaking: [] };
}

function gitAdd(files) {
    const existing = files.filter(f => existsSync(f));
    if (existing.length === 0) return;
    try {
        execSync(`git add ${existing.map(f => `"${f}"`).join(' ')}`, { stdio: 'inherit' });
    } catch (err) {
        console.error(`postbump-apicompat: git add failed: ${err.message}`);
        process.exit(1);
    }
}

const newVersion = getNewVersion();
if (!newVersion) {
    console.error('postbump-apicompat: could not read version from package.json; skipping.');
    process.exit(0);
}

const oldVersion = getOldVersion();
if (!oldVersion) {
    console.log('postbump-apicompat: no previous v* tag found; skipping ApiCompat suppression regeneration.');
    process.exit(0);
}

const bump = bumpType(oldVersion, newVersion);
console.log(`postbump-apicompat: bump type is ${bump} (${oldVersion} -> ${newVersion})`);

runPack(newVersion);

let totalAdditive = 0;
let totalBreaking = 0;
let anyInvalid = false;
const summary = [];

for (const suppFile of SUPPRESSION_FILES) {
    if (!existsSync(suppFile)) {
        summary.push(`  ${suppFile}: file not found (skipped)`);
        continue;
    }

    const xml = readFileSync(suppFile, 'utf8');
    const entries = parseSuppressions(xml);
    const result = validateSuppressions(suppFile, entries, bump, newVersion);

    if (!result.valid) {
        anyInvalid = true;
        continue;
    }

    totalAdditive += result.additive.length;
    totalBreaking += result.breaking.length;

    const interTfm = entries.filter(e => !e.isBaseline);
    const parts = [];
    if (result.additive.length > 0) parts.push(`${result.additive.length} additive (CP0001/CP0007)`);
    if (result.breaking.length > 0) parts.push(`${result.breaking.length} breaking`);
    if (interTfm.length > 0) parts.push(`${interTfm.length} inter-TFM`);
    const desc = parts.length > 0 ? parts.join(', ') : 'empty';
    summary.push(`  ${suppFile}: ${desc}`);
}

if (anyInvalid) {
    process.exit(1);
}

gitAdd(SUPPRESSION_FILES);

console.log('');
console.log(`postbump-apicompat: regenerated CompatibilitySuppressions.xml (${bump} bump, v${newVersion})`);
for (const line of summary) {
    console.log(line);
}
if (bump === 'major' && totalBreaking > 0) {
    console.log('');
    console.log(`  Major bump: ${totalBreaking} breaking suppression(s) recorded above. Review before pushing.`);
}
console.log('');
