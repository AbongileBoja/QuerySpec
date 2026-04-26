#!/usr/bin/env node
// Standard-version postbump hook: keep Directory.Build.props
// PackageValidationBaselineVersion aligned with the previous published release.
//
// Lifecycle: standard-version runs this AFTER bumping package.json to the new version
// but BEFORE creating the release commit and tag. At this point `git describe --tags
// --abbrev=0 --match=v*` returns the most recent EXISTING tag, which is the previous
// release on nuget.org — exactly what the next build should validate against.
//
// commit-all is enabled in .versionrc.json so the modified Directory.Build.props is
// included in standard-version's release commit.

import { execSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';

const PROPS = 'Directory.Build.props';
const RE = /(<PackageValidationBaselineVersion[^>]*>)([^<]+)(<\/PackageValidationBaselineVersion>)/;

let prevTag;
try {
    prevTag = execSync('git describe --tags --abbrev=0 --match=v*', { encoding: 'utf8' }).trim();
} catch (err) {
    console.error('postbump-baseline: no prior v* tag found; skipping baseline update.');
    console.error(err.stderr?.toString() ?? err.message);
    process.exit(0);
}

if (!/^v\d+\.\d+\.\d+/.test(prevTag)) {
    console.error(`postbump-baseline: unexpected tag format '${prevTag}'; skipping baseline update.`);
    process.exit(0);
}
const prevVersion = prevTag.slice(1);

const content = readFileSync(PROPS, 'utf8');
const m = content.match(RE);
if (!m) {
    console.error(`postbump-baseline: PackageValidationBaselineVersion not found in ${PROPS}.`);
    process.exit(1);
}

const oldBaseline = m[2];
if (oldBaseline === prevVersion) {
    console.log(`postbump-baseline: baseline already ${prevVersion}, no change.`);
    process.exit(0);
}

const updated = content.replace(RE, `$1${prevVersion}$3`);
writeFileSync(PROPS, updated);
console.log(`postbump-baseline: ${PROPS} PackageValidationBaselineVersion ${oldBaseline} -> ${prevVersion}`);
