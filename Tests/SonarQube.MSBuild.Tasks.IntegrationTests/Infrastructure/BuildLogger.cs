/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

        public IReadOnlyList<BuildWarningEventArgs> Warnings { get { return warnings.AsReadOnly(); } }

        public IReadOnlyList<BuildErrorEventArgs> Errors { get { return errors.AsReadOnly(); } }

        #endregion Public properties

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
            executedTargets = new List<TargetStartedEventArgs>();
            executedTasks = new List<TaskStartedEventArgs>();

            warnings = new List<BuildWarningEventArgs>();
            errors = new List<BuildErrorEventArgs>();

            RegisterEvents(this.eventSource);
        }

        void ILogger.Shutdown()
        {
            UnregisterEvents(eventSource);
        }

        #endregion ILogger interface

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

        private void source_TargetStarted(object sender, TargetStartedEventArgs e)
        {
            executedTargets.Add(e);
        }

        private void source_TaskStarted(object sender, TaskStartedEventArgs e)
        {
            executedTasks.Add(e);
        }

        private void source_ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            errors.Add(e);
        }

        private void source_WarningRaised(object sender, BuildWarningEventArgs e)
        {
            warnings.Add(e);
        }

        private static void source_AnyEventRaised(object sender, BuildEventArgs e)
        {
            Log("{0}: {1}: {2}", e.Timestamp, e.SenderName, e.Message);
        }

        private static void Log(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        #endregion Private methods

        #region Assertions

        public TargetStartedEventArgs AssertTargetExecuted(string targetName)
        {
            var found = executedTargets.FirstOrDefault(t => t.TargetName.Equals(targetName, StringComparison.InvariantCulture));
            Assert.IsNotNull(found, "Specified target was not executed: {0}", targetName);
            return found;
        }

        public void AssertTargetNotExecuted(string targetName)
        {
            var found = executedTargets.FirstOrDefault(t => t.TargetName.Equals(targetName, StringComparison.InvariantCulture));
            Assert.IsNull(found, "Not expecting the target to have been executed: {0}", targetName);
        }

        public TaskStartedEventArgs AssertTaskExecuted(string taskName)
        {
            var found = executedTasks.FirstOrDefault(t => t.TaskName.Equals(taskName, StringComparison.InvariantCulture));
            Assert.IsNotNull(found, "Specified task was not executed: {0}", taskName);
            return found;
        }

        public void AssertTaskNotExecuted(string taskName)
        {
            var found = executedTasks.FirstOrDefault(t => t.TaskName.Equals(taskName, StringComparison.InvariantCulture));
            Assert.IsNull(found, "Not expecting the task to have been executed: {0}", taskName);
        }

        /// <summary>
        /// Checks that the expected tasks were executed in the specified order
        /// </summary>
        public void AssertExpectedTargetOrdering(params string[] expected)
        {
            foreach (var target in expected)
            {
                AssertTargetExecuted(target);
            }

            var actual = executedTargets.Select(t => t.TargetName).Where(t => expected.Contains(t, StringComparer.Ordinal)).ToArray();

            Console.WriteLine("Expected target order: {0}", string.Join(", ", expected));
            Console.WriteLine("Actual target order: {0}", string.Join(", ", actual));

            CollectionAssert.AreEqual(expected, actual, "Targets were not executed in the expected order");
        }

        public void AssertNoWarningsOrErrors()
        {
            AssertExpectedErrorCount(0);
            AssertExpectedWarningCount(0);
        }

        public void AssertExpectedErrorCount(int expected)
        {
            Assert.AreEqual(expected, errors.Count, "Unexpected number of errors raised");
        }

        public void AssertExpectedWarningCount(int expected)
        {
            Assert.AreEqual(expected, warnings.Count, "Unexpected number of warnings raised");
        }

        #endregion Assertions
    }
}
