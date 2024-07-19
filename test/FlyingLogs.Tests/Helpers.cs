using System.Collections.Immutable;
using System.Reflection;

using FlyingLogs.Analyzers;
using FlyingLogs.Core;
using FlyingLogs.Shared;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FlyingLogs.Tests;

public static class Helpers
{
    public static void Compile(out Compilation compilation, out ImmutableArray<Diagnostic> diagnostics, params string[] source)
    {
        Compilation initialCompilation = CSharpCompilation.Create("compilation",
                source.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray(),
                references: 
                    Basic.Reference.Assemblies.Net80.References.All.Concat(new[]
                    {
                        MetadataReference.CreateFromFile(typeof(LogLevel).GetTypeInfo().Assembly.Location),
                        MetadataReference.CreateFromFile(typeof(LogTemplate).GetTypeInfo().Assembly.Location),
                    }),
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        FlyingLogsSourceGenerator generator = new ();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Run the initial compilation through the source generator.
        _ = driver.RunGeneratorsAndUpdateCompilation(initialCompilation, out compilation, out diagnostics);
    }
}
