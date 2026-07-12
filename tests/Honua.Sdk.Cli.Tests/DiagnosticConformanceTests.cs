using System.Security.Cryptography;
using System.Text.Json;
using Honua.Sdk.Cli;

namespace Honua.Sdk.Cli.Tests;

public sealed class DiagnosticConformanceTests
{
    [Fact]
    public void CanonicalArtifacts_MatchPinnedProvenanceAndEmbeddedSchema()
    {
        string root = FindRepositoryRoot();
        byte[] schema = File.ReadAllBytes(Path.Combine(root, "schemas", "diagnostic-bundle.v1.json"));
        byte[] embedded = DiagnosticSchema.LoadCanonicalBytes();
        Assert.Equal(6494, schema.Length);
        Assert.Equal(DiagnosticSchema.Sha256, Convert.ToHexStringLower(SHA256.HashData(schema)));
        Assert.Equal(schema, embedded);

        using JsonDocument provenance = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(root, "schemas", "diagnostic-bundle.v1.provenance.json")));
        Assert.Equal(DiagnosticSchema.SourceCommit, provenance.RootElement.GetProperty("sourceCommit").GetString());
        Assert.Equal(DiagnosticSchema.Sha256, provenance.RootElement.GetProperty("sha256").GetString());
        Assert.Equal(DiagnosticSchema.ByteCount, provenance.RootElement.GetProperty("bytes").GetInt32());
        Assert.Equal(DiagnosticSchema.CanonicalUrl, provenance.RootElement.GetProperty("canonicalUrl").GetString());
    }

    [Fact]
    public void MergedConformanceCorpus_AcceptsAndRejectsEveryDeclaredCase()
    {
        string corpus = Path.Combine(FindRepositoryRoot(), "schemas", "diagnostic-bundle.v1.conformance");
        Dictionary<string, string> canonicalHashes = new(StringComparer.Ordinal)
        {
            ["manifest.json"] = "b27dd139ea4f2617cd094fc23b28c880e12777d57eef01430cd00493adbdaae9",
            ["valid/minimal.json"] = "933a2b5babde8a63c72142d0cb30692ff0b7546b4c1a2c9db1ae96736125141d",
            ["valid/sanitized-exchange.json"] = "e3e10b0a2ef12ce46bf351f3523e4dd46ed12d53e614ecf08d8b2596733b0e66",
            ["invalid/additional-property.json"] = "49565f95e85b050a1c325a59f3c2927f10e5549698be2972fc74d050dea2d718",
            ["invalid/missing-consent.json"] = "dd8af980bc23cb18f2c4442b23fe7b13ec28f0d1df8d2a3e97c5390765ad9061",
            ["invalid/optional-null.json"] = "df2592a2d1d5660b1c8dba330e09fc8c95b2a03fb7a7a89d589f80e4f50ed0c0",
            ["invalid/status-out-of-range.json"] = "777a5e21014188ee6f8e1708bc339e5c71fa9c785a795f5da5951c6c2089913a",
            ["invalid/wrong-version.json"] = "fd3d04e99667f22275747e7d0fa42868dfb3a325e50f58ebcad3ed9634849e8f"
        };
        foreach (KeyValuePair<string, string> artifact in canonicalHashes)
        {
            string digest = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(corpus, artifact.Key))));
            Assert.Equal(artifact.Value, digest);
        }

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(corpus, "manifest.json")));
        Assert.Equal(DiagnosticSchema.CanonicalUrl, manifest.RootElement.GetProperty("schemaId").GetString());
        Assert.Equal(DiagnosticSchema.Sha256, manifest.RootElement.GetProperty("schemaSha256").GetString());

        foreach (JsonElement testCase in manifest.RootElement.GetProperty("cases").EnumerateArray())
        {
            string path = testCase.GetProperty("path").GetString()!;
            bool expectedValid = testCase.GetProperty("valid").GetBoolean();
            using JsonDocument instance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(corpus, path)));
            IReadOnlyList<string> errors = DiagnosticSchema.Instance.Validate(instance.RootElement);
            Assert.Equal(expectedValid, errors.Count == 0);
            if (!expectedValid)
            {
                string expected = testCase.GetProperty("expectedErrorContains").GetString()!;
                Assert.Contains(errors, error => error.Contains(expected, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void OutputValidation_RejectsOptionalNullBeforeCreatingFile()
    {
        using TemporaryDirectory temporary = new();
        string output = Path.Combine(temporary.Path, "invalid.json");
        DiagnosticBundle invalid = new(
            "1.0",
            "internal",
            new DiagnosticConsent(true, false),
            [new DiagnosticEnvelope("GET", "/healthz/ready")],
            BundleId: null);

        // Null is normally omitted by the serializer. Deliberately validate the canonical
        // invalid corpus instance to prove the gate fails before any writer can accept it.
        using JsonDocument nullInstance = JsonDocument.Parse("""
            {"schemaVersion":"1.0","bundleId":null,"contentClassification":"internal","consent":{"redactionAcknowledged":true,"shareWithSupport":false},"envelopes":[{"method":"GET","normalizedPath":"/healthz/ready"}]}
            """);
        Assert.NotEmpty(DiagnosticSchema.Instance.Validate(nullInstance.RootElement));

        HonuaCli.WriteValidatedBundle(output, invalid);
        string serialized = File.ReadAllText(output);
        Assert.DoesNotContain(": null", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"bundleId\"", serialized, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Honua.Sdk.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
