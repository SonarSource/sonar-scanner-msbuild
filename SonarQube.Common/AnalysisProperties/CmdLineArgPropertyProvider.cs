//-----------------------------------------------------------------------
// <copyright file="CmdLineArgPropertyProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarQube.Common
{
    /// <summary>
    /// Handles validating and fetch analysis properties from the command line
    /// </summary>
    public class CmdLineArgPropertyProvider : IAnalysisPropertyProvider
    {
        public const string DynamicPropertyArgumentId = "dynamic.property";

        public static readonly ArgumentDescriptor Descriptor = new ArgumentDescriptor(
            id: DynamicPropertyArgumentId, prefixes: new string[] { "/d:" }, required: false, allowMultiple: true, description: Resources.CmdLine_ArgDescription_DynamicProperty);

        private readonly IEnumerable<Property> properties;

        #region Public methods

        /// <summary>
        /// Attempts to construct and return a provider that uses analysis properties provided on the command line
        /// </summary>
        /// <param name="commandLineArguments">List of command line arguments (optional)</param>
        /// <returns>False if errors occurred when constructing the provider, otherwise true</returns>
        /// <remarks>If no properties were provided on the command line then an empty provider will be returned</remarks>
        public static bool TryCreateProvider(IEnumerable<ArgumentInstance> commandLineArguments, ILogger logger, out IAnalysisPropertyProvider provider)
        {
            if (commandLineArguments == null)
            {
                throw new ArgumentNullException("commandLineArguments");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            IEnumerable<Property> validProperties;
            if (ExtractAndValidateProperties(commandLineArguments, logger, out validProperties))
            {
                if (validProperties.Any())
                {
                    provider = new CmdLineArgPropertyProvider(validProperties);
                }
                else
                {
                    provider = EmptyPropertyProvider.Instance;
                }

                return true;
            }

            provider = null;
            return false;
        }

        #endregion


        #region IAnalysisPropertyProvider interface

        public bool TryGetProperty(string key, out Property property)
        {
            return Property.TryGetProperty(key, this.properties, out property);
        }

        public IEnumerable<Property> GetAllProperties()
        {
            return this.properties ?? Enumerable.Empty<Property>();
        }

        #endregion

        #region Private methods

        private CmdLineArgPropertyProvider(IEnumerable<Property> properties)
        {
            if (properties == null)
            {
                throw new ArgumentNullException("properties");
            }
            this.properties = properties;
        }

        #endregion

        #region Analysis properties handling

        /// <summary>
        /// Fetches and processes any analysis properties from the command line arguments
        /// </summary>
        /// <param name="analysisProperties">The extracted analysis properties, if any</param>
        /// <returns>Returns false if any errors are encountered, otherwise true</returns>
        /// <remarks>
        /// Analysis properties (/d:[key]=[value] arguments) need further processing. We need
        /// to extract the key-value pairs and check for duplicate keys.
        /// </remarks>
        private static bool ExtractAndValidateProperties(IEnumerable<ArgumentInstance> arguments, ILogger logger, out IEnumerable<Property> analysisProperties)
        {
            bool containsDuplicateProperty = false;
            bool containsAnalysisProperty = false;

            List<Property> validProperties = new List<Property>();

            foreach (ArgumentInstance argument in arguments.Where(a => a.Descriptor.Id == DynamicPropertyArgumentId))
            {
                Property property;
                if (Property.TryParse(argument.Value, out property))
                {
                    Property existing;
                    if (Property.TryGetProperty(property.Id, validProperties, out existing))
                    {
                        logger.LogError(Resources.ERROR_CmdLine_DuplicateProperty, argument.Value, existing.Value);
                        containsDuplicateProperty = true;
                    }
                    else
                    {
                        validProperties.Add(property);
                    }
                }
                else
                {
                    logger.LogError(Resources.ERROR_CmdLine_InvalidAnalysisProperty, argument.Value);
                    containsAnalysisProperty = true;
                }
            }

            // Check for named parameters that can't be set by dynamic properties
            bool containsProjectKey = ContainsNamedParameter(SonarProperties.ProjectKey, validProperties, logger, Resources.ERROR_CmdLine_MustUseProjectKey);
            bool containsProjectName = ContainsNamedParameter(SonarProperties.ProjectName, validProperties, logger, Resources.ERROR_CmdLine_MustUseProjectName);
            bool containsProjectVersion = ContainsNamedParameter(SonarProperties.ProjectVersion, validProperties, logger, Resources.ERROR_CmdLine_MustUseProjectVersion);

            // Check for others properties that can't be set
            bool containsUnsettableWorkingDirectory = ContainsUnsettableParameter(SonarProperties.WorkingDirectory, validProperties, logger);

            analysisProperties = validProperties;

            return !containsDuplicateProperty &&
                !containsAnalysisProperty &&
                !containsProjectKey &&
                !containsProjectName &&
                !containsProjectVersion &&
                !containsUnsettableWorkingDirectory;
        }

        private static bool ContainsNamedParameter(string propertyName, IEnumerable<Property> properties, ILogger logger, string errorMessage)
        {
            Property existing;
            if (Property.TryGetProperty(propertyName, properties, out existing))
            {
                logger.LogError(errorMessage);
                return true;
            }
            return false;
        }

        private static bool ContainsUnsettableParameter(string propertyName, IEnumerable<Property> properties, ILogger logger)
        {
            Property existing;
            if (Property.TryGetProperty(propertyName, properties, out existing))
            {
                logger.LogError(Resources.ERROR_CmdLine_CannotSetPropertyOnCommandLine, propertyName);
                return true;
            }
            return false;
        }

        #endregion

    }
}
