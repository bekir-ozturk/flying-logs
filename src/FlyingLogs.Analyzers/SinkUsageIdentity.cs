using FlyingLogs.Shared;

using Microsoft.CodeAnalysis;

namespace FlyingLogs
{
    internal class SinkUsageIdentity : IEquatable<SinkUsageIdentity>
    {
        public string TypeName { get;set; }

        public LogLevel MinimumLogLevel { get; set; }

        public bool DynamicLogLevel { get; set; }

        public SinkUsageIdentity(string typeName)
        {
            TypeName = typeName;
            MinimumLogLevel = LogLevel.Information;
            DynamicLogLevel = false;
        }

        public SinkUsageIdentity(string typeName, LogLevel minimumLogLevel, bool dynamicLogLevel)
        {
            TypeName = typeName;
            MinimumLogLevel = minimumLogLevel;
            DynamicLogLevel = dynamicLogLevel;
        }

        public override bool Equals(object? obj)
        {
            return obj is SinkUsageIdentity identity &&
                   TypeName == identity.TypeName &&
                   MinimumLogLevel == identity.MinimumLogLevel &&
                   DynamicLogLevel == identity.DynamicLogLevel;
        }

        public bool Equals(SinkUsageIdentity? identity)
        {
            return identity is not null &&
                   TypeName == identity.TypeName &&
                   MinimumLogLevel == identity.MinimumLogLevel &&
                   DynamicLogLevel == identity.DynamicLogLevel;
        }

        public override int GetHashCode()
        {
            int hashCode = -1472231801;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(TypeName);
            hashCode = hashCode * -1521134295 + MinimumLogLevel.GetHashCode();
            hashCode = hashCode * -1521134295 + DynamicLogLevel.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(SinkUsageIdentity? left, SinkUsageIdentity? right)
        {
            return EqualityComparer<SinkUsageIdentity>.Default.Equals(left!, right!);
        }

        public static bool operator !=(SinkUsageIdentity? left, SinkUsageIdentity? right)
        {
            return !(left == right);
        }

        internal static SinkUsageIdentity? FromContext(
            GeneratorAttributeSyntaxContext context,
            CancellationToken cancellationToken)
        {
            if (context.Attributes.Length == 0)
                return null;

            // TODO can there be more than one attribute?
            var sinkTypeAttribute = context.Attributes[0];
            var constructorArguments = sinkTypeAttribute.ConstructorArguments;
            if (constructorArguments.Length < 1)
                return null;

            var itemType = constructorArguments[0].Value as INamedTypeSymbol;
            if (itemType == null)
                return null;

            if (constructorArguments.Length < 2)
                return new SinkUsageIdentity(itemType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            LogLevel minimumLogLevel;
            if (SymbolEqualityComparer.Default.Equals(sinkTypeAttribute.ConstructorArguments[1].Type, context.SemanticModel.Compilation.GetTypeByMetadataName("FlyingLogs.Shared.LogLevel")))
                minimumLogLevel = (LogLevel)sinkTypeAttribute.ConstructorArguments[1].Value!;
            else
                return new SinkUsageIdentity(itemType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            if (itemType == null || minimumLogLevel == 0)
                return null;

            // bool 
            var indexerType = sinkTypeAttribute.ConstructorArguments.Length < 3 ? null :
                sinkTypeAttribute.ConstructorArguments[2].Value as INamedTypeSymbol;

            return null;
        }
    }
}
