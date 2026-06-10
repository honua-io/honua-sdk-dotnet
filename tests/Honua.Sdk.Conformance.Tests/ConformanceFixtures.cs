namespace Honua.Sdk.Conformance.Tests;

/// <summary>
/// Locates and loads the shared <c>geospatial-grpc</c> conformance fixtures
/// pulled by <c>conformance/fetch-fixtures.sh</c>. The fixtures are never copied
/// into this repo; CI fetches the pinned release asset and points the suite at
/// the extracted directory via <c>HONUA_CONFORMANCE_FIXTURES_DIR</c>.
/// </summary>
public static class ConformanceFixtures
{
    /// <summary>Environment variable naming the extracted fixtures directory.</summary>
    public const string FixturesDirEnv = "HONUA_CONFORMANCE_FIXTURES_DIR";

    /// <summary>Pinned fixture version this suite expects (see conformance/FIXTURE_VERSION).</summary>
    public const string PinnedVersion = "0.1.0-alpha.1";

    /// <summary>
    /// Resolves the directory that contains <c>fixtures/</c>, <c>golden/</c>, and
    /// <c>VERSION</c>. Returns <see langword="null"/> when the fixtures have not
    /// been fetched (so schema tests can skip with a clear reason instead of
    /// failing). When the directory is set but malformed, throws so a broken CI
    /// wiring is loud rather than silently green.
    /// </summary>
    public static string? ResolveRoot()
    {
        var dir = Environment.GetEnvironmentVariable(FixturesDirEnv);
        if (string.IsNullOrWhiteSpace(dir))
        {
            return null;
        }

        dir = dir.Trim();
        if (!Directory.Exists(dir))
        {
            throw new DirectoryNotFoundException(
                $"{FixturesDirEnv}={dir} does not exist. Run conformance/fetch-fixtures.sh --version {PinnedVersion}.");
        }

        var fixtures = Path.Combine(dir, "fixtures");
        if (!Directory.Exists(fixtures))
        {
            throw new DirectoryNotFoundException(
                $"{FixturesDirEnv}={dir} has no fixtures/ subdirectory; expected an extracted conformance-fixtures-* bundle.");
        }

        var versionFile = Path.Combine(dir, "VERSION");
        if (File.Exists(versionFile))
        {
            var version = File.ReadAllText(versionFile).Trim();
            if (!string.Equals(version, PinnedVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Fixture VERSION ({version}) does not match the pinned conformance version ({PinnedVersion}). " +
                    "Update conformance/FIXTURE_VERSION and Directory.Build.props together.");
            }
        }

        return dir;
    }

    /// <summary>Returns the skip reason when fixtures are unavailable, else null.</summary>
    public static string? GetSkipReason()
        => ResolveRoot() is null
            ? $"Set {FixturesDirEnv} to an extracted conformance-fixtures-{PinnedVersion} bundle " +
              $"(conformance/fetch-fixtures.sh --version {PinnedVersion}) to run schema conformance."
            : null;

    /// <summary>Reads a fixture JSON payload by file name (e.g. <c>feature_query_response.json</c>).</summary>
    public static string ReadFixture(string fileName)
    {
        var root = ResolveRoot()
            ?? throw new InvalidOperationException($"{FixturesDirEnv} is not set.");
        var path = Path.Combine(root, "fixtures", fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Conformance fixture not found: {path}");
        }

        return File.ReadAllText(path);
    }
}
