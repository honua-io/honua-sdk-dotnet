// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Admin.Models;
using Honua.Sdk.Admin.Tests.Fixtures;

namespace Honua.Sdk.Admin.Tests;

public sealed class LicenseAdminTests
{
    [Fact]
    public async Task GetLicenseStatusAsync_ReturnsStatus()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(90);
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.EndsWith("/api/v1/admin/license", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateJsonResponse(CreateStatus(expiresAt)));
        });

        var status = await client.GetLicenseStatusAsync();

        Assert.Equal("Enterprise", status.Edition);
        Assert.Equal(expiresAt, status.ExpiresAt);
        Assert.Equal("BYOL portal", status.IssuanceSource);
        Assert.Equal("oidc", Assert.Single(status.Entitlements).Key);
    }

    [Fact]
    public async Task GetLicenseEntitlementsAsync_ReturnsEntitlements()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.EndsWith("/api/v1/admin/license/entitlements", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateJsonResponse(new[]
            {
                new
                {
                    key = "rbac",
                    name = "Role-based access control",
                    isActive = true
                }
            }));
        });

        var entitlements = await client.GetLicenseEntitlementsAsync();

        var entitlement = Assert.Single(entitlements);
        Assert.Equal("rbac", entitlement.Key);
        Assert.True(entitlement.IsActive);
    }

    [Fact]
    public async Task UploadLicenseAsync_SendsOctetStreamAndReturnsStatus()
    {
        var payload = new byte[] { 1, 2, 3, 4 };
        var client = TestHelpers.CreateClient(async req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.EndsWith("/api/v1/admin/license", req.RequestUri!.PathAndQuery);
            Assert.Equal("application/octet-stream", req.Content!.Headers.ContentType!.MediaType);
            Assert.Equal(payload, await req.Content.ReadAsByteArrayAsync());

            return TestHelpers.CreateJsonResponse(CreateStatus(DateTimeOffset.UtcNow.AddDays(365)), HttpStatusCode.Created);
        });

        var status = await client.UploadLicenseAsync(payload);

        Assert.True(status.IsValid);
    }

    [Fact]
    public async Task UploadLicenseAsync_RejectsNullPayload()
    {
        var client = TestHelpers.CreateClient(_ => Task.FromResult(TestHelpers.CreateJsonResponse(CreateStatus(DateTimeOffset.UtcNow))));

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.UploadLicenseAsync(null!));
    }

    private static object CreateStatus(DateTimeOffset expiresAt) => new
    {
        edition = "Enterprise",
        expiresAt,
        issuedAt = DateTimeOffset.UtcNow.AddDays(-1),
        licensedTo = "Honua Demo Org",
        isValid = true,
        issuanceSource = LicenseStatusResponse.DefaultIssuanceSource,
        validationState = "valid",
        daysUntilExpiry = 90,
        expiryWarning = false,
        entitlements = new[]
        {
            new
            {
                key = "oidc",
                name = "OIDC sign-in",
                isActive = true
            }
        }
    };
}
