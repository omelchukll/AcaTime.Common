using AcaTime.ScheduleCommon.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcaTime.ScheduleGenerator.Abstract
{
    /// <summary>
    /// Сервіс для керування алгоритмами генерації розкладу
    /// </summary>
    public interface IScheduleParameters
    {
        /// <summary>
        /// Отримує доступні алгоритми генерації розкладу
        /// </summary>
        /// <returns></returns>
        IEnumerable<IScheduleAlgorithm> GetAvailableAlgorithms();

        /// <summary>
        /// Отримує алгоритм генерації розкладу за його іменем
        /// </summary>
        /// <param name="name">Ім'я алгоритму</param>
        /// <returns>Алгоритм генерації розкладу або null, якщо алгоритм не знайдений</returns>
        IScheduleAlgorithm? GetAlgorithmByName(string name);

        /// <summary>
        /// Назва алгоритму
        /// </summary>
        /// <returns>Ім'я алгоритму</returns>
        string ResolveAlgorithmName();
        
        /// <summary>
        /// Розв'язує параметри алгоритму
        /// </summary>
        /// <param name="algorithm">Алгоритм генерації розкладу</param>
        /// <param name="cancellationToken">Токен відміни</param>
        /// <returns>Словник параметрів алгоритму</returns>
        Dictionary<string, string> ResolveParameters(IScheduleAlgorithm algorithm);
        
        /// <summary>
        /// Розв'язує параметри алгоритму
        /// </summary>
        /// <param name="algorithm">Алгоритм генерації розкладу</param>
        /// <param name="cancellationToken">Токен відміни</param>
        /// <returns>Словник параметрів алгоритму</returns>
        bool ResolveIgnoreClassrooms();

        /// <summary>
        /// Адреса для отримання/збереження розкладу
        /// </summary>
        /// <param name="cancellationToken">Токен відміни</param>
        /// <returns>Адреса для збереження розкладу</returns>
        string ResolveServerUrl();

        /// <summary>
        /// API ключ для отримання розкладу
        /// </summary>
        /// <param name="cancellationToken">Токен відміни</param>
        /// <returns>API ключ для отримання розкладу</returns>
        string ResolveApiKey();
    }

}
