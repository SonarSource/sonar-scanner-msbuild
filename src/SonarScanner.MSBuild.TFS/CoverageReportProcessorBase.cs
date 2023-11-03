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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;

namespace SonarScanner.MSBuild.TFS
{
    public abstract class CoverageReportProcessorBase : ICoverageReportProcessor
    {
        private const string XmlReportFileExtension = "coveragexml";
        private readonly ICoverageReportConverter converter;

        private AnalysisConfig config;
        private IBuildSettings settings;
        private string propertiesFilePath;

        private bool successfullyInitialised;

        protected ILogger Logger { get; }

        protected CoverageReportProcessorBase(ICoverageReportConverter converter, ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
        }

        public bool Initialise(AnalysisConfig config, IBuildSettings settings, string propertiesFilePath)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.propertiesFilePath = propertiesFilePath ?? throw new ArgumentNullException(nameof(propertiesFilePath));
            successfullyInitialised = true;

            return successfullyInitialised;
        }

        public bool ProcessCoverageReports(ILogger logger)
        {
            if (!successfullyInitialised)
            {
                throw new InvalidOperationException(Resources.EX_CoverageReportProcessorNotInitialised);
            }

            if (config.GetSettingOrDefault(SonarProperties.VsTestReportsPaths, true, null, logger) != null)
            {
                Logger.LogInfo(Resources.TRX_DIAG_SkippingCoverageCheckPropertyProvided);
            }
            else
            {
                // Fetch all of the report URLs
                Logger.LogInfo(Resources.PROC_DIAG_FetchingCoverageReportInfoFromServer);

                if (TryGetTrxFiles(config, settings, out var trxPaths) &&
                    trxPaths.Any())
                {
                    using (StreamWriter sw = File.AppendText(propertiesFilePath))
                    {
                        sw.WriteLine($"{SonarProperties.VsTestReportsPaths}={string.Join(",", trxPaths.Select(c => c.Replace(@"\", @"\\")))}");
                    }
                }
            }

            var success = TryGetVsCoverageFiles(config, settings, out var vscoveragePaths);
            if (success &&
                vscoveragePaths.Any() &&
                TryConvertCoverageReports(vscoveragePaths, out var coverageReportPaths) &&
                coverageReportPaths.Any() &&
                config.GetSettingOrDefault(SonarProperties.VsCoverageXmlReportsPaths, true, null, logger) == null)
            {
                using (StreamWriter sw = File.AppendText(propertiesFilePath))
                {
                    sw.WriteLine($"{SonarProperties.VsCoverageXmlReportsPaths}={string.Join(",", coverageReportPaths.Select(c => c.Replace(@"\", @"\\")))}");
                }
            }

            return success;
        }

        protected abstract bool TryGetVsCoverageFiles(AnalysisConfig config, IBuildSettings settings, out IEnumerable<string> binaryFilePaths);

        protected abstract bool TryGetTrxFiles(AnalysisConfig config, IBuildSettings settings, out IEnumerable<string> trxFilePaths);

        private bool TryConvertCoverageReports(IEnumerable<string> vscoverageFilePaths, out IEnumerable<string> vscoveragexmlPaths)
        {
            var xmlFileNames = new List<string>();

            foreach (var vscoverageFilePath in vscoverageFilePaths)
            {
                var xmlFilePath = Path.ChangeExtension(vscoverageFilePath, XmlReportFileExtension);
                if(File.Exists(xmlFilePath))
                {
                    Logger.LogInfo(String.Format(Resources.COVXML_DIAG_FileAlreadyExist_NoConversionAttempted, vscoverageFilePath));
                }
                else
                {
                    if (!converter.ConvertToXml(vscoverageFilePath, xmlFilePath))
                    {
                        vscoveragexmlPaths = Enumerable.Empty<string>();
                        return false;
                    }
                }

                xmlFileNames.Add(xmlFilePath);
            }

            vscoveragexmlPaths = xmlFileNames;
            return true;
        }
    }
}
