namespace AcaTime.ScheduleCommon.Models.Api
{
    /// <summary>
    /// Спрощена версія DTO предмету по групі без циклічних залежностей
    /// </summary>
    public class SimplifiedGroupSubjectDTO
    {
        /// <summary>Ідентифікатор предмета по групах.</summary>
        public long Id { get; set; }

        /// <summary>Викладач, призначений на предмет.</summary>
        public SimplifiedTeacherDTO Teacher { get; set; }

        /// <summary>Предмет</summary>
        public SimplifiedSubjectDTO Subject { get; set; }

        /// <summary>Список груп та підгруп.</summary>
        public List<SimplifiedStudentGroupDTO> Groups { get; set; } = new List<SimplifiedStudentGroupDTO>();

     
    }
} 