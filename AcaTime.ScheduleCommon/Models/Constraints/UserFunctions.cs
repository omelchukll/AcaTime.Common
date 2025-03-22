using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcaTime.ScheduleCommon.Models.Constraints
{
    /// <summary>
    /// Функції користувача
    /// </summary>
    public class UserFunctions
    {
        /// <summary>
        /// Функції оцінки оптимальності розкладу
        /// </summary>
        public List<ScheduleEstimation> ScheduleEstimations { get; set; }

        /// <summary>
        /// Функції оцінки оптимальності значення слоту 
        /// </summary>
        public List<ScheduleSlotEstimation> ScheduleSlotEstimations { get; set; }

        /// <summary>
        /// Абсолютні обмеження на значення слоту
        /// </summary>
        public List<UnitaryConstraint> UnitaryConstraints { get; set; }

        /// <summary>
        /// Пріоритет визначення розкладу для слоту 
        /// </summary>
        public List<SlotPriorityEstimation> SlotPriorities { get; set; }

        /// <summary>
        /// Обмеження на значення слоту відносно інших розподілених слотів
        /// </summary>
        public List<ScheduleSlotValidation> SlotValidators { get; set; }

    }
}
