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

using SonarQube.Common;
using System;
using System.Collections.Generic;

namespace SonarQube.Bootstrapper
{
    public enum AnalysisPhase
    {
        Unspecified = 0,
        PreProcessing,
        PostProcessing
    }

    /// <summary>
    /// Returns the settings required by the bootstrapper
    /// </summary>
    public interface IBootstrapperSettings
    {
        /// <summary>
        /// Temporary analysis directory, usually .sonarqube
        /// </summary>
        string TempDirectory { get; }

        AnalysisPhase Phase { get; }

        /// <summary>
        /// The command line arguments to pass to the child process
        /// </summary>
        IEnumerable<string> ChildCmdLineArgs { get; }

        /// <summary>
        /// The level of detail that should be logged
        /// </summary>
        /// <remarks>Should be in sync with the SQ components</remarks>
        LoggerVerbosity LoggingVerbosity { get; }

        /// <summary>
        /// Path of the directory where scanner binaries are located
        /// </summary>
        string ScannerBinaryDirPath { get; }
    }
}
