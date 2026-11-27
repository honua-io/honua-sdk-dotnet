// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Field.Forms;

/// <summary>The server receipt for a published-form submission.</summary>
public sealed class PublishedFormSubmissionReceipt
{
    /// <summary>Server-assigned submission identifier, when available.</summary>
    public Guid? SubmissionId { get; init; }

    /// <summary>Server submission status.</summary>
    public string? Status { get; init; }

    /// <summary>Published form identifier.</summary>
    public string? FormId { get; init; }

    /// <summary>Published form version used by the submission.</summary>
    public int FormVersion { get; init; }

    /// <summary>The requested feature operation.</summary>
    public string? Operation { get; init; }

    /// <summary>The identifier of the edited feature, when known.</summary>
    public long? TargetFeatureId { get; init; }

    /// <summary>The result of the feature edit, when attempted.</summary>
    public PublishedFormEditOutcome? EditOutcome { get; init; }

    /// <summary>Server results for the submitted evidence parts.</summary>
    public IReadOnlyList<PublishedFormAttachmentOutcome>? AttachmentOutcomes { get; init; }

    /// <summary>Validation issues returned by the server.</summary>
    public IReadOnlyList<PublishedFormValidationIssue>? ValidationIssues { get; init; }

    /// <summary>Optional retry guidance for this submission.</summary>
    public PublishedFormRetryGuidance? Retry { get; init; }

    /// <summary>Whether this result replays the receipt for the same idempotency key.</summary>
    public bool IdempotentReplay { get; init; }

    /// <summary>HTTP status of the response that carried this receipt.</summary>
    [JsonIgnore]
    public HttpStatusCode HttpStatusCode { get; internal set; }
}

/// <summary>The outcome of the requested feature edit.</summary>
public sealed record PublishedFormEditOutcome(bool Succeeded, int Created, int Updated, int Deleted, string? Error);

/// <summary>The outcome for one evidence part.</summary>
public sealed record PublishedFormAttachmentOutcome(
    string? ClientAttachmentId, string? FieldId, string? Status, long? AttachmentId, string? Reason, bool PrivacyApplied);

/// <summary>A validation issue for the submitted form data.</summary>
public sealed record PublishedFormValidationIssue(string? Code, string? Severity, string? FieldId, string? Path, string? Message);

/// <summary>Guidance about retrying a submission.</summary>
public sealed record PublishedFormRetryGuidance(bool? Retryable, int? RetryAfterSeconds, string? Reason);
