using AcaTime.Algorithm.Alternative.Services;
using AcaTime.Algorithm.Default.Services;
using AcaTime.Algorithm.Genetic.Services;
// using AcaTime.Algorithm.Second.Services;
using AcaTime.ScheduleCommon.Abstract;
using AcaTime.ScheduleCommon.Services;
using AcaTime.ScheduleGenerator.Abstract;
using AcaTime.ScheduleGenerator.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AcaTime.ScheduleGenerator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                {

                    // Додаємо логування
                    services.AddLogging(loggingBuilder =>
                    {
                        loggingBuilder.ClearProviders(); // Очищуємо стандартні провайдери, якщо треба
                                                         // loggingBuilder.AddConsole();     // Додаємо консольний логер
                        loggingBuilder.AddSimpleConsole(options =>
                        {
                            options.SingleLine = true;
                            options.TimestampFormat = null;
                            options.IncludeScopes = false;
                            //options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Disabled;
                            options.IncludeScopes = false;
                        });
                        loggingBuilder.SetMinimumLevel(LogLevel.Debug); // Мінімальний рівень логування
                    });


                    // Реєструємо алгоритми вручну
                    services.AddSingleton<IScheduleAlgorithm, DefaultScheduleAlgorithm>();
                    services.AddSingleton<IScheduleAlgorithm, GeneticScheduleAlgorithm>();
                    services.AddSingleton<IScheduleAlgorithm, AlternativeScheduleAlgorithm>();


                    // Реєструємо сервіс для управління алгоритмами
                    services.AddSingleton<IScheduleParameters, ScheduleParameters>();


                    services.AddSingleton<ScheduleDataClient>();

                    services.AddSingleton<IScheduleDataClient, ScheduleDataClient>(x => x.GetRequiredService<ScheduleDataClient>());

                    services.AddSingleton<IScheduleBuilderDataService>(x => x.GetRequiredService<ScheduleDataClient>());

                    services.AddSingleton<ScheduleBuilderService>();

                    services.AddSingleton<ScriptExecutionService>();

                    services.AddHostedService<ScheduleGeneratorHost>();
                })
                .Build();

            await host.RunAsync();
        }
    }
}
