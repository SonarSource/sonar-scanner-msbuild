//-----------------------------------------------------------------------
// <copyright file="WriteProjectInfoFileTests.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace Sonar.MSBuild.Tasks.UnitTests
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
            Assert.AreEqual(0, this.errors.Count, "Not expecting any errors to be logged");
        }

        public void AssertNoWarnings()
        {
            Assert.AreEqual(0, this.warnings.Count, "Not expecting any warnings to be logged");
        }

        #endregion
    }
}