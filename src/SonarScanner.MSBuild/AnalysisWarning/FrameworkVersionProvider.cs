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

#if NETFRAMEWORK
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace SonarScanner.MSBuild.AnalysisWarning
{
    [ExcludeFromCodeCoverage]
    internal class FrameworkVersionProvider : IFrameworkVersionProvider
    {
        public bool IsLowerThan462FrameworkVersion() =>
           // This class was introduced in 4.6.2, so if it exists it means the runtime is >= 4.6.2
           typeof(AesManaged).Assembly.GetType("System.Security.Cryptography.DSACng", false) == null;
    }
}
#endif
