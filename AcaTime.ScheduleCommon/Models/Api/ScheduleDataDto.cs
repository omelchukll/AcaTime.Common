using AcaTime.ScheduleCommon.Models;
using AcaTime.ScheduleCommon.Models.Constraints;
using System.Collections.Generic;

namespace AcaTime.ScheduleCommon.Models.Api
{
    /// <summary>
    /// DTO для передачі даних розкладу
    /// </summary>
    public class ScheduleDataDto
    {
        /// <summary>
        /// Дані факультету та сезону
        /// </summary>
        public SimplifiedFacultySeasonDTO FacultySeason { get; set; }

        /// <summary>
        /// Функції користувача (оцінки, обмеження, тощо)
        /// </summary>
        public SimplifiedUserFunctionsDTO UserFunctions { get; set; }
    }

    /// <summary>
    /// Спрощена версія DTO користувацьких функцій без циклічних залежностей
    /// </summary>
    public class SimplifiedUserFunctionsDTO
    {
        /// <summary>
        /// Функції оцінки оптимальності розкладу - тільки назви й описи
        /// </summary>
        public List<SimplifiedConstraintDTO> ScheduleEstimations { get; set; } = new List<SimplifiedConstraintDTO>();

        /// <summary>
        /// Функції оцінки оптимальності значення слоту - тільки назви й описи
        /// </summary>
        public List<SimplifiedConstraintDTO> ScheduleSlotEstimations { get; set; } = new List<SimplifiedConstraintDTO>();

        /// <summary>
        /// Абсолютні обмеження на значення слоту - тільки назви й описи
        /// </summary>
        public List<SimplifiedConstraintDTO> UnitaryConstraints { get; set; } = new List<SimplifiedConstraintDTO>();

        /// <summary>
        /// Пріоритет визначення розкладу для слоту - тільки назви й описи
        /// </summary>
        public List<SimplifiedConstraintDTO> SlotPriorities { get; set; } = new List<SimplifiedConstraintDTO>();

        /// <summary>
        /// Обмеження на значення слоту відносно інших розподілених слотів - тільки назви й описи
        /// </summary>
        public List<SimplifiedConstraintDTO> SlotValidators { get; set; } = new List<SimplifiedConstraintDTO>();
    }
} 