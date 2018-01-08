/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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

namespace SonarQube.Common
{
    /// <summary>
    /// Provides analysis property properties
    /// </summary>
    /// <remarks>The properties could come from different sources e.g. a file, command line arguments, the SonarQube server</remarks>
    public interface IAnalysisPropertyProvider
    {
        IEnumerable<Property> GetAllProperties();

        bool TryGetProperty(string key, out Property property);
    }

    public static class AnalysisPropertyProviderExtensions
    {
        public static bool TryGetValue(this IAnalysisPropertyProvider provider, string name, out string value)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException("name");
            }

            if (provider.TryGetProperty(name, out Property property))
            {
                value = property.Value;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }
    }
}
