// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using FieldFormConsole;

var summary = FieldFormDemo.Run(Console.Out);

// Expected: the incomplete record fails validation with several errors, the
// complete record passes, and calculated fields produce the joined name and total.
var ok =
    summary is { InvalidIsValid: false, ValidIsValid: true } &&
    summary.InvalidErrorCount >= 3 &&
    summary.CalculatedInspectorName == "Leilani Kealoha" &&
    summary.CalculatedSampleTotal == "10";

return ok ? 0 : 1;
