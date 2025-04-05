using AcaTime.ScheduleCommon.Abstract;
using AcaTime.ScheduleCommon.Models.Calc;
using AcaTime.ScheduleGenerator.Abstract;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcaTime.ScheduleGenerator.Services
{

    public class ScheduleParameters : IScheduleParameters
    {
        private readonly IEnumerable<IScheduleAlgorithm> _algorithms;
        private readonly IConfiguration _configuration;

        public ScheduleParameters(IEnumerable<IScheduleAlgorithm> algorithms, IConfiguration configuration)
        {
            _algorithms = algorithms;
            _configuration = configuration;
        }

        public IEnumerable<IScheduleAlgorithm> GetAvailableAlgorithms() => _algorithms;

        public IScheduleAlgorithm? GetAlgorithmByName(string name)
        {
            return _algorithms.FirstOrDefault(a => string.Equals(a.GetName(), name, StringComparison.OrdinalIgnoreCase));
        }

        public Dictionary<string, string> ResolveParameters(IScheduleAlgorithm algorithm)
        {
            var result = new Dictionary<string, string>();

            foreach (var param in algorithm.GetParameters())
            {


                string value = TryGetValueFromEnvOrConfig(algorithm.GetName(), param.Name);

                while (string.IsNullOrWhiteSpace(value))
                {
                    Console.WriteLine($"Введіть значення для '{param.Name}' ({param.Description}) [Тип: {param.DataType}]:");

                    value = Console.ReadLine();

                    if (param.IsRequired && string.IsNullOrWhiteSpace(value))
                    {
                        Console.WriteLine("Цей параметр є обов'язковим. Будь ласка, введіть значення.");
                        continue;
                    }

                    break;
                }

                // Optional: Validate value by type
                if (!ValidateValueByType(param.DataType, value))
                {
                    Console.WriteLine($"Неправильний формат для '{param.Name}'. Очікується {param.DataType}.");
                    throw new ArgumentException($"Неправильний формат для {param.Name}");
                }

                result[param.Name] = value ?? param.DefaultValue;
            }

            return result;
        }

        public bool ResolveIgnoreClassrooms()
        {
            var envValue = Environment.GetEnvironmentVariable("IGNORE_CLASSROOMS");
            var configValue = _configuration["IgnoreClassrooms"];

            string value = envValue ?? configValue;

            while (string.IsNullOrWhiteSpace(value))
            {
                Console.WriteLine("Ігнорувати класи? (1 - так, 0 - ні):");
                value = Console.ReadLine();
                if (!int.TryParse(value, out var parsedValue) || (parsedValue != 1 && parsedValue != 0))
                {
                    Console.WriteLine("Введіть 1 або 0.");
                    value = null;
                }
            }

            return value == "1";
        }

        private string TryGetValueFromEnvOrConfig(string algorithmName, string paramName)
        {
            var envKey = $"{algorithmName.ToUpperInvariant()}_{paramName.ToUpperInvariant()}";
            var envValue = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(envValue))
                return envValue;

            var configPath = $"Algorithms:{algorithmName}:{paramName}";
            var configValue = _configuration[configPath];
            return configValue ?? string.Empty;
        }

        private bool ValidateValueByType(AlgorithmParameterType type, string? value)
        {
            if (value == null) return false;

            return type switch
            {
                AlgorithmParameterType.String => true,
                AlgorithmParameterType.Integer => int.TryParse(value, out _),
                AlgorithmParameterType.Decimal => decimal.TryParse(value, out _),
                AlgorithmParameterType.Boolean => bool.TryParse(value, out _),
                AlgorithmParameterType.List => true, // можемо окремо валідовувати по списку можливих значень
                _ => false
            };
        }

      
        public string ResolveServerUrl()
        {
            var envValue = Environment.GetEnvironmentVariable("SERVER_URL");
            var configValue = _configuration["ServerUrl"];

            string value = envValue ?? configValue;

            while (string.IsNullOrWhiteSpace(value))
            {
                Console.WriteLine("Введіть адресу сервера:");
                value = Console.ReadLine();
            }

            return value;
        }

        public string ResolveApiKey()
        {
            var envValue = Environment.GetEnvironmentVariable("API_KEY");
            var configValue = _configuration["ApiKey"];

            string value = envValue ?? configValue;

            while (string.IsNullOrWhiteSpace(value))
            {
                Console.WriteLine("Введіть API ключ:");
                value = Console.ReadLine();
            }

            return value;
        }

        public string ResolveAlgorithmName()
        {
            var envValue = Environment.GetEnvironmentVariable("ALGORITHM_NAME");
            var configValue = _configuration["AlgorithmName"];

            var value = envValue ?? configValue;

            if (string.IsNullOrWhiteSpace(value))
            {
                Console.WriteLine("Доступні алгоритми:");
                foreach (var algo in GetAvailableAlgorithms())
                {
                    Console.WriteLine($"- {algo.GetName()}");
                }

            }

            while (string.IsNullOrWhiteSpace(value))
            {
                Console.WriteLine("Введіть назву алгоритму для генерації розкладу:");
                value = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(value) || !GetAvailableAlgorithms().Any(a => a.GetName() == value))
                {
                    Console.WriteLine("Неправильна назва алгоритму. Будь ласка, спробуйте ще раз.");
                    value = null;
                }
            }

            return value;
        }
    }
}
