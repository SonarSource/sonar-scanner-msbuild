//-----------------------------------------------------------------------
// <copyright file="BuildLogger.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarQube.MSBuild.Tasks.IntegrationTests
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
        private List<BuildErrorEventArgs> errors;
        private List<BuildWarningEventArgs> warnings;


        #region Public properties

        public IReadOnlyList<BuildWarningEventArgs> Warnings { get { return this.warnings.AsReadOnly(); } }

        public IReadOnlyList<BuildErrorEventArgs> Errors { get { return this.errors.AsReadOnly(); } }

        #endregion

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

            this.warnings = new List<BuildWarningEventArgs>();
            this.errors = new List<BuildErrorEventArgs>();
            
            this.RegisterEvents(this.eventSource);
        }


        void ILogger.Shutdown()
        {
            this.UnregisterEvents(this.eventSource);
        }

        #endregion

        #region Private methods

        private void RegisterEvents(IEventSource source)
        {
            source.AnyEventRaised += source_AnyEventRaised;
            source.TargetStarted += source_TargetStarted;
            source.TaskStarted += source_TaskStarted;
            source.ErrorRaised += source_ErrorRaised;
            source.WarningRaised += source_WarningRaised;
        }

        private void UnregisterEvents(IEventSource source)
        {
            source.AnyEventRaised -= source_AnyEventRaised;
            source.TargetStarted -= source_TargetStarted;
            source.TaskStarted -= source_TaskStarted;
            source.ErrorRaised -= source_ErrorRaised;
            source.WarningRaised -= source_WarningRaised;
        }

        void source_TargetStarted(object sender, TargetStartedEventArgs e)
        {
            this.executedTargets.Add(e);
        }

        void source_TaskStarted(object sender, TaskStartedEventArgs e)
        {
            this.executedTasks.Add(e);
        }

        void source_ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            this.errors.Add(e);
        }

        void source_WarningRaised(object sender, BuildWarningEventArgs e)
        {
            this.warnings.Add(e);
        }

        private void source_AnyEventRaised(object sender, BuildEventArgs e)
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

        public void AssertTargetNotExecuted(string targetName)
        {
            TargetStartedEventArgs found = this.executedTargets.FirstOrDefault(t => t.TargetName.Equals(targetName, StringComparison.InvariantCulture));
            Assert.IsNull(found, "Not expecting the target to have been executed: {0}", targetName);
        }

        public TaskStartedEventArgs AssertTaskExecuted(string taskName)
        {
            TaskStartedEventArgs found = this.executedTasks.FirstOrDefault(t => t.TaskName.Equals(taskName, StringComparison.InvariantCulture));
            Assert.IsNotNull(found, "Specified task was not executed: {0}", taskName);
            return found;
        }

        public void AssertTaskNotExecuted(string taskName)
        {
            TaskStartedEventArgs found = this.executedTasks.FirstOrDefault(t => t.TaskName.Equals(taskName, StringComparison.InvariantCulture));
            Assert.IsNull(found, "Not expecting the task to have been executed: {0}", taskName);
        }

        public void AssertNoWarningsOrErrors()
        {
            AssertExpectedErrorCount(0);
            AssertExpectedWarningCount(0);
        }

        public void AssertExpectedErrorCount(int expected)
        {
            Assert.AreEqual(expected, this.errors.Count, "Unexpected number of errors raised");
        }

        public void AssertExpectedWarningCount(int expected)
        {
            Assert.AreEqual(expected, this.warnings.Count, "Unexpected number of warnings raised");
        }

        #endregion
    }
}
