using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SonarScanner.MSBuild.Common
{
    public sealed class ApplicationCultureInfo : IDisposable
    {
        private readonly CultureInfo defaultThreadCurrentCulture;
        private readonly CultureInfo defaultThreadCurrentUICulture;

        public ApplicationCultureInfo(CultureInfo setCultureInfo)
        {
            defaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentCulture;
            defaultThreadCurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture;
            CultureInfo.DefaultThreadCurrentCulture = setCultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = setCultureInfo;
        }

        public void Dispose()
        {
            CultureInfo.DefaultThreadCurrentCulture = defaultThreadCurrentCulture;
            CultureInfo.DefaultThreadCurrentUICulture = defaultThreadCurrentUICulture;
        }
    }
}
