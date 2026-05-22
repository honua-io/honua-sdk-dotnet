# Contributing to the Honua .NET SDK

Thanks for considering a contribution. This repo owns reusable .NET SDK
contracts, service clients, protocol adapters, serialization formats, and tests.
The repo boundary, what belongs here, and what doesn't, is documented in
[AGENTS.md](AGENTS.md) — please read it before opening a pull request.

## Quick start for contributors

1. Install [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later.
2. Clone the repo and configure the GitHub Packages source — see
   [INSTALL.md](INSTALL.md#install-from-github-packages-pre-release).
3. Build the solution:

   ```bash
   dotnet build Honua.Sdk.sln --configuration Release /p:TreatWarningsAsErrors=true
   ```

4. Run the tests:

   ```bash
   dotnet test Honua.Sdk.sln --configuration Release --no-build
   ```

   Integration tests under `tests/Honua.Sdk.IntegrationTests` and
   `tests/Honua.Sdk.ProtocolIntegration.Tests` are gated on environment
   variables and skipped by default; see
   [docs/staging-integration.md](docs/staging-integration.md) and
   [docs/protocol-integration-tests.md](docs/protocol-integration-tests.md).

## House rules

- The solution builds clean under `TreatWarningsAsErrors`. Don't introduce
  warnings; suppress narrowly (with a comment) if absolutely necessary.
- Public APIs use `cancellationToken` (not `ct`); see
  [docs/client-behavior.md](docs/client-behavior.md) for the broader
  cross-package conventions.
- New exception types derive from
  [`Honua.Sdk.Abstractions.HonuaException`](src/Honua.Sdk.Abstractions/HonuaException.cs).
- New options classes follow the existing snapshot pattern: `BaseAddress`
  required, `Timeout` validated, `MaxRetryAttempts` throws on out-of-range.
- Use NetTopologySuite + ProjNet for geometry / CRS work rather than rolling
  your own — see [docs/geometry-analysis.md](docs/geometry-analysis.md).
- Don't ship UI, native, MAUI/WPF/Blazor controls, or display-layer code in
  this repo. The boundary is enforced in [AGENTS.md](AGENTS.md).

## Internal / contributor documentation

Backlog cadence, capability backlog, contract harmonization, and cross-repo
sequencing live under [docs/internal/](docs/internal/README.md). Consumer
documentation lives in [docs/](docs/README.md).

## Pull-request checklist

Before opening a PR, verify:

- [ ] `dotnet build Honua.Sdk.sln --configuration Release /p:TreatWarningsAsErrors=true` is clean — zero warnings, zero errors.
- [ ] `dotnet test Honua.Sdk.sln --configuration Release --no-build` is green; integration tests under `tests/Honua.Sdk.IntegrationTests` / `tests/Honua.Sdk.ProtocolIntegration.Tests` may stay skipped (environment-gated) unless your change touches their fixtures.
- [ ] You added or updated XML doc comments on any new public type or member. Missing-doc warnings fail the build under `TreatWarningsAsErrors`.
- [ ] You added unit tests for new behavior. Code-coverage floors (80% line / 70% branch, see `.github/workflows/ci.yml`) are enforced solution-wide; new packages should meet or exceed those numbers.
- [ ] Public-API surface changes pass `scripts/validate-api-compat.sh` (the CI `api-compat` job runs this against the prior `dotnet-sdk-v*` tag).
- [ ] The change does not break the `Honua.Sdk.Abstractions` provider-neutral contracts unless the change is intentional and version-gated; see [docs/feature-edits.md](docs/feature-edits.md).
- [ ] You did not introduce a default `BaseAddress`, an unguarded `Address` (string), or a clamping `MaxRetryAttempts` setter; the conventions are documented in [docs/troubleshooting.md](docs/troubleshooting.md).
- [ ] If you added a new SDK package, you added a `src/Honua.Sdk.<X>/README.md`, a `tests/Honua.Sdk.<X>.Tests/` project, a per-package CI job in `.github/workflows/ci.yml`, an `<None Include="README.md" Pack="true" />` line in the csproj for nuget.org rendering, and a row in `INSTALL.md` + `README.md` + `docs/architecture.md`.

## Style and conventions

- Async methods: `*Async` suffix, `CancellationToken cancellationToken = default` parameter (FDG / CA1068).
- Options: `BaseAddress` (`Uri?`, required, no localhost default), `Timeout` (`TimeSpan`, validated 10ms <= T < 24h), `EnableRetry` (default true), `MaxRetryAttempts` (default 3, throws outside `[2, 5]`).
- Exceptions: every SDK exception derives from `Honua.Sdk.Abstractions.HonuaException`; configuration-time failures use `HonuaConfigurationException`; runtime protocol failures use the protocol-specific sealed type.
- Geometry / CRS: use NetTopologySuite + ProjNet via `Honua.Sdk.Geometry`, not custom math.
- Don't ship UI, native, MAUI/WPF/Blazor controls, or display-layer code; see [AGENTS.md](AGENTS.md) for the repo boundary.

## Reporting an issue

Open a [GitHub issue](https://github.com/honua-io/honua-sdk-dotnet/issues)
with the package name(s) and version, .NET runtime version, Honua server
version (`adminClient.GetCapabilitiesAsync()` output is ideal), the call you're
making, and the full exception (type + message + stack). See
[docs/troubleshooting.md](docs/troubleshooting.md) for known failure modes.

## License

By contributing you agree that your contributions will be licensed under the
[Apache 2.0 License](LICENSE).
