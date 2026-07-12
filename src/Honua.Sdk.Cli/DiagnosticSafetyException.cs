namespace Honua.Sdk.Cli;

internal sealed class DiagnosticSafetyException : Exception
{
    public DiagnosticSafetyException()
    {
    }

    public DiagnosticSafetyException(string message)
        : base(message)
    {
    }

    public DiagnosticSafetyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public DiagnosticSafetyException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    internal string? Code { get; }
}
