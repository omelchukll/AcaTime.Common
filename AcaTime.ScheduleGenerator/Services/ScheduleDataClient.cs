using AcaTime.ScheduleCommon.Abstract;
using AcaTime.ScheduleCommon.Models.Api;
using AcaTime.ScheduleCommon.Models.Calc;
using AcaTime.ScheduleCommon.Utils;
using AcaTime.ScheduleGenerator.Abstract;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace AcaTime.ScheduleGenerator.Services
{
    /// <summary>
    /// Сервіс для взаємодії з сервером розкладу через API.
    /// </summary>
    public class ScheduleDataClient : IScheduleDataClient, IScheduleBuilderDataService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ScheduleDataClient> _logger;
        private string _apiKey;

        public string ServerUrl => _httpClient.BaseAddress.ToString();

        /// <summary>
        /// Ініціалізує новий екземпляр класу ScheduleDataClient.
        /// </summary>
        /// <param name="httpClient">HttpClient для виконання запитів.</param>
        public ScheduleDataClient(IScheduleParameters algorithmService, ILogger<ScheduleDataClient> logger)
        {
            var url = algorithmService.ResolveServerUrl();
            _httpClient = new HttpClient();
            _apiKey = algorithmService.ResolveApiKey();
            _httpClient.BaseAddress = new Uri(url);
            this._logger = logger;
        }

        /// <summary>
        /// Отримує дані розкладу з сервера за API-ключем.
        /// </summary>
        /// <returns>Дані розкладу.</returns>
        /// <exception cref="HttpRequestException">Виникає, якщо запит не вдався.</exception>
        public async Task<SimplifiedScheduleDataDto> GetScheduleDataAsync()
        {
            var request = new ApiKeyRequest { ApiKey = _apiKey };
            var response = await _httpClient.PostAsJsonAsync("api/scheduledata", request);

            await response.ThrowIfNotSuccessAsync("отриманні даних розкладу");

            var data = await response.Content.ReadFromJsonAsync<SimplifiedScheduleDataDto>();
            if (data == null)
                throw new HttpRequestException("Не вдалося отримати дані розкладу: відповідь пуста.");

            return data;
        }

        /// <summary>
        /// Зберігає розклад на сервері.
        /// У разі невдачі зберігає дамп запиту.
        /// </summary>
        /// <param name="saveRequest">Запит на збереження слотів розкладу.</param>
        /// <returns>Ідентифікатор варіанта розкладу.</returns>
        /// <exception cref="HttpRequestException">Виникає, якщо запит не вдався.</exception>
        public async Task<long> SaveScheduleSlotsAsync(SaveScheduleSlotsRequest saveRequest)
        {
            saveRequest.ApiKey = _apiKey;
            var response = await _httpClient.PostAsJsonAsync("api/scheduledata/save", saveRequest);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                await SaveDumpAsync(saveRequest, errorContent);
                await response.ThrowIfNotSuccessAsync("збереженні розкладу");
            }

            var variantId = await response.Content.ReadFromJsonAsync<long?>();
            if (variantId == null)
                throw new HttpRequestException("Не вдалося зберегти розклад: відповідь пуста.");

            return variantId.Value;
        }

        private async Task SaveDumpAsync(SaveScheduleSlotsRequest saveRequest, string errorContent)
        {
            try
            {
                var basePath = AppContext.BaseDirectory;
                var dumpDir = Path.Combine(basePath, "Dumps");
                if (!Directory.Exists(dumpDir))
                {
                    Directory.CreateDirectory(dumpDir);
                }

                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var dumpPath = Path.Combine(dumpDir, $"ScheduleDump_{timestamp}.json");

                saveRequest.ApiKey = "*****"; // Не зберігаємо API-ключ у дампі

                var dump = new
                {
                    Timestamp = DateTime.UtcNow,
                    Request = saveRequest,
                    ServerError = errorContent
                };

                var json = JsonSerializer.Serialize(dump, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(dumpPath, json);
            }
            catch
            {
                // Ігноруємо будь-які помилки під час збереження дампу, щоб не зламати основний процес.
            }
        }

        public async Task<long> SaveError(long facultySeasonId, string algorithmName, string error)
        {
            _logger.LogError($"Error in schedule algorithm: {algorithmName} - {error}");
            return 0;
        }

        public async Task<long> SaveScheduleSlots(long facultySeasonId, List<ScheduleSlotDTO> slotsDto, int score, string variantName)
        {
            _logger.LogInformation($"Збереження розкладу {variantName} з {score} балами...");
           var res = await SaveScheduleSlotsAsync(new SaveScheduleSlotsRequest
           {               
               Slots = ReverseScheduleMapper.ToSimplifiedScheduleSlotDtos( slotsDto),
               Score = score,
               AlgorithmName = variantName               
           });
           _logger.LogInformation($"Розклад збережено");

           return res;
        }
    }
}
