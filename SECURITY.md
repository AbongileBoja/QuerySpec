# Security Policy

## Supported Versions

Security fixes are issued for the versions below. Older majors do not receive backports.

| Version | Status              |
| ------- | ------------------- |
| 4.x     | Supported           |
| < 4.0   | End of life         |

## Reporting a Vulnerability

Please report suspected vulnerabilities privately. Do not open a public issue or pull request that describes the flaw.

- Preferred: [GitHub Private Vulnerability Reporting](https://github.com/AbongileBoja/QuerySpec/security/advisories/new). This routes the report into a private advisory thread that only the maintainers can read.
- Fallback: email `bojaabongile0@gmail.com` if the GitHub flow is unavailable for you.

Please include affected version(s), a minimal reproduction, the impact you observed, and any proposed mitigation.

## Triage SLA

- Acknowledgement of the report: within 72 hours.
- Initial assessment (severity, scope, affected versions): within 7 days.
- Fix or mitigation:
  - High and Critical: within 90 days.
  - Medium and Low: within 180 days.
- Coordinated disclosure window: 90 days from acknowledgement, or upon fix release, whichever comes first.

We will keep the reporter informed at each step and credit them in the advisory unless they request otherwise.

## Scope

In scope:

- `src/QuerySpec.Core`
- `src/QuerySpec.EFCore`
- `src/QuerySpec.DependencyInjection`
- `src/QuerySpec.Analyzers`

Out of scope (please do not file private reports against these; use a regular GitHub issue or the appropriate upstream channel):

- Sample applications under `samples/`
- Benchmark project under `benchmarks/`
- Test fixtures and test-only assemblies
- CI infrastructure under `.github/workflows/` (report via a GitHub Security Advisory if a finding is genuinely security-relevant)

## Disclosure Process

Reports are received privately, acknowledged within the SLA above, triaged for severity and affected versions, and patched on a private branch. A coordinated release publishes the fix together with a public GitHub Security Advisory and, where applicable, a CVE identifier. The advisory is the canonical record of what was fixed, which versions are affected, and how to upgrade.

---

Past advisories and currently open reports are tracked under [Security Advisories](https://github.com/AbongileBoja/QuerySpec/security/advisories).
