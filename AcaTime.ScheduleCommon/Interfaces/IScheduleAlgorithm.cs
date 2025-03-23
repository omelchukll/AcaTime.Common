using AcaTime.ScheduleCommon.Models.Calc;
using AcaTime.ScheduleCommon.Models.Constraints;
using Microsoft.Extensions.Logging;

namespace AcaTime.ScheduleCommon.Interfaces
{
    /// <summary>
    /// Алгоритм розподілу розкладу
    /// </summary>
    public interface IScheduleAlgorithm
    {

        /// <summary>
        /// Запускає алгоритм розподілу розкладу
        /// </summary>
        /// <param name="root"></param>
        /// <param name="userFunctions"></param>
        /// <param name="parameters"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<List<AlgorithmResultDTO>> Run(FacultySeasonDTO root, UserFunctions userFunctions, Dictionary<string, string> parameters, ILogger logger, CancellationToken cancellationToken = default);

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