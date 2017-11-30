﻿using Autumn.Mvc.Data.EF.SqlServer.Samples.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autumn.Mvc.Data.EF.SqlServer.Samples
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            _hostingEnvironment = env;
        }

         private IHostingEnvironment _hostingEnvironment;
       
        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddAutumn(config =>
                    config 
                        .QueryFieldName("search")
                        .Pluralized()
                        .Swagger())
                .AddAutumnEntityFrameworkCoreSqlServer<ChinookContext>(config =>
                    config
                        .ConnectionString("server=localhost;database=Chinook;User Id=sa;password=16gG1158#3")
                        .Evolve()
                );
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {

            if (!env.IsProduction())
            {
                app.UseDeveloperExceptionPage();
            }
            app
                .UseAutumn(loggerFactory)
                .UseMvc();
        }
    }
}