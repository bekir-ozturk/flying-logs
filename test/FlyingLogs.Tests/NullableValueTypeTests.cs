using FlyingLogs.Core;
using FlyingLogs.Shared;

using Microsoft.CodeAnalysis;

namespace FlyingLogs.Tests;

public class NullableValueTypeTests
{
    [Test]
    public void CanAcceptNullableValueProperties()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
Log.Information.L1("Its {clock} o'clock.", (int?)4);
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);

        string? methodDefinitionTree = compilation.SyntaxTrees
            .Select(s => s.ToString())
            .FirstOrDefault(t => t.ToString().Contains("public static void L1(string template, int? clock)"));
        Assert.That(methodDefinitionTree, Is.Not.Null);
    }

    [Test]
    public void CanExpandNullableValueTypes()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
using System.Drawing;
#nullable enable
Log.Information.L1("Dead pixel detected.", @point: (Point?)new Point(1,2));
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);

        string? methodDefinitionTree = compilation.SyntaxTrees
            .Select(s => s.ToString())
            .FirstOrDefault(t => t.ToString().Contains("public static void L1(string template, System.Drawing.Point? point)"));
        Assert.That(methodDefinitionTree, Is.Not.Null);
        Assert.That(methodDefinitionTree, Does.Contain("if (point == null)"));
        Assert.That(methodDefinitionTree, Does.Contain(nameof(PropertyValueHints) + "." + nameof(PropertyValueHints.Complex)));
        Assert.That(methodDefinitionTree, Does.Contain(nameof(PropertyValueHints) + "." + nameof(PropertyValueHints.Skip)));
        Assert.That(methodDefinitionTree, Does.Not.Contain("point.HasValue"));
        Assert.That(methodDefinitionTree, Does.Contain("point.Value.IsEmpty"));
        Assert.That(methodDefinitionTree, Does.Contain("point.Value.X"));
        Assert.That(methodDefinitionTree, Does.Contain("point.Value.Y"));
        Assert.That(methodDefinitionTree, Does.Not.Contain("point.ToString"));
        Assert.That(methodDefinitionTree, Does.Not.Contain("point.TryFormat"));
    }

    [Test]
    public void WontExpandNullablePrimitives()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
Log.Information.L1("Thread terminated.", @threadCount: (int?)null);
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);

        string? methodDefinitionTree = compilation.SyntaxTrees
            .Select(s => s.ToString())
            .FirstOrDefault(t => t.ToString().Contains("public static void L1(string template, int? threadCount)"));
        Assert.That(methodDefinitionTree, Is.Not.Null);
        Assert.That(methodDefinitionTree, Does.Contain("if (threadCount == null)"));
        // If the value is null, the 'Null' hint should be added to the values.
        Assert.That(methodDefinitionTree, Does.Contain(nameof(PropertyValueHints) + "." + nameof(PropertyValueHints.Null)));
        // Since int isn't a complex type (even in the 'Nullable<int>' form), it shouldn't be marked 'Complex'.
        Assert.That(methodDefinitionTree, Does.Not.Contain(nameof(PropertyValueHints) + "." + nameof(PropertyValueHints.Complex)));
        // Primitive type contains no fields to skip.
        Assert.That(methodDefinitionTree, Does.Not.Contain(nameof(PropertyValueHints) + "." + nameof(PropertyValueHints.Skip)));
        Assert.That(methodDefinitionTree, Does.Contain("threadCount.Value.TryFormat("));
        Assert.That(methodDefinitionTree, Does.Not.Contain("threadCount.HasValue"));

    }

    [Test]
    public void ExpandingExcludesHasValueProperty()
    {
        Helpers.Compile(out var compilation, out var diagnostics, """
using FlyingLogs;
using System.Drawing;
Log.Information.L1("Dead pixel detected.", @point: (Point?)new Point(1,2));
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);

        string? methodDefinitionTree = compilation.SyntaxTrees
            .Select(s => s.ToString())
            .FirstOrDefault(t => t.ToString().Contains("public static void L1(string template, System.Drawing.Point? point)"));
        Assert.That(methodDefinitionTree, Is.Not.Null);
        Assert.That(methodDefinitionTree, Does.Contain("if (point == null)"));
        Assert.That(methodDefinitionTree, Does.Not.Contain("point.HasValue"));
    }
}
