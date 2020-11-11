using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EventSourceProxy.TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) => Host
            .CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) => config
                    .AddEnvironmentVariables($"{typeof(Service).Namespace.Replace(".", ":")}:")
                    .AddCommandLine(args))
                .ConfigureServices(
                    (hostContext, services) =>
                    {
                        services.AddTransient<MyClass>(sp => new MyClassProxy(new MyClass()));
                        services.AddHostedService<Service>();
                    });
    }
}
