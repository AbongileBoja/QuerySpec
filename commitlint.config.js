/**
 * commitlint configuration.
 *
 * Body/footer line-length rules are enforced for humans (120 chars) but
 * disabled for dependabot, whose grouped-update commits include a markdown
 * changelog table with rows that legitimately exceed 120 chars per line.
 * Detection is via the `Signed-off-by: dependabot[bot]` trailer that
 * dependabot stamps on every commit it authors.
 */
module.exports = {
  extends: ['@commitlint/config-conventional'],
  ignores: [
    (commit) => /^Signed-off-by: dependabot\[bot\]/m.test(commit),
  ],
  rules: {
    'type-enum': [
      2,
      'always',
      [
        'feat',
        'fix',
        'docs',
        'style',
        'refactor',
        'perf',
        'test',
        'build',
        'ci',
        'chore',
        'revert',
      ],
    ],
    'type-case': [2, 'always', 'lower-case'],
    'type-empty': [2, 'never'],
    'scope-case': [2, 'always', 'lower-case'],
    'scope-enum': [
      2,
      'always',
      [
        'core',
        'efcore',
        'di',
        'caching',
        'resilience',
        'security',
        'auditing',
        'monitoring',
        'advanced',
        'benchmarks',
        'tests',
        'ci',
        'deps',
        'deps-dev',
        'docs',
        'release',
      ],
    ],
    'subject-empty': [2, 'never'],
    'subject-full-stop': [2, 'never', '.'],
    'subject-case': [2, 'never', ['start-case', 'pascal-case', 'upper-case']],
    'header-max-length': [2, 'always', 100],
    'body-max-line-length': [2, 'always', 120],
    'footer-max-line-length': [2, 'always', 120],
  },
};
