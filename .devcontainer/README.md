# QuerySpec dev container

Reproducible development environment for QuerySpec. Works in:

- **VS Code Dev Containers** locally (Docker Desktop / Rancher Desktop / Podman)
- **GitHub Codespaces** in the browser or VS Code

Click **Reopen in Container** when VS Code prompts you, or hit
`F1 → Dev Containers: Rebuild Container` to refresh after `devcontainer.json`
changes.

## What's inside

| Tool                     | Why it's here                                                  |
|--------------------------|----------------------------------------------------------------|
| .NET SDK 8.0, 9.0, 10.0  | All three target frameworks built and tested by `QuerySpec.sln`|
| Node.js 20 LTS           | Husky, commitlint, `standard-version` (`npm run release`)      |
| GitHub CLI (`gh`)        | Used in `RELEASE.md` workflows + PR review                     |
| `reportgenerator`        | Local coverage report HTML generation                          |
| `dotnet-coverage`        | XPlat code coverage collection                                 |
| `dotnet-outdated-tool`   | `dotnet outdated` to spot stale `PackageReference`s            |
| ASP.NET Core HTTPS cert  | Generated for the sample WebApi project                        |

## Persistent volumes

Two named Docker volumes survive container rebuilds, so a fresh container is
fast to set up:

- `queryspec-nuget-cache` → `~/.nuget/packages`
- `queryspec-dotnet-tools` → `~/.dotnet/tools`

To wipe them (e.g. to test a clean restore):

```bash
docker volume rm queryspec-nuget-cache queryspec-dotnet-tools
```

## VS Code customisations

Wired up to match the repo's conventions:

- C# Dev Kit + OmniSharp for IntelliSense, debugging, test discovery
- EditorConfig + format-on-save + organize-imports-on-save
- Conventional Commits extension pre-populated with the project's scope list
  (`core`, `efcore`, `di`, `caching`, …) — matches `.commitlintrc.json`
- GitLens, GitHub Actions, GitHub PRs, YAML, TOML, Markdown lint, code spell checker

## Common tasks (cheatsheet)

```bash
dotnet build QuerySpec.sln -c Release
dotnet test  QuerySpec.sln -c Release
dotnet format QuerySpec.sln

npm run release -- --release-as patch
git push --follow-tags origin develop

dotnet run --project samples/QuerySpec.Samples.WebApi
```

The `post-create.sh` script prints the same list at the end of container
initialisation.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Husky hook didn't run on commit | `rm -rf node_modules && npm install` (re-runs `prepare`) |
| `dotnet test` can't find SDK 8/9 | Container build step failed; rebuild via `Dev Containers: Rebuild Without Cache` |
| Coverage report missing classes | Ensure `coverlet.collector` is referenced in every test csproj |
| HTTPS dev cert not trusted in browser | On Linux there's no system trust store; generate the cert and accept the warning, or run sample on HTTP |
