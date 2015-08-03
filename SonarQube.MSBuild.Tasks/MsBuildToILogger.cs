using Microsoft.Build.Utilities;
using SonarQube.Common;

namespace SonarQube.MSBuild.Tasks
{
    internal class MsBuildToILogger : ILogger
    {
        private readonly TaskLoggingHelper logger;

        internal MsBuildToILogger(TaskLoggingHelper logger)
        {
            this.logger = logger;
        }

        void Common.ILogger.LogMessage(string message, params object[] args)
        {
            this.logger.LogMessage(Microsoft.Build.Framework.MessageImportance.Low, message, args);
        }

        void Common.ILogger.LogWarning(string message, params object[] args)
        {
            this.logger.LogWarning(message, args);
        }

        void Common.ILogger.LogError(string message, params object[] args)
        {
            this.logger.LogError(message, args);
        }
    }
}
