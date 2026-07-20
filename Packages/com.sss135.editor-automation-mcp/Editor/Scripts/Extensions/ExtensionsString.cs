#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace com.SSS135.EditorAutomation.Runtime.Extensions
{
    public static class ExtensionsString
    {
        public static string Join(this IEnumerable<string> strings, string separator = ", ")
            => string.Join(separator, strings);

        public static string JoinExcept(this IEnumerable<string> strings, string except, string separator = ", ")
            => string.Join(separator, strings
                .Where(s => s != except));

        public static string JoinEnclose(this IEnumerable<string> strings, string separator = ", ", string enclose = "'")
            => string.Join(separator, strings
                .Select(s => $"{enclose}{s}{enclose}"));

        public static string JoinEncloseExcept(this IEnumerable<string> strings, string except, string separator = ", ", string enclose = "'")
            => string.Join(separator, strings
                .Where(s => s != except)
                .Select(s => $"{enclose}{s}{enclose}"));
    }
}
