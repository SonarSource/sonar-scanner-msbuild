//-----------------------------------------------------------------------
// <copyright file="BuildLogger.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarMSBuild.Tasks.IntegrationTests
{
    /// <summary>
    /// Simple implementation of <see cref="ILogger"/> that writes all
    /// event information to the console
    /// </summary>
    internal class BuildLogger : ILogger
    {

        private IEventSource eventSource;

        private List<TargetStartedEventArgs> executedTargets;
        private List<TaskStartedEventArgs> executedTasks;

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
            this.executedTargets = new List<TargetStartedEventArgs>();
            this.executedTasks = new List<TaskStartedEventArgs>();
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
            source.TargetStarted += source_TargetStarted;
            source.TaskStarted += source_TaskStarted;
        }

        void source_TargetStarted(object sender, TargetStartedEventArgs e)
        {
            this.executedTargets.Add(e);
        }

        void source_TaskStarted(object sender, TaskStartedEventArgs e)
        {
            this.executedTasks.Add(e);
        }


        private void UnregisterEvents(IEventSource source)
        {
            source.AnyEventRaised -= Source_AnyEventRaised;
            source.TargetStarted -= source_TargetStarted;
            source.TaskStarted -= source_TaskStarted;
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


        #region Assertions

        public TargetStartedEventArgs AssertTargetExecuted(string targetName)
        {
            TargetStartedEventArgs found = this.executedTargets.FirstOrDefault(t => t.TargetName.Equals(targetName, StringComparison.InvariantCulture));
            Assert.IsNotNull(found, "Specified target was not executed: {0}", targetName);
            return found;
        }

        #endregion
    }
}
