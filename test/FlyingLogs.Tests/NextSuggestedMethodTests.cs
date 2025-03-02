using FlyingLogs.Tests;

public class NextSuggestedMethodNameTests {

    private string? suggestedMethodPrefixBeforeSetup;
    [SetUp]
    public void Setup()
    {
        suggestedMethodPrefixBeforeSetup = Environment.GetEnvironmentVariable(
            "FLYINGLOGS_SUGGESTED_METHOD_PREFIX");

        Environment.SetEnvironmentVariable(
            "FLYINGLOGS_SUGGESTED_METHOD_PREFIX",
            null,
            EnvironmentVariableTarget.Process);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(
            "FLYINGLOGS_SUGGESTED_METHOD_PREFIX",
            suggestedMethodPrefixBeforeSetup,
            EnvironmentVariableTarget.Process);
    }

    [Test]
    public void CanSuggestNextMethod()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
Log.Information.L1("hello", time: System.DateTime.UtcNow);
""");

        Assert.That(compilation.SyntaxTrees.Any(t => t.GetText().ToString().Contains(
            """public const string L2_ = "This field exists to hint you a unique method name. Use it to trigger code completion and remove the underscore afterwards.";"""
        )));
    }

    [Test]
    public void ContinuesFromLastUsedNumber()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
Log.Information.L7("hello", time: System.DateTime.UtcNow);
""");

        Assert.That(compilation.SyntaxTrees.Any(t => t.GetText().ToString().Contains(
            """public const string L8_ = "This field exists to hint you a unique method name. Use it to trigger code completion and remove the underscore afterwards.";"""
        )));
    }

    [Test]
    public void CanProcessHighNumbers()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
Log.Information.L999999999999999999999999999999999999999("hello", time: System.DateTime.UtcNow);
""");

        Assert.That(compilation.SyntaxTrees.Any(t => t.GetText().ToString().Contains(
            """public const string L1000000000000000000000000000000000000000_ = "This field exists to hint you a unique method name. Use it to trigger code completion and remove the underscore afterwards.";"""
        )));
    }

    [Test]
    public void SuggestedPrefixCanBeOverridenWithEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable(
            "FLYINGLOGS_SUGGESTED_METHOD_PREFIX",
            "Boo",
            EnvironmentVariableTarget.Process);

        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
Log.Information.L3("hello", time: System.DateTime.UtcNow);
""");

        Assert.That(compilation.SyntaxTrees.Any(t => t.GetText().ToString().Contains(
            """public const string Boo4_ = "This field exists to hint you a unique method name. Use it to trigger code completion and remove the underscore afterwards.";"""
        )));
    }

    [Test]
    public void AnyMethodNameCounts()
    {
        Environment.SetEnvironmentVariable(
            "FLYINGLOGS_SUGGESTED_METHOD_PREFIX",
            "Foo",
            EnvironmentVariableTarget.Process);

        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
Log.Information.L3("hello", time: System.DateTime.UtcNow);
Log.Information.Hey7("hello");
""");

        Assert.That(compilation.SyntaxTrees.Any(t => t.GetText().ToString().Contains(
            """public const string Foo8_ = "This field exists to hint you a unique method name. Use it to trigger code completion and remove the underscore afterwards.";"""
        )));
    }
}