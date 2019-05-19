using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Serialization;
using MUSE.Server.Middleware;

namespace MUSE.Server
{
    public class Startup
    {
        private ILogger _logger;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add MVC framework services.

            services.AddMvc()
                .AddJsonOptions(options => options.SerializerSettings.ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            // Add Cross-Origin-Request (CORS)

            services.AddCors();

            services.AddLogging();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Startup>();
                        
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Servce static files from wwwroot.

            app.UseStaticFiles();

            // Use cross-origin and set it up for a specific URL (CORS).

            var webServerSection = Configuration.GetSection("WebServer");
            bool enableCORS = false;

            if (webServerSection != null)
            {
                enableCORS = webServerSection.GetValue<bool>("CrossOriginResourceSharing", false);
            }

            if (enableCORS)
            {
                _logger.LogInformation("Cross-Origin Resource Sharing is enabled.");

                app.UseCors(builder => builder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());
            }

            // Use WebSockets

            var webSocketOptions = new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = 4 * 1024
            };

            app.UseWebSockets(webSocketOptions);
            app.UseMiddleware<WebSocketMiddleware>();

            // Use MVC;

            app.UseMvc();
        }
    }
}
