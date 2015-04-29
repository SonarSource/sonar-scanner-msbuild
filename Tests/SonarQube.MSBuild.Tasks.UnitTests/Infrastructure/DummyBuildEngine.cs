//-----------------------------------------------------------------------
// <copyright file="DummyBuildEngine.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarQube.MSBuild.Tasks.UnitTests
{
    public sealed class DummyBuildEngine : IBuildEngine
    {
        private List<BuildWarningEventArgs> warnings;
        private List<BuildErrorEventArgs> errors;
        private List<BuildMessageEventArgs> messages;

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
        public void AssertMessageExists(params string[] expected)
        {
            IEnumerable<BuildMessageEventArgs> matches = this.messages.Where(m => expected.All(e => m.Message.Contains(e)));
            Assert.AreNotEqual(0, matches.Count(), "No message contains the expected strings: {0}", string.Join(",", expected));
            Assert.AreEqual(1, matches.Count(), "More than one message contains the expected strings: {0}", string.Join(",", expected));
        }

        #endregion
    }
}