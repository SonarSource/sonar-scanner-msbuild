using Microsoft.Extensions.Hosting;

namespace AspNetCoreMvc
{
    public static class Program
    {
        // FIXME: This line contains S1134 warning

        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            null;
    }
}
