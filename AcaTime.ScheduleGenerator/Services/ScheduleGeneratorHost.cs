using AcaTime.ScheduleCommon.Services;
using AcaTime.ScheduleCommon.Utils;
using AcaTime.ScheduleGenerator.Abstract;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcaTime.ScheduleGenerator.Services
{
    internal class ScheduleGeneratorHost : IHostedService
    {
        private readonly IScheduleParameters _algorithmParametersService;
        private readonly IScheduleDataClient _scheduleDataClient;
        private readonly ScheduleBuilderService _scheduleBuilderService;
        private readonly ILogger<ScheduleGeneratorHost> _logger;
        private readonly ScriptExecutionService _scriptExecutionService;

        public ScheduleGeneratorHost(IScheduleParameters algorithmParametersService,
            IScheduleDataClient scheduleDataClient,
            ScheduleBuilderService scheduleBuilderService,
            ScriptExecutionService scriptExecutionService,
            ILogger<ScheduleGeneratorHost> logger)
        {
            _algorithmParametersService = algorithmParametersService;
            _scheduleDataClient = scheduleDataClient;
            this._scheduleBuilderService = scheduleBuilderService;
            _logger = logger;
            _scriptExecutionService = scriptExecutionService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var algoName = _algorithmParametersService.ResolveAlgorithmName();
            var algorithm = _algorithmParametersService.GetAlgorithmByName(algoName);
            var parameters = _algorithmParametersService.ResolveParameters(algorithm);
            var ignoreClassrooms = _algorithmParametersService.ResolveIgnoreClassrooms();
             
            _logger.LogInformation($"Назва алгоритму: {algoName}");
            _logger.LogInformation("Параметри алгоритму:");
            foreach (var param in parameters)
            {
                _logger.LogInformation($"{param.Key}: {param.Value}");
            }

            _logger.LogInformation($"Ігнорувати класи: {ignoreClassrooms}");
            _logger.LogInformation($"Адреса сервера: {_scheduleDataClient.ServerUrl}");
            

            _logger.LogInformation("Отримуємо дані розкладу...");
            var scheduleApiData = await _scheduleDataClient.GetScheduleDataAsync();
            _logger.LogInformation($"Отримані дані {scheduleApiData.FacultySeason.Name}");

            var scheduleData = ReverseScheduleMapper.CreateScheduleAlgorithmData(scheduleApiData);

            foreach (var estimation in scheduleData.UserFunctions.ScheduleEstimations)
            {
                estimation.Func = await _scriptExecutionService.CompileScheduleEstimationAsync(estimation.MainScript);
            }

            foreach (var estimation in scheduleData.UserFunctions.ScheduleSlotEstimations)
            {
                estimation.Func = await _scriptExecutionService.CompileSlotEstimationAsync(estimation.MainScript);
            }

            foreach (var constraint in scheduleData.UserFunctions.UnitaryConstraints)
            {
                constraint.SelectorFunc = await _scriptExecutionService.CompileSlotsSelectorAsync(constraint.SelectorScript);
                constraint.Func = await _scriptExecutionService.CompileUnarySlotValidationAsync(constraint.MainScript);
            }

            foreach (var constraint in scheduleData.UserFunctions.SlotPriorities)
            {
                constraint.Func = await _scriptExecutionService.CompileSlotPriorityEstimationAsync(constraint.MainScript);
            }

            foreach (var constraint in scheduleData.UserFunctions.SlotValidators)
            {
                constraint.Func = await _scriptExecutionService.CompileSlotValidationAsync(constraint.MainScript);
            }
            

            _logger.LogInformation("Створюємо розклад...");
            await _scheduleBuilderService.DoAll(scheduleData.FacultySeason,
                scheduleData.UserFunctions,
                algorithm,
                parameters,
                ignoreClassrooms,
                cancellationToken);
            _logger.LogInformation("Роботу завершено");

           // Завершуємо процес
           Environment.Exit(0);
         
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
