//-----------------------------------------------------------------------
// <copyright file="ICoverageReportConverter.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
namespace SonarQube.TeamBuild.Integration
{
    internal interface ICoverageReportConverter
    {
        /// <summary>
        /// Initialises the converter
        /// </summary>
        /// <returns>True if the converter was initialised successfully, otherwise false</returns>
        bool Initialize(ILogger logger);

        /// <summary>
        /// Converts the supplied binary code coverage report file to XML
        /// </summary>
        /// <param name="fullBinaryFileName">The full path to the file to be converted</param>
        /// <param name="fullXmlFileName">The name of the XML file to be created</param>
        /// <returns>True if the conversion was successful, otherwise false</returns>
        bool ConvertToXml(string fullBinaryFileName, string fullXmlFileName, ILogger logger);
    }
}
