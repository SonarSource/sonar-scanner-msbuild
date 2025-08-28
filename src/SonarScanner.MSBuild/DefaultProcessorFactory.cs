﻿/*
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

using SonarScanner.MSBuild.PostProcessor;
using SonarScanner.MSBuild.PostProcessor.Interfaces;
using SonarScanner.MSBuild.PreProcessor;
using SonarScanner.MSBuild.Shim;
using SonarScanner.MSBuild.TFS;

namespace SonarScanner.MSBuild;

public class DefaultProcessorFactory : IProcessorFactory
{
    private readonly IRuntime runtime;

    public DefaultProcessorFactory(IRuntime runtime) =>
        this.runtime = runtime;

    public IPostProcessor CreatePostProcessor() =>
        new PostProcessor.PostProcessor(
            new SonarScannerWrapper(runtime),
            new SonarEngineWrapper(runtime, new ProcessRunner(runtime.Logger)),
            runtime.Logger,
            new TargetsUninstaller(runtime.Logger),
            new TfsProcessorWrapper(runtime),
            new SonarProjectPropertiesValidator(),
            new BuildVNextCoverageReportProcessor(new BinaryToXmlCoverageReportConverter(runtime.Logger), runtime));

    public IPreProcessor CreatePreProcessor() =>
        new PreProcessor.PreProcessor(new PreprocessorObjectFactory(runtime), runtime.Logger);
}
