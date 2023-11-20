using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Reflection;
using FlyingLogs.Analyzers;
using System.Diagnostics;

namespace FlyingLogs.Tests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        Compilation inputCompilation = CreateCompilation(@"
namespace MyCode
{
    using FlyingLogs;
    public class Program
    {
        public static void Main(string[] args)
        {
            FlyingLogs.Log.Error.Hello(""This is a message template {position}"");
            Log.Information.What(""heyt!"");
        }
    }
}
");

        // directly create an instance of the generator
        // (Note: in the compiler this is loaded from an assembly, and created via reflection at runtime)
        FlyingLogsSourceGenerator generator = new ();

        // Create the driver that will control the generation, passing in our generator
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Run the generation pass
        // (Note: the generator driver itself is immutable, and all calls return an updated version of the driver that you should use for subsequent calls)
        driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);

        // We can now assert things about the resulting compilation:
        Debug.Assert(diagnostics.IsEmpty); // there were no diagnostics created by the generators
        Debug.Assert(outputCompilation.SyntaxTrees.Count() == 8); // we have two syntax trees, the original 'user' provided one, and the one added by the generator
        Debug.Assert(outputCompilation.GetDiagnostics().IsEmpty); // verify the compilation with the added source has no diagnostics
    }

    private static Compilation CreateCompilation(string source)
            => CSharpCompilation.Create("compilation",
                new[] { CSharpSyntaxTree.ParseText(source) },
                new[] { MetadataReference.CreateFromFile(typeof(Binder).GetTypeInfo().Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));
}