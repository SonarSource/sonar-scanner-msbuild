/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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
using System.Globalization;
using System.IO;
using Microsoft.CodeCoverage.IO;
using Microsoft.CodeCoverage.IO.Exceptions;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.TFS.Classic;

public class BinaryToXmlCoverageReportConverter : ICoverageReportConverter
{
    private readonly ILogger logger;

    public BinaryToXmlCoverageReportConverter(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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
        if (!File.Exists(inputFilePath))
        {
            logger.LogError(Resources.CONV_ERROR_InputFileNotFound, inputFilePath);
            return false;
        }

        var util = new CoverageFileUtility();
        try
        {
            // Temporary work around until https://github.com/microsoft/codecoverage/issues/63 is fixed
            using var dummy = new ApplicationCultureInfo(CultureInfo.InvariantCulture);
            logger.LogDebug(Resources.CONV_DIAG_ConvertCoverageFile, inputFilePath, outputFilePath);
            util.ConvertCoverageFile(path: inputFilePath, outputPath: outputFilePath, includeSkippedFunctions: false, includeSkippedModules: false);
        }
        catch (AggregateException aggregate) when (aggregate.InnerException is VanguardException)
        {
            logger.LogError(Resources.CONV_ERROR_ConversionToolFailed, inputFilePath);
            return false;
        }
        return true;
    }
}
