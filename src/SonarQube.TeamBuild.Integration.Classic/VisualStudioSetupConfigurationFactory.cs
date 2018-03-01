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
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Setup.Configuration;

namespace SonarQube.TeamBuild.Integration.Classic
{
    public class VisualStudioSetupConfigurationFactory : IVisualStudioSetupConfigurationFactory
    {
        /// <summary>
        /// COM class not registered exception
        /// </summary>
        private const int REGDB_E_CLASSNOTREG = unchecked((int)0x80040154);

        [DllImport("Microsoft.VisualStudio.Setup.Configuration.Native.dll", ExactSpelling = true, PreserveSig = true)]
        private static extern int GetSetupConfiguration([MarshalAs(UnmanagedType.Interface), Out] out ISetupConfiguration configuration, IntPtr reserved);

        public ISetupConfiguration GetSetupConfigurationQuery()
        {
            ISetupConfiguration setupConfiguration = null;
            try
            {
                setupConfiguration = new SetupConfiguration();
            }
            catch (COMException ex) when (ex.HResult == REGDB_E_CLASSNOTREG)
            {
                //Attempt to access the native library
                try
                {
                    return GetSetupConfiguration(out ISetupConfiguration query, IntPtr.Zero) < 0 ? null : query;
                }
                catch (DllNotFoundException)
                {
                    //Setup configuration is not supported
                    return null;
                }
            }

            return setupConfiguration;
        }
    }
}
