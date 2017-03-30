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

namespace SonarQube.Bootstrapper
{
    public static class Program
    {
        public const int ErrorCode = 1;
        public const int SuccessCode = 0;

        public static int Main(string[] args)
        {
            var logger = new ConsoleLogger(includeTimestamp: false);
            Utilities.LogAssemblyVersion(logger, typeof(Program).Assembly, Resources.AssemblyDescription);
            return Execute(args, logger);
        }

        public static int Execute(string[] args, ILogger logger)
        {
            IBootstrapperSettings settings;
            logger.SuspendOutput();

            if (ArgumentProcessor.IsHelp(args))
            {
                logger.LogInfo("");
                logger.LogInfo("Usage: ");
                logger.LogInfo("");
                logger.LogInfo("  {0} [begin|end] /key:project_key [/name:project_name] [/version:project_version] [/d:sonar.key=value] [/s:settings_file]", System.AppDomain.CurrentDomain.FriendlyName);
                logger.LogInfo("");
                logger.LogInfo("    When executing the begin phase, at least the project key must be defined.");
                logger.LogInfo("    Other properties can dynamically be defined with '/d:'. For example, '/d:sonar.verbose=true'.");
                logger.LogInfo("    A settings file can be used to define properties. If no settings file path is given, the file SonarQube.Analysis.xml in the installation directory will be used.");
                logger.LogInfo("    Only the token should be passed during the end phase, if it was used during the begin phase.");

                return SuccessCode;
            }

            if (!ArgumentProcessor.TryProcessArgs(args, logger, out settings))
            {
                logger.ResumeOutput();
                // The argument processor will have logged errors
                return ErrorCode;
            }

            IProcessorFactory processorFactory = new DefaultProcessorFactory(logger);
            BootstrapperClass bootstrapper = new BootstrapperClass(processorFactory, settings, logger);
            return bootstrapper.Execute();
        }
    }
}
