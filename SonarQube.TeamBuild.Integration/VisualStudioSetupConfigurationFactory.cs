using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Setup.Configuration;

namespace SonarQube.TeamBuild.Integration
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
                    ISetupConfiguration query;
                    return GetSetupConfiguration(out query, IntPtr.Zero) < 0 ? null : query;
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