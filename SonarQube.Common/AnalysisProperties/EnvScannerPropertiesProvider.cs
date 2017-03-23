using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SonarQube.Common
{
    /// <summary>
    /// Provides properties from the environment
    /// </summary>
    public class EnvScannerPropertiesProvider : IAnalysisPropertyProvider
    {
        private const string ENV_VAR_KEY = "SONARQUBE_SCANNER_PARAMS";
        private readonly IEnumerable<Property> properties;

        public static bool TryCreateProvider(ILogger logger, out IAnalysisPropertyProvider provider)
        {
            provider = null;
            try
            {
                provider = new EnvScannerPropertiesProvider(Environment.GetEnvironmentVariable(ENV_VAR_KEY));
                return true;
            }
            catch (Exception e)
            {
                if (logger != null)
                {
                    logger.LogError(Resources.ERROR_FailedParsePropertiesEnvVar, ENV_VAR_KEY);
                }
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

        private IEnumerable<Property> ParseVar(String json)
        {
            var props = JObject.Parse(json).Properties();
            return props.Select(p => new Property { Id = p.Name, Value = p.Value.ToString() });
        }
    }
}
