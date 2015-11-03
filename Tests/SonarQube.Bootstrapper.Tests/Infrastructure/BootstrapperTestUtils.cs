//-----------------------------------------------------------------------
// <copyright file="BootstrapperTestUtils.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System.IO;

namespace SonarQube.Bootstrapper.Tests
{
    internal static class BootstrapperTestUtils
    {
        public static string GetDefaultPropertiesFilePath()
        {
            string defaultPropertiesFilePath = Path.Combine(Path.GetDirectoryName(typeof(Bootstrapper.Program).Assembly.Location), FilePropertyProvider.DefaultFileName);
            return defaultPropertiesFilePath;
        }

        public static void EnsureDefaultPropertiesFileDoesNotExist()
        {
            string defaultPropertiesFilePath = GetDefaultPropertiesFilePath();
            if (File.Exists(defaultPropertiesFilePath))
            {
                File.Delete(defaultPropertiesFilePath);
            };
        }
    }
}
