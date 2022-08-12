using System.Collections.Generic;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ReproAzureFunctions
{
    public class Function1
    {
        public Function1()
        {
        }

        [Function("Function1")]
        public void Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            // FIXME: This should raise S1134
        }
    }
}
