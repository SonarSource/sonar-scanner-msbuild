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

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Entry point for the executable. All work is delegated to the pre-processor class.
    /// </summary>
    public static class Program // was internal
    {
        private const int SuccessCode = 0;
        private const int ErrorCode = 1;

        private static int Main(string[] args)
        {
            ILogger logger = new ConsoleLogger(includeTimestamp: false);
            Utilities.LogAssemblyVersion(logger, typeof(Program).Assembly, Resources.AssemblyDescription);
            logger.IncludeTimestamp = true;

            IPreprocessorObjectFactory factory = new PreprocessorObjectFactory();
            TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor(factory, logger);
            bool success = preProcessor.Execute(args);

            return success ? SuccessCode : ErrorCode;
        }
    }
}