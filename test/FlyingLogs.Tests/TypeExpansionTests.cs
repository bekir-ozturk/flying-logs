using FlyingLogs.Core;
using FlyingLogs.Shared;

using Microsoft.CodeAnalysis;

namespace FlyingLogs.Tests;


[TestFixture(LogEncodings.Utf8Plain)]
[TestFixture(LogEncodings.Utf8Json)]
public class TypeExpansionTests
{
    private readonly string _assemblyLevelAttributes;

    public TypeExpansionTests(LogEncodings encoding)
    {
        _assemblyLevelAttributes = $"""
    [assembly:FlyingLogs.Core.PreencodeAttribute(FlyingLogs.Core.LogEncodings.{encoding})]
""";
    }

    [Test]
    public void CanExpandUserDefinedTypeFields()
    {
        Helpers.Compile(out var compilation, out var diagnostics, _assemblyLevelAttributes, """
using FlyingLogs;
Log.Information.L1("Player spawned.", @player: new Player());
internal class Player
{
// Supress warning: Field is never assigned to and will always have its default value.
#pragma warning disable CS0649
    public int Health;
    public float Mana;
    public string Name;
}
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);

        string? methodDefinitionTree = compilation.SyntaxTrees
            .Select(s => s.ToString())
            .FirstOrDefault(t => t.ToString().Contains("public static void L1(string template, Player player)"));
        Assert.That(methodDefinitionTree, Is.Not.Null);
        Assert.That(methodDefinitionTree, Does.Contain("if (player == null)"));
        Assert.That(methodDefinitionTree, Does.Contain(nameof(PropertyValueHints) + "." + nameof(PropertyValueHints.Complex)));
        Assert.That(methodDefinitionTree, Does.Contain(nameof(PropertyValueHints) + "." + nameof(PropertyValueHints.Skip)));
        Assert.That(methodDefinitionTree, Does.Not.Contain("player.ToString("));
        Assert.That(methodDefinitionTree, Does.Contain("player.Health.TryFormat"));
        Assert.That(methodDefinitionTree, Does.Contain("player.Mana.TryFormat"));
        Assert.That(methodDefinitionTree, Does.Contain("if (player.Name == null)"));
        Assert.That(methodDefinitionTree, Does.Contain("TryGetBytes(player.Name"));
    }

    [Test]
    public void CanExpandUserDefinedTypeProperties()
    {
        Helpers.Compile(out var compilation, out var diagnostics, _assemblyLevelAttributes, """
using FlyingLogs;
Log.Information.L1("Player spawned.", @player: new Player());
internal class Player
{
    public int Health { get; set; }
    public float Mana { get; set; }
    public string Name { get; set; }
}
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);

        string? methodDefinitionTree = compilation.SyntaxTrees
            .Select(s => s.ToString())
            .FirstOrDefault(t => t.ToString().Contains("public static void L1(string template, Player player)"));
        Assert.That(methodDefinitionTree, Is.Not.Null);
        Assert.That(methodDefinitionTree, Does.Contain("if (player == null)"));
        Assert.That(methodDefinitionTree, Does.Contain(nameof(PropertyValueHints) + "." + nameof(PropertyValueHints.Complex)));
        Assert.That(methodDefinitionTree, Does.Contain(nameof(PropertyValueHints) + "." + nameof(PropertyValueHints.Skip)));
        Assert.That(methodDefinitionTree, Does.Not.Contain("player.ToString("));
        Assert.That(methodDefinitionTree, Does.Contain("player.Health.TryFormat"));
        Assert.That(methodDefinitionTree, Does.Contain("player.Mana.TryFormat"));
        Assert.That(methodDefinitionTree, Does.Contain("if (player.Name == null)"));
        Assert.That(methodDefinitionTree, Does.Contain("TryGetBytes(player.Name"));
    }

