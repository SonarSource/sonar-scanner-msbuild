/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.TFS.Interfaces;

namespace SonarScanner.MSBuild.TFS
{
    public abstract class CoverageReportProcessorBase : ICoverageReportProcessor
    {
        private const string XmlReportFileExtension = "coveragexml";
        private readonly ICoverageReportConverter converter;

        private AnalysisConfig config;
        private ITeamBuildSettings settings;

        private bool succesfullyInitialised = false;

        protected ILogger Logger { get; }

        protected CoverageReportProcessorBase(ICoverageReportConverter converter, ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
        }

        public bool Initialise(AnalysisConfig config, ITeamBuildSettings settings)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            this.succesfullyInitialised = this.converter.Initialize();
            return this.succesfullyInitialised;
        }

        public bool ProcessCoverageReports()
        {
            if (!this.succesfullyInitialised)
            {
                throw new InvalidOperationException(Resources.EX_CoverageReportProcessorNotInitialised);
            }

            Debug.Assert(this.config != null, "Expecting the config to not be null. Did you call Initialize() ?");

            // Fetch all of the report URLs
            Logger.LogInfo(Resources.PROC_DIAG_FetchingCoverageReportInfoFromServer);

            if (TryGetTrxFiles(this.config, this.settings, out var trxPaths) &&
                trxPaths.Any() &&
                config.GetSettingOrDefault(SonarProperties.VsTestReportsPaths, true, null) == null)
            {
                this.config.LocalSettings.Add(new Property { Id = SonarProperties.VsTestReportsPaths, Value = string.Join(",", trxPaths) });
            }

            var success = TryGetVsCoverageFiles(this.config, this.settings, out var vscoveragePaths);
            if (success &&
                vscoveragePaths.Any() &&
                TryConvertCoverageReports(vscoveragePaths, out var coverageReportPaths) &&
                coverageReportPaths.Any() &&
                config.GetSettingOrDefault(SonarProperties.VsCoverageXmlReportsPaths, true, null) == null)
            {
                this.config.LocalSettings.Add(new Property { Id = SonarProperties.VsCoverageXmlReportsPaths, Value = string.Join(",", coverageReportPaths) });
            }

            return success;
        }

        protected abstract bool TryGetVsCoverageFiles(AnalysisConfig config, ITeamBuildSettings settings, out IEnumerable<string> binaryFilePaths);

        protected abstract bool TryGetTrxFiles(AnalysisConfig config, ITeamBuildSettings settings, out IEnumerable<string> trxFilePaths);

        private bool TryConvertCoverageReports(IEnumerable<string> vscoverageFilePaths, out IEnumerable<string> vscoveragexmlPaths)
        {
            var xmlFileNames = vscoverageFilePaths.Select(x => Path.ChangeExtension(x, XmlReportFileExtension));

            Debug.Assert(!xmlFileNames.Any(x => File.Exists(x)),
                "Not expecting a file with the name of the binary-to-XML conversion output to already exist.");

            var anyFailedConversion = vscoverageFilePaths.Zip(xmlFileNames, (x, y) => new { coverage = x, xml = y })
                .Any(x => !this.converter.ConvertToXml(x.coverage, x.xml));

            if (anyFailedConversion)
            {
                vscoveragexmlPaths = Enumerable.Empty<string>();
                return false;
            }

            vscoveragexmlPaths = xmlFileNames;
            return true;
        }
    }
}
