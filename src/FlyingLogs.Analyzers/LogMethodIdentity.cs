using FlyingLogs.Shared;

namespace FlyingLogs
{
    internal class LogMethodIdentity : IEquatable<LogMethodIdentity?>
    {
        public LogLevel Level { get; set; }

        public string Name { get;set; }

        public string Template { get; set; }

        public LogMethodIdentity(LogLevel level, string name, string template)
        {
            Level = level;
            Name = name;
            Template = template;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as LogMethodIdentity);
        }

        public bool Equals(LogMethodIdentity? other)
        {
            return other is not null &&
                   Level == other.Level &&
                   Name == other.Name &&
                   Template == other.Template;
        }

        public override int GetHashCode()
        {
            int hashCode = 2009384174;
            hashCode = hashCode * -1521134295 + EqualityComparer<LogLevel>.Default.GetHashCode(Level);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Template);
            return hashCode;
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
