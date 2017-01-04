using System.Diagnostics;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Ext;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System;

namespace SampleApp
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc();
            services.AddSingleton(new HttpClient());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, IApplicationLifetime applicationLifetime)
        {
#if true // New 
            loggerFactory
                .AddConsole(Configuration.GetSection("Logging"))
                .AddDebug()
                .AddElasicSearch()
                ;
#endif
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseStaticFiles();

#if true // New
            app.UseCorrelationInstrumentation();
            var logger = loggerFactory.CreateLogger("Activity");

            DiagnosticListener.AllListeners.Subscribe(delegate (DiagnosticListener listener)
            {
                if (listener.Name == "Microsoft.AspNetCore.Http")
                {
                    GC.KeepAlive("");   // Place to put a breakpoint
                    listener.Subscribe(delegate (KeyValuePair<string, object> value)
                    {
                        if (value.Key.StartsWith("Http_In"))
                            logger.LogInformation($"**** Event: {value.Key} ActivityName: {Activity.Current.OperationName} ID: {Activity.Current.Id} ");
                    });
                }
                else if (listener.Name == "HttpActivityListener")
                {
                    GC.KeepAlive("");   // Place to put a breakpoint
                    listener.Subscribe(new HttpActivityObserver(loggerFactory), s => !s.Contains("localhost:9200"));
                }
            } );
#endif 

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }

    class HttpActivityObserver : IObserver<KeyValuePair<string, object>>
    {
        private readonly ILogger<HttpActivityObserver> logger;

        public HttpActivityObserver(ILoggerFactory loggerFactory)
        {
            logger = loggerFactory.CreateLogger<HttpActivityObserver>();
        }

        public void OnCompleted(){}

        public void OnError(Exception error){}

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Key.StartsWith("Http_Out"))
                logger.LogInformation($"**** Event: {value.Key} ActivityName: {Activity.Current.OperationName} ID: {Activity.Current.Id} ");
        }
    }
}
