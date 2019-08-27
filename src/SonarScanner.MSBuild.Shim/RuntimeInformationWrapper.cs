#if NET46
using System.Runtime.InteropServices;
#endif

namespace SonarScanner.MSBuild.Shim
{
    /* Class for ease mocking in UT */
    public class RuntimeInformationWrapper : IRuntimeInformationWrapper
    {
        public bool IsOS(System.Runtime.InteropServices.OSPlatform osPlatform)
        {
            return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(osPlatform);
        }
    }

    public interface IRuntimeInformationWrapper
    {
        bool IsOS(System.Runtime.InteropServices.OSPlatform osPlatform);
    }
}
