using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace FlyingLogs.Analyzers
{
    internal class NextAvailableMethodNameGenerator
    {
        private struct ComparableMethodName : IComparable<ComparableMethodName>
        {
            public readonly string Name;

            public ComparableMethodName(string name)
            {
                Name = name;
            }

            public int CompareTo(ComparableMethodName other)
            {
                string l = Name;
                string r = other.Name;

                if (l == null || r == null)
                {
                    if (l == null && r == null) return 0;
                    if (l == null) return -1;
                    return 1;
                }

                int i = 0;
                int digitCount = 0;
                while (true)
                {
                    char? cL = i < l.Length && l[l.Length - i - 1] <= '9' && l[l.Length - i - 1] >= '0' 
                        ? l[l.Length - i - 1] 
                        : null;

                    char? cR = i < r.Length && r[r.Length - i - 1] <= '9' && r[r.Length - i - 1] >= '0'
                        ? r[r.Length - i - 1]
                        : null;

                    if (cL == null || cR == null)
                    {
                        if (cL == null && cR == null)
                        {
                            if (i == 0) return 0;
                            // Same digit count. We need proper comparison of the two.
                            digitCount = i;
                            break;
                        }
                        if (cL == null) return -1;
                        return 1;
                    }

                    i++;
                }

                for (i=0; i<digitCount; i++)
                {
                    char cL = l[l.Length - digitCount + i];
                    char cR = r[r.Length - digitCount + i];
                    if (cL == cR)
                        continue;

                    return cL.CompareTo(cR);
                }

                return 0;
            }
        }

        public static void GenerateNextAvailableMethodNameProperties(
            SourceProductionContext context,
            ImmutableArray<LogMethodDetails> logs)
        {
            if (logs.Length == 0)
            {
                return;
            }

            string nameWithLargestNumber = logs.Select(l => new ComparableMethodName(l.Name)).Max().Name;
            int digitCount = nameWithLargestNumber.Reverse().TakeWhile(c => c >= '0' && c <= '9').Count();
            bool allNines = nameWithLargestNumber.Skip(nameWithLargestNumber.Length - digitCount).All(c => c == '9');
            string nextNumber = "";

            if (allNines)
            {
                nextNumber = "1" + new string('0', digitCount);
            }
            else
            {
                char[] digits = new char[digitCount];
                int carryOver = 1;
                for (int i = digitCount - 1; i>=0; i--)
                {
                    char current = nameWithLargestNumber[nameWithLargestNumber.Length - digitCount + i];
                    if (carryOver == 0)
                    {
                        digits[i] = current;
                    }
                    else
                    {
                        if (current == '9')
                            digits[i] = '0';
                        else
                        {
                            digits[i] = (char)(current + 1);
                            carryOver = 0;
                        }
                    }
                }
                nextNumber = new string(digits);
            }

            foreach(var level in Constants.LoggableLevelNames)
            {
                context.AddSource($"FlyingLogs.Log.{level}.Next.g.cs", SourceText.From($$"""
namespace FlyingLogs
{
    internal static partial class Log
    {
        public static partial class {{level}}
        {
            public const string L{{nextNumber}}_ = "This field exists to hint you a unique method name. Use it to trigger code completion and remove the underscore afterwards.";
        }
    }
}
""", Encoding.UTF8));
            }
        }
    }
}
