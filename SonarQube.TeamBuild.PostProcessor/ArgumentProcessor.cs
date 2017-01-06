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
using System.Diagnostics;
using System.Linq;

namespace SonarQube.TeamBuild.PostProcessor
{
    /// <summary>
    /// Processes the command line arguments.
    /// Supports the standard property-related arguments automatically (i.e. /d: and /s:).
    /// The appropriate "additionalDescriptors" should be supplied to provide support for other command line arguments.
    /// </summary>
    public static class ArgumentProcessor
    {
        #region Arguments definitions

        private static IList<ArgumentDescriptor> Descriptors;

        static ArgumentProcessor()
        {
            // Initialise the set of valid descriptors.
            // To add a new argument, just add it to the list.
            Descriptors = new List<ArgumentDescriptor>();

            Descriptors.Add(CmdLineArgPropertyProvider.Descriptor);

            Debug.Assert(Descriptors.All(d => d.Prefixes != null && d.Prefixes.Any()), "All descriptors must provide at least one prefix");
            Debug.Assert(Descriptors.Select(d => d.Id).Distinct().Count() == Descriptors.Count, "All descriptors must have a unique id");
        }

        #endregion Arguments definitions

        #region Public methods

        /// <summary>
        /// Attempts to process the supplied command line arguments and
        /// reports any errors using the logger.
        /// Returns false if any parsing errors were encountered.
        /// </summary>
        public static bool TryProcessArgs(string[] commandLineArgs, ILogger logger, out IAnalysisPropertyProvider provider)
        {
            if (commandLineArgs == null)
            {
                throw new ArgumentNullException("commandLineArgs");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            provider = null;
            IEnumerable<ArgumentInstance> arguments;

            // This call will fail if there are duplicate or missing arguments
            CommandLineParser parser = new CommandLineParser(Descriptors, false /* don't allow unrecognized arguments*/);
            bool parsedOk = parser.ParseArguments(commandLineArgs, logger, out arguments);

            if (parsedOk)
            {
                // Handler for command line analysis properties
                parsedOk &= CmdLineArgPropertyProvider.TryCreateProvider(arguments, logger, out provider);

                Debug.Assert(!parsedOk || provider != null);

                if (parsedOk && !AreParsedArgumentsValid(provider, logger))
                {
                    provider = null;
                }
            }

            return provider != null;
        }

        #endregion Public methods

        #region Private methods

        /// <summary>
        /// Performs any additional validation on the parsed arguments and logs errors
        /// if necessary.
        /// </summary>
        /// <returns>True if the arguments are valid, otherwise false</returns>
        private static bool AreParsedArgumentsValid(IAnalysisPropertyProvider provider, ILogger logger)
        {
            bool areValid = true;

            foreach (Property property in provider.GetAllProperties())
            {
                if (!IsPermittedProperty(property))
                {
                    areValid = false;
                    logger.LogError(Resources.ERROR_CmdLine_DisallowedArgument, property.Id);
                }
            }

            return areValid;
        }

        /// <summary>
        /// Determines whether the supplied property is accepted by the post-processor
        /// </summary>
        public static bool IsPermittedProperty(Property property)
        {
            // Currently the post-processor only accepts command line arguments that
            // will be stripped from the the pre-processor command line
            return ProcessRunnerArguments.SensitivePropertyKeys.Any(marker => Property.AreKeysEqual(marker, property.Id));
        }

        #endregion
    }
}
