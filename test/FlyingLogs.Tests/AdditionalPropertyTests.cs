using FlyingLogs.Core;
using FlyingLogs.Shared;

using Microsoft.CodeAnalysis;

namespace FlyingLogs.Tests;


public class AdditionalPropertyTests
{
    [Test]
    public void CanHandleSingleAdditionalProperty()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
Log.Information.L1("hello", time: System.DateTime.UtcNow);
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);

        string? methodDefinitionTree = compilation.SyntaxTrees
            .Select(s => s.ToString())
            .FirstOrDefault(t => t.ToString().Contains("public static void L1(string template"));
        Assert.That(methodDefinitionTree, Is.Not.Null);
        Assert.That(methodDefinitionTree, Does.Contain("time.TryFormat"));
    }

    [Test]
    public void CanHandleMultipleAdditionalProperties()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
Log.Information.L1("hello",
    time: System.DateTime.UtcNow,
    threadId: (System.Int32)System.Threading.Thread.CurrentThread.ManagedThreadId,
    o: new object());
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);

        string? methodDefinitionTree = compilation.SyntaxTrees
            .Select(s => s.ToString())
            .FirstOrDefault(t => t.ToString().Contains("public static void L1(string template"));
        Assert.That(methodDefinitionTree, Is.Not.Null);
        Assert.That(methodDefinitionTree, Does.Contain("time.TryFormat"));
        Assert.That(methodDefinitionTree, Does.Contain("threadId.TryFormat"));
        Assert.That(methodDefinitionTree, Does.Contain("o.ToString"));
    }

    [Test]
    public void CanExpand()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
using System.Drawing;
Log.Information.L1("hello", @point: new Point(1,2));
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);

        string? methodDefinitionTree = compilation.SyntaxTrees
            .Select(s => s.ToString())
            .FirstOrDefault(t => t.ToString().Contains("public static void L1(string template"));
        Assert.That(methodDefinitionTree, Is.Not.Null);
        Assert.That(methodDefinitionTree, Does.Contain("point.X"));
        Assert.That(methodDefinitionTree, Does.Not.Contain("point.ToString"));
    }

    [Test]
    public void DoesntExpandDateTime()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
Log.Information.L1("hello", @time: System.DateTime.UtcNow);
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);

        string? methodDefinitionTree = compilation.SyntaxTrees
            .Select(s => s.ToString())
            .FirstOrDefault(t => t.ToString().Contains("public static void L1(string template"));
        Assert.That(methodDefinitionTree, Is.Not.Null);
        Assert.That(methodDefinitionTree, Does.Contain("time.TryFormat"));
        Assert.That(methodDefinitionTree, Does.Not.Contain("time.Day"));
        Assert.That(methodDefinitionTree, Does.Not.Contain("time.Hour"));
        Assert.That(methodDefinitionTree, Does.Not.Contain(nameof(PropertyValueHints) + "." + nameof(PropertyValueHints.Complex)));
    }

    [Test]
    public void ChecksReferenceTypesForNullValue()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
Log.Information.L1("hello",
    time: System.DateTime.UtcNow,
    tId: System.Threading.Thread.CurrentThread.ManagedThreadId,
    o: new object());
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);

        string? methodDefinitionTree = compilation.SyntaxTrees
            .Select(s => s.ToString())
            .FirstOrDefault(t => t.ToString().Contains("public static void L1(string template"));
        Assert.That(methodDefinitionTree, Is.Not.Null);
        Assert.That(methodDefinitionTree, Does.Not.Match("time\\s*==\\s*null").And.Not.Match("null\\s*==\\s*time"));
        Assert.That(methodDefinitionTree, Does.Not.Match("tId\\s*==\\s*null").And.Not.Match("null\\s*==\\s*tId"));
        Assert.That(methodDefinitionTree, Does.Match("if\\s*\\(\\s*o\\s*==\\s*null\\)"));
    }
}
