#!/usr/bin/env node
// Standard-version postbump hook: keep Directory.Build.props
// PackageValidationBaselineVersion aligned with the previous published release.
//
// Lifecycle: standard-version runs this AFTER bumping package.json to the new version
// but BEFORE creating the release commit and tag. At this point the most recent v* git
// tag is the previous release. However that tag may correspond to a release that failed
// before reaching NuGet (e.g. a CI gate failure). Writing an unpublished version as the
// baseline causes PackageValidation to fail with NU1102 on the very next release attempt.
//
// This script resolves the issue by querying the NuGet v3 flat-container index for each
// candidate tag (newest first) and stopping at the first one that is actually published.
// If no candidate is published (brand-new package) it exits 0 without modifying the file.
//
// commit-all is enabled in .versionrc.json so the modified Directory.Build.props is
// included in standard-version's release commit.
//
// Failure semantics (fail-closed): network/git failures during a release halt the bump
// rather than ship a stale baseline. To run offline (e.g. local `npm run release` while
// disconnected), set QUERYSPEC_SKIP_BASELINE_NETWORK=true to downgrade NuGet failures to
// a warning. This flag is intentionally NOT honoured in CI — release.yml has no such env.

import { execSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { request } from 'node:https';

const PROPS = 'Directory.Build.props';
const RE = /(<PackageValidationBaselineVersion[^>]*>)([^<]+)(<\/PackageValidationBaselineVersion>)/;
const NUGET_INDEX = 'https://api.nuget.org/v3-flatcontainer/queryspec.core/index.json';

function fetchPublishedVersions() {
    return new Promise((resolve, reject) => {
        const req = request(NUGET_INDEX, (res) => {
            let body = '';
            res.on('data', (chunk) => body += chunk);
            res.on('end', () => {
                if (res.statusCode !== 200) {
                    return reject(new Error(`NuGet API returned HTTP ${res.statusCode}`));
                }
                try {
                    resolve(JSON.parse(body).versions);
                } catch (e) {
                    reject(e);
                }
            });
        });
        req.on('error', reject);
        req.end();
    });
}

let tags;
try {
    const raw = execSync('git tag -l --sort=-v:refname', { encoding: 'utf8' }).trim();
    tags = raw ? raw.split('\n').filter(Boolean) : [];
} catch (err) {
    console.error('postbump-baseline: git tag failed.');
    console.error(err.stderr?.toString() ?? err.message);
    console.error('Cannot determine candidate baselines without git history. Aborting release.');
    process.exit(1);
}

if (tags.length === 0) {
    console.log('postbump-baseline: no v* tags found; skipping baseline update.');
    process.exit(0);
}

const candidates = tags
    .filter(t => /^v\d+\.\d+\.\d+/.test(t))
    .map(t => t.slice(1));

if (candidates.length === 0) {
    console.log('postbump-baseline: no v* tags with semver format found; skipping baseline update.');
    process.exit(0);
}

let published;
try {
    published = new Set(await fetchPublishedVersions());
} catch (err) {
    console.error(`postbump-baseline: could not query NuGet — ${err.message}`);
    if (process.env.QUERYSPEC_SKIP_BASELINE_NETWORK === 'true') {
        console.warn('postbump-baseline: QUERYSPEC_SKIP_BASELINE_NETWORK=true; leaving Directory.Build.props unchanged.');
        console.warn('This flag must NOT be set in CI — a stale baseline will fail the release pack gate.');
        process.exit(0);
    }
    console.error('Aborting release: cannot verify the baseline points at a published version on NuGet.');
    console.error('If retrying after a NuGet outage, re-run `npm run release` once the API is reachable.');
    console.error('For offline local development only, set QUERYSPEC_SKIP_BASELINE_NETWORK=true.');
    process.exit(1);
}

const baseline = candidates.find(v => published.has(v));

if (!baseline) {
    console.log('postbump-baseline: no published version found among candidate tags; leaving Directory.Build.props unchanged.');
    process.exit(0);
}

if (candidates[0] !== baseline) {
    const skipped = candidates.slice(0, candidates.indexOf(baseline));
    console.log(`postbump-baseline: ${skipped.map(v => 'v' + v).join(', ')} not published on NuGet.org; falling back to v${baseline}`);
}

const content = readFileSync(PROPS, 'utf8');
const m = content.match(RE);
if (!m) {
    console.error(`postbump-baseline: PackageValidationBaselineVersion not found in ${PROPS}.`);
    process.exit(1);
}

const oldBaseline = m[2];
if (oldBaseline === baseline) {
    console.log(`postbump-baseline: baseline already ${baseline}, no change.`);
    process.exit(0);
}

const updated = content.replace(RE, `$1${baseline}$3`);
writeFileSync(PROPS, updated);
console.log(`postbump-baseline: ${PROPS} PackageValidationBaselineVersion ${oldBaseline} -> ${baseline}`);
