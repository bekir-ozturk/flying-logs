using Microsoft.CodeAnalysis;

namespace FlyingLogs.Analyzers;

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor LogMethodNameIsNotUnique = new DiagnosticDescriptor(
        id: "FL0001",
        title: $"Log method names should be unique.",
        messageFormat: $"Log method '{{0}}' is called from multiple places in code. " 
            + "Update one of the names so that each log method is unique.",
        category: "FlyingLogs.Analyzers",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TemplateStringShouldBeConstant = new DiagnosticDescriptor(
        id: "FL0002",
        title: $"First argument of a log message must always be a constant string.",
        messageFormat: $"First argument of a log message must always be a constant string.",
        category: "FlyingLogs.Analyzers",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
