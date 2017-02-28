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
using SonarQube.TeamBuild.PostProcessor;
using SonarQube.TeamBuild.PostProcessor.Interfaces;
using SonarQube.TeamBuild.PreProcessor;
using SonarScanner.Shim;
using System;
using System.IO;

namespace SonarQube.Bootstrapper
{
    public static class Program
    {
        public const int ErrorCode = 1;
        public const int SuccessCode = 0;

        public static int Main(string[] args)
        {
            var logger = new ConsoleLogger();
            Utilities.LogAssemblyVersion(logger, typeof(Program).Assembly, Resources.AssemblyDescription);
            IBootstrapperSettings settings;
            if (!ArgumentProcessor.TryProcessArgs(args, logger, out settings))
            {
                // The argument processor will have logged errors
                return ErrorCode;
            }

            IProcessorFactory processorFactory = new DefaultProcessorFactory(logger);
            BootstrapperClass bootstrapper = new BootstrapperClass(processorFactory, settings, logger);
            return bootstrapper.Execute();
        }
    }
}
