using FlyingLogs.Shared;

using Microsoft.CodeAnalysis;

namespace FlyingLogs
{
    internal class LogMethodIdentity : IEquatable<LogMethodIdentity?>
    {
        public LogLevel Level { get; set; }

        public string Name { get;set; }

        public string Template { get; set; }

        public ITypeSymbol[] ArgumentTypes { get; set; }

        public LogMethodIdentity(LogLevel level, string name, string template, ITypeSymbol[] argumentTypes)
        {
            Level = level;
            Name = name;
            Template = template;
            ArgumentTypes = argumentTypes;
        }

        public override int GetHashCode()
        {
            int hashCode = -1685887027;
            hashCode = hashCode * -1521134295 + Level.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Template);
            hashCode = hashCode * -1521134295 + EqualityComparer<ITypeSymbol[]>.Default.GetHashCode(ArgumentTypes);
            return hashCode;
        }

        public bool Equals(LogMethodIdentity? other)
        {
            return other is not null &&
                   Level == other.Level &&
                   Name == other.Name &&
                   Template == other.Template &&
                   EqualityComparer<ITypeSymbol[]>.Default.Equals(ArgumentTypes, other.ArgumentTypes);
        }

        public override bool Equals(object? obj)
        {
            return obj is LogMethodIdentity identity &&
                   Level == identity.Level &&
                   Name == identity.Name &&
                   Template == identity.Template &&
                   EqualityComparer<ITypeSymbol[]>.Default.Equals(ArgumentTypes, identity.ArgumentTypes);
        }

        public static bool operator ==(LogMethodIdentity? left, LogMethodIdentity? right)
        {
            return EqualityComparer<LogMethodIdentity>.Default.Equals(left!, right!);
        }

        public static bool operator !=(LogMethodIdentity? left, LogMethodIdentity? right)
        {
            return !(left == right);
        }
    }
}
