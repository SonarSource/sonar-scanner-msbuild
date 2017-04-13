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
 
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarQube.MSBuild.Tasks.UnitTests
{
    public sealed class DummyBuildEngine : IBuildEngine
    {
        private readonly List<BuildWarningEventArgs> warnings;
        private readonly List<BuildErrorEventArgs> errors;
        private readonly List<BuildMessageEventArgs> messages;

        #region Public methods

        public DummyBuildEngine()
        {
            this.warnings = new List<BuildWarningEventArgs>();
            this.errors = new List<BuildErrorEventArgs>();
            this.messages = new List<BuildMessageEventArgs>();
        }

        public IReadOnlyList<BuildErrorEventArgs> Errors { get { return this.errors.AsReadOnly(); } }

        public IReadOnlyList<BuildWarningEventArgs> Warnings { get { return this.warnings.AsReadOnly(); } }

        #endregion

        #region IBuildEngine interface

        bool IBuildEngine.BuildProjectFile(string projectFileName, string[] targetNames, System.Collections.IDictionary globalProperties, System.Collections.IDictionary targetOutputs)
        {
            throw new NotImplementedException();
        }

        int IBuildEngine.ColumnNumberOfTaskNode
        {
            get { return -2; }
        }

        bool IBuildEngine.ContinueOnError
        {
            get { return false; }
        }

        int IBuildEngine.LineNumberOfTaskNode
        {
            get { return -1; }
        }

        void IBuildEngine.LogCustomEvent(CustomBuildEventArgs e)
        {
            throw new NotImplementedException();
        }

        void IBuildEngine.LogErrorEvent(BuildErrorEventArgs e)
        {
            Console.WriteLine("BuildEngine: ERROR: {0}", e.Message);
            this.errors.Add(e);
        }

        void IBuildEngine.LogMessageEvent(BuildMessageEventArgs e)
        {
            Console.WriteLine("BuildEngine: MESSAGE: {0}", e.Message);
            this.messages.Add(e);
        }

        void IBuildEngine.LogWarningEvent(BuildWarningEventArgs e)
        {
            Console.WriteLine("BuildEngine: WARNING: {0}", e.Message);
            this.warnings.Add(e);
        }

        string IBuildEngine.ProjectFileOfTaskNode
        {
            get { return null; }
        }

        #endregion

        #region Assertions

        public void AssertNoErrors()
        {
            Assert.AreEqual(0, this.errors.Count, "Not expecting any errors to have been logged");
        }

        public void AssertNoWarnings()
        {
            Assert.AreEqual(0, this.warnings.Count, "Not expecting any warnings to have been logged");
        }

        /// <summary>
        /// Checks that a single error exists that contains all of the specified strings
        /// </summary>
        public void AssertSingleErrorExists(params string[] expected)
        {
            IEnumerable<BuildErrorEventArgs> matches = this.errors.Where(w => expected.All(e => w.Message.Contains(e)));
            Assert.AreNotEqual(0, matches.Count(), "No error contains the expected strings: {0}", string.Join(",", expected));
            Assert.AreEqual(1, matches.Count(), "More than one error contains the expected strings: {0}", string.Join(",", expected));
        }

        /// <summary>
        /// Checks that at least one message exists that contains all of the specified strings.
        /// </summary>
        public void AssertSingleMessageExists(params string[] expected)
        {
            IEnumerable<BuildMessageEventArgs> matches = this.messages.Where(m => expected.All(e => m.Message.Contains(e)));
            Assert.AreNotEqual(0, matches.Count(), "No message contains the expected strings: {0}", string.Join(",", expected));
            Assert.AreEqual(1, matches.Count(), "More than one message contains the expected strings: {0}", string.Join(",", expected));
        }

        /// <summary>
        /// Checks that at least one warning exists that contains all of the specified strings.
        /// </summary>
        public void AssertSingleWarningExists(params string[] expected)
        {
            IEnumerable<BuildWarningEventArgs> matches = this.warnings.Where(w => expected.All(e => w.Message.Contains(e)));
            Assert.AreNotEqual(0, matches.Count(), "No warning contains the expected strings: {0}", string.Join(",", expected));
            Assert.AreEqual(1, matches.Count(), "More than one warning contains the expected strings: {0}", string.Join(",", expected));
        }

        #endregion
    }
}