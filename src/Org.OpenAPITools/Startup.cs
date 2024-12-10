using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Org.OpenAPITools.Authentication;
using Org.OpenAPITools.Filters;
using Org.OpenAPITools.OpenApi;
using Org.OpenAPITools.Formatters;
using Prometheus;

namespace Org.OpenAPITools
{
    public class Startup
    {
        internal static readonly Counter CatalogEntriesCounter = Metrics.CreateCounter("catalog_entries_total", "Total number of entries in the music catalog.");

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers(options =>
                {
                    options.InputFormatters.Insert(0, new InputFormatterStream());
                })
                .AddNewtonsoftJson(opts =>
                {
                    opts.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                    opts.SerializerSettings.Converters.Add(new StringEnumConverter
                    {
                        NamingStrategy = new CamelCaseNamingStrategy()
                    });
                });

            // Добавляем метрики, задавая порт для сервера
            services.AddSingleton<KestrelMetricServer>(provider =>
            {
                return new KestrelMetricServer(port: 1234);
            });

            services
                .AddSwaggerGen(c =>
                {
                    c.EnableAnnotations(enableAnnotationsForInheritance: true, enableAnnotationsForPolymorphism: true);
                    c.SwaggerDoc("1.0.0", new OpenApiInfo
                    {
                        Title = "Sample Music Catalog API",
                        Description = "Sample Music Catalog API (ASP.NET Core 8.0)",
                        TermsOfService = new Uri("https://github.com/openapitools/openapi-generator"),
                        Contact = new OpenApiContact
                        {
                            Name = "OpenAPI-Generator Contributors",
                            Url = new Uri("https://github.com/openapitools/openapi-generator"),
                            Email = ""
                        },
                        License = new OpenApiLicense
                        {
                            Name = "NoLicense",
                            Url = new Uri("http://localhost")
                        },
                        Version = "1.0.0",
                    });
                    c.CustomSchemaIds(type => type.FriendlyId(true));
                    c.IncludeXmlComments($"{AppContext.BaseDirectory}{Path.DirectorySeparatorChar}{Assembly.GetExecutingAssembly().GetName().Name}.xml");
                    c.OperationFilter<GeneratePathParamsValidationFilter>();
                })
                .AddSwaggerGenNewtonsoftSupport();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseDefaultFiles();
            app.UseStaticFiles();

            // Запускаем сервер метрик
            var metricServer = app.ApplicationServices.GetRequiredService<KestrelMetricServer>();
            metricServer.Start();

            app.UseSwagger(c =>
            {
                c.RouteTemplate = "openapi/{documentName}/openapi.json";
            })
            .UseSwaggerUI(c =>
            {
                c.RoutePrefix = "openapi";
                c.SwaggerEndpoint("/openapi/1.0.0/openapi.json", "Sample Music Catalog API");
            });

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                // Маршрут для метрик: /metrics
                endpoints.MapMetrics();
            });
        }
    }
}
