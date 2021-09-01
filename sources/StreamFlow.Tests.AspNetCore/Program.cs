using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace StreamFlow.Tests.AspNetCore
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog((_, _, config) =>
                {
                    config.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}");
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
