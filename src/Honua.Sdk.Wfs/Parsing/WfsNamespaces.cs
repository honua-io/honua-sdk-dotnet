// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Xml.Linq;

namespace Honua.Sdk.Wfs.Parsing;

/// <summary>
/// OGC XML namespace constants used by WFS 2.0 responses.
/// </summary>
internal static class WfsNamespaces
{
    public static readonly XNamespace Wfs = "http://www.opengis.net/wfs/2.0";
    public static readonly XNamespace Ows = "http://www.opengis.net/ows/1.1";
    public static readonly XNamespace Fes = "http://www.opengis.net/fes/2.0";
    public static readonly XNamespace Gml = "http://www.opengis.net/gml/3.2";
    public static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";
    public static readonly XNamespace Xlink = "http://www.w3.org/1999/xlink";
}
