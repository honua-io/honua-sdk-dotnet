// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Grpc.Tests.Fixtures;

internal static class GrpcFixtureReader
{
    public static string ReadJson(string fileName)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Json", fileName));
}
