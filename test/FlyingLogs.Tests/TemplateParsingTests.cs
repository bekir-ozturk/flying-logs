using FlyingLogs.Core;

namespace FlyingLogs.Tests;

public class TemplateParsingTests
{

    [Test]
    public void LogMethodsAreDetectedWithUsingFlyingLogs()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
Log.Information.L1("hello");
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);
    }

    [Test]
    public void LogMethodsAreDetectedWhenAccessedViaNamespace()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
FlyingLogs.Log.Information.L1("hello");
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);
    }

    [Test]
    public void LogMethodsAreDetectedWithUsingAliases()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using What = FlyingLogs;
What.Log.Information.L1("hello");
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);
    }

    [Test]
    public void CanCreateSingleParameterMethod()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
Log.Information.L1("Hello, {name}!", "world");
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);
    }

    [Test]
    [Ignore("Reserved keywords are not handled properly. https://github.com/bekir-ozturk/flying-logs/issues/1")]
    public void CanHandleParameterNamesThatAreReservedKeywords()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
Log.Information.L1("Hello, {object}!", "world");
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);
    }

    [Test]
    public void CanHandleMultipleTemplateParameters()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
Log.Information.L1("I ate not {num1}, not {num2} but {num3} apples today.", 1, 2, 3);
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);
    }

    [Test]
    public void CanHandleNullableRefTypeParameters()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
string name = "Jerry Smith";
Log.Information.L1(".. then my name isn't {name}.", name);
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);
    }

    [Test]
    public void CanHandleNullableValueTypeParameters()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
Log.Information.L1("Recorded graduation date is {date}.", (System.DateTime?)null);
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);
    }

    [Test]
    public void CanHandleParametersWithNullableAnnotations()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
#nullable enable
using FlyingLogs;
string? someString = "World";
Log.Information.L1("Hello, {name}.", someString);
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);
    }

    [Test]
    public void ChecksReferenceTypesForNullValue()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
Log.Information.L1("hello {name}", "world");
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);

        string? methodDefinitionTree = compilation.SyntaxTrees
            .Select(s => s.ToString())
            .FirstOrDefault(t => t.ToString().Contains("public static void L1(string template"));
        Assert.That(methodDefinitionTree, Is.Not.Null);
        Assert.That(methodDefinitionTree, Does.Contain("TryGetBytes(name"));
        Assert.That(methodDefinitionTree, Does.Match("if\\s*\\(\\s*name\\s*==\\s*null\\)"));
    }
}
