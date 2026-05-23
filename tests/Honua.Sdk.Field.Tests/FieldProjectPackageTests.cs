using System.Globalization;
using System.Text.Json;
using Honua.Sdk.Field.Projects;
using Honua.Sdk.Field.Records;

namespace Honua.Sdk.Field.Tests;

public sealed class FieldProjectPackageTests
{
    [Fact]
    public void Fixture_DeserializesAndValidatesLocalPackageModel()
    {
        var package = FieldProjectPackage.ParseJson(ReadFixture("field-project-package.v1.json"));

        Assert.Equal(FieldProjectPackage.CurrentSchemaVersion, package.SchemaVersion);
        Assert.Equal("local-inspection-demo", package.ProjectId);
        Assert.Equal(2, package.Forms.Count);
        Assert.Equal(2, package.Sources.Count);
        Assert.Equal(2, package.Bindings.Count);
        Assert.Equal(2, package.TaskPackets[0].Assignments.Count);

        var validation = package.Validate();

        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Issues.Select(issue => issue.Message)));
        Assert.Contains(package.LifecyclePolicy.AllowedStatuses, status => status == RecordStatus.ReadyToSubmit);
        Assert.Contains(package.LifecyclePolicy.AllowedTransitions, transition =>
            transition.From == RecordStatus.Rejected && transition.To == RecordStatus.Reopened);
    }

    [Fact]
    public void ToJson_RoundTripsWithStableCamelCaseShape()
    {
        var package = FieldProjectPackage.ParseJson(ReadFixture("field-project-package.v1.json"));

        var json = package.ToJson();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("schemaVersion", out _));
        Assert.True(root.TryGetProperty("projectId", out _));
        Assert.True(root.TryGetProperty("mediaPolicy", out var mediaPolicy));
        Assert.True(mediaPolicy.TryGetProperty("captureGpsTrackForTimedMedia", out _));
        Assert.Equal("ReadyToSubmit", root
            .GetProperty("lifecyclePolicy")
            .GetProperty("allowedStatuses")[1]
            .GetString());
    }

    [Fact]
    public void Validate_ReturnsReferenceDiagnosticsForInvalidPackage()
    {
        var package = FieldProjectPackage.ParseJson(ReadFixture("field-project-package.v1.json")) with
        {
            Bindings =
            [
                new FieldProjectBinding
                {
                    BindingId = "broken",
                    FormId = "missing-form",
                    SourceId = "missing-source",
                    OfflinePackageId = "missing-offline-package",
                }
            ],
            TaskPackets =
            [
                new FieldTaskPacket
                {
                    TaskPacketId = "tasks",
                    Assignments =
                    [
                        new FieldAssignment
                        {
                            AssignmentId = "assignment-1",
                            BindingId = "missing-binding",
                        }
                    ],
                },
            ],
        };

        var result = package.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Path == "$.bindings[0].formId");
        Assert.Contains(result.Issues, issue => issue.Path == "$.bindings[0].sourceId");
        Assert.Contains(result.Issues, issue => issue.Path == "$.bindings[0].offlinePackageId");
        Assert.Contains(result.Issues, issue => issue.Path == "$.taskPackets[0].assignments[0].bindingId");
    }

    [Fact]
    public void RecordWorkflow_AllowsNoCloudLifecycleTransitions()
    {
        var createdAt = DateTimeOffset.Parse("2026-05-23T08:00:00Z", CultureInfo.InvariantCulture);
        var record = new FieldRecord
        {
            RecordId = "record-local-1",
            FormId = "inspection",
            CreatedAtUtc = createdAt,
        };

        RecordWorkflow.Transition(record, RecordStatus.ReadyToSubmit, createdAt.AddMinutes(10));
        RecordWorkflow.Transition(record, RecordStatus.Submitted, createdAt.AddMinutes(20));
        RecordWorkflow.Transition(record, RecordStatus.Rejected, createdAt.AddMinutes(30));
        RecordWorkflow.Transition(record, RecordStatus.Reopened, createdAt.AddMinutes(40));

        Assert.Equal(RecordStatus.Reopened, record.Status);
        Assert.Equal(createdAt.AddMinutes(20), record.SubmittedAtUtc);
        Assert.Null(record.CompletedAtUtc);
        Assert.True(RecordWorkflow.CanTransition(RecordStatus.Reopened, RecordStatus.Deleted));
    }

    private static string ReadFixture(string name)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Json", name));
}
