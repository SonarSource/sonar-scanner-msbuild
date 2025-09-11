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

using System.Collections.Generic;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.AnalysisConfigProcessing.Processors;

public abstract class AnalysisConfigProcessorBase(ProcessedArgs localSettings, IDictionary<string, string> serverProperties) : IAnalysisConfigProcessor
{
    public abstract void Update(AnalysisConfig config);

    protected ProcessedArgs LocalSettings { get; } = localSettings;
    protected IDictionary<string, string> ServerProperties { get; } = serverProperties;

    protected string PropertyValue(string propertyName)
    {
        if (LocalSettings.TryGetSetting(propertyName, out var localValue))
        {
            return localValue;
        }
        return ServerProperties.TryGetValue(propertyName, out var serverProperty) ? serverProperty : null;
    }

    protected static void AddSetting(AnalysisProperties properties, string id, string value)
    {
        var property = new Property(id, value);
        if (!property.ContainsSensitiveData()) // Ensures that sensitive data is not written to the configuration file.
        {
            properties.Add(new(id, value));
            if (SonarPropertiesDefault.JavaScannerMapping.TryGetValue(id, out var mappedId))
            {
                properties.Add(new(mappedId, value));
            }
        }
    }
}
