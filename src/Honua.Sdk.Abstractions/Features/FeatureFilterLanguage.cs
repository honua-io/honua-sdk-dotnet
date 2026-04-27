// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Identifies the filter language used by a provider-neutral feature query.
/// </summary>
public enum FeatureFilterLanguage
{
    /// <summary>
    /// Use the provider's default filter language.
    /// </summary>
    ProviderDefault,

    /// <summary>
    /// SQL WHERE syntax, used by gRPC and GeoServices FeatureServer clients.
    /// </summary>
    SqlWhere,

    /// <summary>
    /// CQL2 text syntax, used by OGC API Features clients.
    /// </summary>
    Cql2Text,

    /// <summary>
    /// FES 2.0 XML syntax, used by WFS clients.
    /// </summary>
    FesXml
}
