/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SonarScanner.MSBuild.Common
{
    /// <summary>
    /// Provides properties from the environment
    /// </summary>
    public class EnvScannerPropertiesProvider : IAnalysisPropertyProvider
    {
        public static readonly string ENV_VAR_KEY = "SONARQUBE_SCANNER_PARAMS";
        private readonly IEnumerable<Property> properties;

        public static bool TryCreateProvider(ILogger logger, out IAnalysisPropertyProvider provider)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            provider = null;
            try
            {
                provider = new EnvScannerPropertiesProvider(Environment.GetEnvironmentVariable(ENV_VAR_KEY));
                return true;
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException)
            {
                logger.LogWarning(Resources.ERROR_FailedParsePropertiesEnvVar, ENV_VAR_KEY, ex.Message);
            }
            return false;
        }

        public EnvScannerPropertiesProvider(string json)
        {
            properties = (json == null) ? new List<Property>() : ParseVar(json);
        }

        public IEnumerable<Property> GetAllProperties()
        {
            return properties;
        }

        public bool TryGetProperty(string key, out Property property)
        {
            return Property.TryGetProperty(key, properties, out property);
        }

        private IEnumerable<Property> ParseVar(string json)
        {
            return JObject.Parse(json)
                .Properties()
                .Select(p => new Property { Id = p.Name, Value = p.Value.ToString() });
        }
    }
}
