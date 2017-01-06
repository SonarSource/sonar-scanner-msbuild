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
    /// Defines symbolic names for common SonarQube properties
    /// </summary>
    public static class SonarProperties
    {
        // SonarQube server settings
        public const string HostUrl = "sonar.host.url";

        public const string SonarUserName = "sonar.login";
        public const string SonarPassword = "sonar.password";

        // Database settings
        public const string DbConnectionString = "sonar.jdbc.url";

        public const string DbUserName = "sonar.jdbc.username";
        public const string DbPassword = "sonar.jdbc.password";

        // SonarQube project settings
        public const string ProjectKey = "sonar.projectKey";
        public const string ProjectBranch = "sonar.branch";

        public const string ProjectName = "sonar.projectName";
        public const string ProjectVersion = "sonar.projectVersion";

        // Miscellaneous
        public const string SourceEncoding = "sonar.sourceEncoding";

        public const string ProjectBaseDir = "sonar.projectBaseDir";
        public const string WorkingDirectory = "sonar.working.directory";
        public const string Verbose = "sonar.verbose";
        public const string LogLevel = "sonar.log.level";

        // Default property values

        /// <summary>
        /// Regex that determines if a project is a test project or not based on its path. 
        /// This regular expression matches paths where the filename contains the 'test' token. 
        /// Regex breakdown: 
        /// [^\\]*  - everything except \
        /// test    - that contains 'test'
        /// [^\\]*$ - and it doesn't end in \
        /// </summary>
        public const string DefaultTestProjectPattern = @"[^\\]*test[^\\]*$";
    }
}