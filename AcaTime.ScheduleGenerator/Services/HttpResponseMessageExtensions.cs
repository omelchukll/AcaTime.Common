namespace AcaTime.ScheduleGenerator.Services
{
    /// <summary>
    /// Розширення для HttpResponseMessage для обробки помилок.
    /// </summary>
    public static class HttpResponseMessageExtensions
    {
        /// <summary>
        /// Перевіряє статус відповіді та кидає виняток з деталями у разі помилки.
        /// </summary>
        /// <param name="response">HttpResponseMessage для перевірки.</param>
        /// <param name="operationDescription">Опис операції для повідомлення про помилку.</param>
        /// <exception cref="HttpRequestException">Якщо статус відповіді не є успішним.</exception>
        public static async Task ThrowIfNotSuccessAsync(this HttpResponseMessage response, string operationDescription)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Помилка при {operationDescription}. Статус: {response.StatusCode}. Вміст: {errorContent}");
            }
        }
    }
}
