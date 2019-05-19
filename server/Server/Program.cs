using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetEscapades.Extensions.Logging.RollingFile;

namespace MUSE.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddCommandLine(args)
                .AddJsonFile("Server.AppSettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables(prefix: "ASPNETCORE_")
                .Build();

            string urls = "http://*:8080";

            var webServerSection = config.GetSection("WebServer");

            if (webServerSection != null)
            {
                var webServerUrls = webServerSection.GetSection("Urls");

                if (webServerUrls != null && !string.IsNullOrWhiteSpace(webServerUrls.Value))
                {
                    urls = webServerUrls.Value;
                }
            }

            var host = new WebHostBuilder()
               .UseConfiguration(config)
               .UseUrls(urls)
               .UseKestrel()
               .UseContentRoot(Directory.GetCurrentDirectory())
               .UseIISIntegration()
               .ConfigureLogging(builder =>
               {
                   builder.AddConfiguration(config.GetSection("Logging"));
                   builder.AddConsole();
                   builder.AddDebug();
                   builder.AddEventSourceLogger();
                   builder.AddFile(e => new FileLoggerOptions
                   {
                        FileName = "Log",
                        RetainedFileCountLimit = 10
                   });
               })
               .UseStartup<Startup>()
               .Build();

            host.Run();
        }
    }
}
