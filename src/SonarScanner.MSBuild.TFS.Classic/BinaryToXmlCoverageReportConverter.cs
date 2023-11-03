/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2023 SonarSource SA
 * mailto: info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.CodeCoverage.IO;
using Microsoft.CodeCoverage.IO.Exceptions;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.TFS.Classic
{
    public class BinaryToXmlCoverageReportConverter : ICoverageReportConverter
    {
        private readonly ILogger logger;

        #region Public methods

        public BinaryToXmlCoverageReportConverter(ILogger logger, AnalysisConfig config)
            : this(new VisualStudioSetupConfigurationFactory(), logger, config)
        { }

        public BinaryToXmlCoverageReportConverter(IVisualStudioSetupConfigurationFactory setupConfigurationFactory,
            ILogger logger, AnalysisConfig config)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion Public methods

        #region IReportConverter interface

        public bool Initialize() => true;

        public bool ConvertToXml(string inputFilePath, string outputFilePath)
        {
            if (string.IsNullOrWhiteSpace(inputFilePath))
            {
                throw new ArgumentNullException(nameof(inputFilePath));
            }
            if (string.IsNullOrWhiteSpace(outputFilePath))
            {
                throw new ArgumentNullException(nameof(outputFilePath));
            }

            return ConvertBinaryToXml(inputFilePath, outputFilePath, logger);
        }

        #endregion IReportConverter interface

        // was internal
        public static bool ConvertBinaryToXml(string inputBinaryFilePath, string outputXmlFilePath,
            ILogger logger)
        {
            Debug.Assert(Path.IsPathRooted(inputBinaryFilePath), "Expecting the input file name to be a full absolute path");
            Debug.Assert(File.Exists(inputBinaryFilePath), "Expecting the input file to exist: " + inputBinaryFilePath);
            Debug.Assert(Path.IsPathRooted(outputXmlFilePath), "Expecting the output file name to be a full absolute path");

            var util = new CoverageFileUtility();
            try
            {
                using var dummy = new ApplicationCultureInfo(CultureInfo.InvariantCulture);
                util.ConvertCoverageFile(
                    path: inputBinaryFilePath,
                    outputPath: outputXmlFilePath,
                    includeSkippedFunctions: false,
                    includeSkippedModules: false);
            }
            catch (AggregateException aggregate) when (aggregate.InnerException is VanguardException)
            {
                logger.LogError(Resources.CONV_ERROR_ConversionToolFailed, inputBinaryFilePath);
                return false;
            }
            return true;
        }
    }
}
