using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcaTime.ScriptModels;

namespace AcaTime.ScheduleCommon.Models.Calc
{

    /// <summary>
    /// DTO для представлення розкладу уроку.
    /// </summary>
    public class ScheduleSlotDTO : IScheduleSlot
    {
        /// <summary>Ідентифікатор розкладеного уроку. Ініціалізується 0. При збереженні в базу не використовується. Можна міняти в алгоритмі.</summary>
        public long Id { get; set; } = 0;

        /// <summary>Номер уроку.</summary>
        public int LessonNumber { get; set; }

        /// <summary>Дата проведення уроку.</summary>
        public DateTime Date { get; set; }

        /// <summary>Номер пари в розкладі.</summary>
        public int PairNumber { get; set; }

        /// <summary>
        /// Предмет до якого належить слот
        /// </summary>
        public GroupSubjectDTO GroupSubject { get; set; }

        /// <summary>
        /// Кількість уроків для предмета в один і той же день тижня і той же номер пари разом с поточним заняттям
        /// </summary>
        public int LessonSeriesLength { get; set; } = 1;

        IGroupSubject IScheduleSlot.GroupSubject => GroupSubject;
    }

}
