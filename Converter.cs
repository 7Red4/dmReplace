using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DmReplace
{
    public sealed class ConvertResult
    {
        public string Text;
        public List<string> Targets = new List<string>();
    }

    public static class Converter
    {
        private static readonly Dictionary<string, string> Targets = new Dictionary<string, string>
        {
            { "twitter.com", "fxtwitter.com" },
            { "x.com", "fxtwitter.com" },
            { "instagram.com", "kkinstagram.com" },
            { "threads.com", "fixthreads.seria.moe" },
        };

        // The host must stand on its own, so netflix.com, foo@x.com, sub.x.com, x.com.tw and
        // example.com/x.com stay untouched. ASCII classes are used because \w would also match
        // CJK text, which would block a link written straight after Chinese characters.
        private static readonly Regex HostPattern = new Regex(
            @"(?<![A-Za-z0-9_.\-@/=?&%#+~])" +
            @"(?<scheme>https?://)?(?:www\.)?" +
            @"(?<host>twitter\.com|x\.com|instagram\.com|threads\.com)" +
            @"(?![A-Za-z0-9_\-@]|\.[A-Za-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static ConvertResult Convert(string input)
        {
            var result = new ConvertResult();
            result.Text = HostPattern.Replace(input ?? string.Empty, delegate(Match m)
            {
                string target = Targets[m.Groups["host"].Value.ToLowerInvariant()];
                if (!result.Targets.Contains(target))
                {
                    result.Targets.Add(target);
                }
                return m.Groups["scheme"].Value + target;
            });
            return result;
        }
    }
}
