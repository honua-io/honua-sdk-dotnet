using RealtimeWorker;

var mode = Environment.GetEnvironmentVariable("HONUA_REALTIME_MODE") ?? "simulated";
if (string.Equals(mode, "server", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Live realtime transport is gated on Honua Server realtime endpoints. Run without HONUA_REALTIME_MODE=server for the deterministic SDK contract simulation.");
    return 2;
}

var summary = await RealtimeWorkerSimulation.RunAsync(Console.Out);
return summary.RejectedCount == 2 && summary.LastSequenceNumber == 4 ? 0 : 1;
