using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using Serilog;

namespace Org.OpenAPITools
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("logs/myapp.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                Log.Information("Starting up");
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application start-up failed");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    services.AddOpenTelemetryTracing((TracerProviderBuilder builder) =>
                    {
                        builder
                            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyApp"))
                            .AddAspNetCoreInstrumentation()
                            .AddHttpClientInstrumentation()
                            .AddSource("MyAppTracer") // Источник для кастомных спанов
                            .AddOtlpExporter(options =>
                            {
                                options.Endpoint = new Uri("http://192.168.1.162:4318/v1/traces"); // HTTP-протокол вместо gRPC
                                options.Protocol = OtlpExportProtocol.HttpProtobuf; // Указываем HTTP-протокол
                                Console.WriteLine("Tracing initialized and configured to send to http://192.168.1.162:4318/v1/traces");
                            });
                    });
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>()
                              .UseUrls("http://0.0.0.0:8080/");
                });
    }
}
