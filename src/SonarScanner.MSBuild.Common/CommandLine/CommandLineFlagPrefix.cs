using System;
using System.Collections.Generic;
using System.Text;

namespace SonarScanner.MSBuild.Common.CommandLine
{
    public static class CommandLineFlagPrefix
    {
        private static readonly char[] _prefixes = new[] { '-', '/' };

        public static string[] GetPrefixFlag(string[] flags)
        {
            var flagPrefixed = new List<string>();
            foreach (var flag in flags)
            {
                foreach (var prefix in _prefixes)
                    flagPrefixed.Add($"{prefix}{flag}");
            }

            return flagPrefixed.ToArray();
        }
    }


}
