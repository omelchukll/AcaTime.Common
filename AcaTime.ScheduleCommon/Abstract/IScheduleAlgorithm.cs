using AcaTime.ScheduleCommon.Models.Calc;
using AcaTime.ScheduleCommon.Models.Constraints;
using Microsoft.Extensions.Logging;

namespace AcaTime.ScheduleCommon.Abstract
{
    /// <summary>
    /// Алгоритм розподілу розкладу
    /// </summary>
    public interface IScheduleAlgorithm
    {

        /// <summary>
        /// Запускає алгоритм розподілу розкладу
        /// </summary>        
        Task<List<AlgorithmResultDTO>> Run(FacultySeasonDTO root, UserFunctions userFunctions, Dictionary<string, string> parameters, bool ignoreClassrooms, ILogger logger, CancellationToken cancellationToken = default);

        /// <summary>
        /// Отримує статистику роботи алгоритму
        /// </summary>
        /// <returns></returns>
        string GetStatistics();

        /// <summary>
        /// Ім'я алгоритму
        /// </summary>
        /// <returns></returns>
        string GetName();   

        /// <summary>
        /// Отримує список параметрів, які використовує алгоритм
        /// </summary>
        /// <returns>Список параметрів з описом, типом та значенням за замовчуванням</returns>
        List<AlgorithmParameterDTO> GetParameters();
    }
}