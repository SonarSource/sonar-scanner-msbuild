using System;
using System.Linq;
using System.Net;

namespace SonarScanner.MSBuild.Common
{
    public static class WebUtils
    {
        public static string Escape(string format, params string[] args) =>
            string.Format(format, args.Select(WebUtility.UrlEncode).ToArray());
    }
}