    [Test]
    public void WontExpandPrivateStaticConstFields()
    {
        Helpers.Compile(out var compilation, out var diagnostics, _assemblyLevelAttributes, """
using FlyingLogs;
Log.Information.L1("Player spawned.", @player: new Player());
internal class Player
{
// Supress warning: Field is never assigned to and will always have its default value.
// Supress warning: The field is never used.
#pragma warning disable CS0649, CS0169
    public static int Health;
    public const float Mana = 0;
    private string Name;
}
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);

        string? methodDefinitionTree = compilation.SyntaxTrees
            .Select(s => s.ToString())
            .FirstOrDefault(t => t.ToString().Contains("public static void L1(string template, Player player)"));
        Assert.That(methodDefinitionTree, Is.Not.Null);
        Assert.That(methodDefinitionTree, Does.Contain("if (player == null)"));
        Assert.That(methodDefinitionTree, Does.Contain(nameof(PropertyValueHints) + "." + nameof(PropertyValueHints.Complex)));
        // Empty object, nothing to skip.
        Assert.That(methodDefinitionTree, Does.Not.Contain(nameof(PropertyValueHints) + "." + nameof(PropertyValueHints.Skip)));
        Assert.That(methodDefinitionTree, Does.Not.Contain("player.ToString("));
        Assert.That(methodDefinitionTree, Does.Not.Contain("player.Health.TryFormat"));
        Assert.That(methodDefinitionTree, Does.Not.Contain("player.Mana.TryFormat"));
        Assert.That(methodDefinitionTree, Does.Not.Contain("if (player.Name == null)"));
        Assert.That(methodDefinitionTree, Does.Not.Contain("TryGetBytes(player.Name"));
    }

    [Test]
    public void WontExpandPrivateStaticAbstractWriteOnlyFields()
    {
        Helpers.Compile(out var compilation, out var diagnostics, _assemblyLevelAttributes, """
using FlyingLogs;
Log.Information.L1("Player spawned.", @player: (Player)null);
internal abstract class Player
{
    public static int Health { get; set; }
    public abstract float Mana { get; set; }
    private string Name { get; set; }
    public Player Opponent { set { } }
}
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);

        string? methodDefinitionTree = compilation.SyntaxTrees
            .Select(s => s.ToString())
            .FirstOrDefault(t => t.ToString().Contains("public static void L1(string template, Player player)"));
        Assert.That(methodDefinitionTree, Is.Not.Null);
        Assert.That(methodDefinitionTree, Does.Contain("if (player == null)"));
        Assert.That(methodDefinitionTree, Does.Contain(nameof(PropertyValueHints) + "." + nameof(PropertyValueHints.Complex)));
        // Empty object, nothing to skip.
        Assert.That(methodDefinitionTree, Does.Not.Contain(nameof(PropertyValueHints) + "." + nameof(PropertyValueHints.Skip)));
        Assert.That(methodDefinitionTree, Does.Not.Contain("player.ToString("));
        Assert.That(methodDefinitionTree, Does.Not.Contain("player.Health.TryFormat"));
        Assert.That(methodDefinitionTree, Does.Not.Contain("player.Mana.TryFormat"));
        Assert.That(methodDefinitionTree, Does.Not.Contain("if (player.Name == null)"));
        Assert.That(methodDefinitionTree, Does.Not.Contain("TryGetBytes(player.Name"));
        Assert.That(methodDefinitionTree, Does.Not.Contain("if (player.Opponent == null)"));
        Assert.That(methodDefinitionTree, Does.Not.Contain("player.ToString("));
        Assert.That(methodDefinitionTree, Does.Not.Contain("player.Opponent.Health.TryFormat"));
    }

    [Test]
    public void WontExpandBeyondTheMaxDepth()
    {
        Helpers.Compile(out var compilation, out var diagnostics, _assemblyLevelAttributes, """
// Supress warning: Field is never assigned to and will always have its default value.
#pragma warning disable CS0649
using FlyingLogs;
Log.Information.L1("Player spawned.", @player: new Player());
internal class Player
{
    public Weapon weapon;
}
internal class Weapon
{
    public Ammo ammo;
}
internal class Ammo
{
    public float damage;
}
""");

        Assert.That(diagnostics.IsEmpty);
        Assert.That(compilation.GetDiagnostics().IsEmpty);

        string? methodDefinitionTree = compilation.SyntaxTrees
            .Select(s => s.ToString())
            .FirstOrDefault(t => t.ToString().Contains("public static void L1(string template, Player player)"));
        Assert.That(methodDefinitionTree, Is.Not.Null);
        Assert.That(methodDefinitionTree, Does.Contain("if (player == null)"));
        Assert.That(methodDefinitionTree, Does.Contain(nameof(PropertyValueHints) + "." + nameof(PropertyValueHints.Complex)));
        Assert.That(methodDefinitionTree, Does.Contain(nameof(PropertyValueHints) + "." + nameof(PropertyValueHints.Skip)));
        Assert.That(methodDefinitionTree, Does.Not.Contain("player.ToString("));
        Assert.That(methodDefinitionTree, Does.Contain("if (player.weapon == null)"));
        Assert.That(methodDefinitionTree, Does.Not.Contain("player.weapon.ToString("));
        Assert.That(methodDefinitionTree, Does.Contain("if (player.weapon.ammo == null)"));
        Assert.That(methodDefinitionTree, Does.Contain("player.weapon.ammo.ToString("));
        Assert.That(methodDefinitionTree, Does.Not.Contain("damage"));
    }
}
