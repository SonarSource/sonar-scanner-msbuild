using Microsoft.Extensions.Configuration;

namespace AspNetCoreMvc5
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
    }
}
