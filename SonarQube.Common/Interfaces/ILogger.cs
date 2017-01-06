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

namespace SonarQube.Common
{
    /// <summary>
    /// Level of detail for the log messages.
    /// </summary>
    /// <remarks>
    /// Does not cover warnings and errors.
    /// The levels are in step with the SonarQube verbosity levels (http://docs.sonarqube.org/display/SONAR/Server+Log+Management):
    /// Info, Debug (for advanced logs), Trace (for advanced logs and logs that might have a perf impact)
    /// </remarks>
    public enum LoggerVerbosity
    {
        /// <summary>
        /// Important messages that always get logged
        /// </summary>
        Info = 0,

        /// <summary>
        /// Advanced information messages that help in debugging scenarios
        /// </summary>
        Debug = 1
    }

    /// <summary>
    /// Simple logging interface
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Log a message with the Debug verbosity
        /// </summary>
        void LogDebug(string message, params object[] args);

        /// <summary>
        /// Log a message with the Info verbosity
        /// </summary>
        void LogInfo(string message, params object[] args);
        
        void LogWarning(string message, params object[] args);
        
        void LogError(string message, params object[] args);

        /// <summary>
        /// Gets or sets the level of detail to show in the log
        /// </summary>
        LoggerVerbosity Verbosity { get; set; }

        /// <summary>
        /// Gets or sets whether log entries are prefixed with timestamps
        /// </summary>
        bool IncludeTimestamp { get; set; }
    }
}
