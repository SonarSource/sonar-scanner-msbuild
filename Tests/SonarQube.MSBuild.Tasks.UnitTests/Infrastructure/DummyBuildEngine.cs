/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
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