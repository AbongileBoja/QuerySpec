#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

bold() { printf '\033[1m%s\033[0m\n' "$*"; }
dim()  { printf '\033[2m%s\033[0m\n' "$*"; }

bold "═══ QuerySpec dev container setup ═══"

bold "→ Fixing ownership on bind mounts and dotnet home"
for dir in "$HOME/.nuget" "$HOME/.nuget/packages" "$HOME/.dotnet" "$HOME/.dotnet/tools"; do
  if [[ -e "$dir" ]] && [[ "$(stat -c '%U' "$dir" 2>/dev/null || echo unknown)" != "$(id -un)" ]]; then
    sudo chown -R "$(id -u):$(id -g)" "$dir" 2>/dev/null || dim "  could not chown $dir"
  fi
done
mkdir -p "$HOME/.dotnet/tools" "$HOME/.nuget/packages"

bold "→ Toolchain"
dotnet --list-sdks | sed 's/^/  dotnet /'
printf '  node    %s\n' "$(node --version)"
printf '  npm     %s\n' "$(npm --version)"
printf '  gh      %s\n' "$(gh --version | head -n1 | cut -d' ' -f3)"

bold "→ Installing Node tooling (commitlint, husky, standard-version)"
npm install --no-audit --no-fund

bold "→ Installing global .NET tools"
DOTNET_TOOLS=(
  "dotnet-reportgenerator-globaltool"
  "dotnet-coverage"
  "dotnet-outdated-tool"
)
for tool in "${DOTNET_TOOLS[@]}"; do
  if ! dotnet tool list --global | grep -q "^${tool}\b"; then
    dotnet tool install --global "$tool" || dim "  warning: failed to install $tool"
  else
    dim "  $tool already installed"
  fi
done

TOOLS_LINE='export PATH="$PATH:$HOME/.dotnet/tools"'
for rc in "$HOME/.bashrc" "$HOME/.profile"; do
  if [[ -f "$rc" ]] && ! grep -qF "$TOOLS_LINE" "$rc"; then
    printf '\n# .NET global tools\n%s\n' "$TOOLS_LINE" >> "$rc"
  fi
done
export PATH="$PATH:$HOME/.dotnet/tools"

bold "→ Restoring NuGet packages"
dotnet restore QuerySpec.sln --verbosity minimal

bold "→ Generating ASP.NET Core HTTPS dev cert"
dotnet dev-certs https --check >/dev/null 2>&1 || dotnet dev-certs https >/dev/null 2>&1 || \
  dim "  HTTPS dev cert generation skipped (Linux trust store not used)"

bold "→ Configuring git"
git config --global core.autocrlf input
git config --global init.defaultBranch develop
if [[ -d .git/hooks ]] && [[ ! -f .git/hooks/commit-msg ]]; then
  dim "  husky should have installed commit-msg hook via npm install"
fi

cat <<'EOF'

═══ Setup complete ═══

Build & test:
  dotnet build  QuerySpec.sln -c Release
  dotnet test   QuerySpec.sln -c Release

Format & lint:
  dotnet format QuerySpec.sln
  dotnet format QuerySpec.sln --verify-no-changes --severity warn

Coverage (uses installed reportgenerator):
  dotnet test QuerySpec.sln --collect:"XPlat Code Coverage" --results-directory ./TestResults
  reportgenerator -reports:./TestResults/**/coverage.cobertura.xml -targetdir:./CoverageReport -reporttypes:Html

Benchmarks (BenchmarkDotNet, ~20 minutes):
  dotnet run -c Release --project benchmarks/QuerySpec.Benchmarks -- --filter "*"

Sample apps:
  dotnet run --project samples/QuerySpec.Samples.WebApi
  dotnet run --project samples/QuerySpec.Samples.Security
  dotnet run --project samples/QuerySpec.Samples.Resilience

Release (cuts a SemVer tag, generates CHANGELOG, prepares for push):
  npm run release                                 # auto from commits
  npm run release -- --release-as patch|minor|major
  git push --follow-tags origin develop

EOF
