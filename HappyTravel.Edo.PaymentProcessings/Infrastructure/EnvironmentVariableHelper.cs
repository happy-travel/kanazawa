using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace HappyTravel.Edo.PaymentProcessings.Infrastructure
{
    public static class EnvironmentVariableHelper
    {
        public static string Get(string key, IConfiguration configuration)
        {
            var environmentVariable = configuration[key];
            if (environmentVariable is null)
                throw new Exception($"Couldn't obtain the value for '{key}' configuration key.");

            return Environment.GetEnvironmentVariable(environmentVariable) ?? throw new Exception($"Environment variable '{environmentVariable}' is not set");;
        }


        public static bool IsLocal(this IHostEnvironment hostingEnvironment) 
            => hostingEnvironment.IsEnvironment(LocalEnvironment);    
        
        
        private const string LocalEnvironment = "Local";
    }
}