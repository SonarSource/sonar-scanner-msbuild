using System;

namespace SonarScanner.MSBuild.Common
{
    public static class Contract
    {
        // ToDo: Use `CallerArgumentExpression` once we update the target version to .Net 3 or later.
        // See: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.callerargumentexpressionattribute?view=net-7.0#applies-to
        public static void ThrowIfNullOrWhitespace(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(paramName);
            }
        }
    }
}
