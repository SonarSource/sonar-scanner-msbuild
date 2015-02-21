//-----------------------------------------------------------------------
// <copyright file="BuildLogger.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Framework;
using System;

namespace SonarMSBuild.Tasks.IntegrationTests
{
    /// <summary>
    /// Simple implementation of <see cref="ILogger"/> that writes all
    /// event information to the console
    /// </summary>
    internal class BuildLogger : ILogger
    {

        private IEventSource eventSource;

        #region ILogger interface

        string ILogger.Parameters
        {
            get; set;
        }

        LoggerVerbosity ILogger.Verbosity
        {
            get; set;
        }

        void ILogger.Initialize(IEventSource eventSource)
        {
            this.eventSource = eventSource;
            this.RegisterEvents(eventSource);

        }

        void ILogger.Shutdown()
        {
            this.UnregisterEvents(this.eventSource);
        }

        #endregion

        #region Private methods

        private void RegisterEvents(IEventSource source)
        {
            source.AnyEventRaised += Source_AnyEventRaised;
        }

        private void UnregisterEvents(IEventSource source)
        {
            source.AnyEventRaised -= Source_AnyEventRaised;
        }

        private void Source_AnyEventRaised(object sender, BuildEventArgs e)
        {
            Log("{0}: {1}: {2}", e.Timestamp, e.SenderName, e.Message);
        }

        private void Log(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        #endregion

    }
}
