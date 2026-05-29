// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using OfflineConflictConsole;

var summary = await OfflineConflictDemo.RunAsync(Console.Out);

// Expected: ManualReview records a conflict (1 edit request, detected),
// ServerWins drops the local edit (1 edit request, succeeded), and
// ClientWins force-writes after the first conflict (2 edit requests, succeeded).
var ok =
    summary.ManualReview is { Conflicts: 1, ConflictDetected: true, EditRequestCount: 1 } &&
    summary.ServerWins is { Succeeded: 1, ConflictDetected: false, EditRequestCount: 1 } &&
    summary.ClientWins is { Succeeded: 1, ConflictDetected: false, EditRequestCount: 2 };

return ok ? 0 : 1;
