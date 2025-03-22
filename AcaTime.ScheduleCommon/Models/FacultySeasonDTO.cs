using System.ComponentModel.DataAnnotations;
using AcaTime.ScriptModels;

namespace AcaTime.ScheduleCommon.Models
{
    /// <summary>
    /// DTO для представлення факультету та сезону.
    /// </summary>
    public class FacultySeasonDTO : IFacultySeason
    {
        /// <summary>Ідентифікатор факультету та сезону.</summary>
        public long Id { get; set; }

        /// <summary>Назва факультету та сезону.</summary>
        public string Name { get; set; }

        /// <summary>Список предметів, закріплених за групами.</summary>
        public List<GroupSubjectDTO> GroupSubjects { get; set; } = new List<GroupSubjectDTO>();

        [Display(Name = "Дата початку занять")]
        public DateTime BeginSeason { get; set; } 

        [Display(Name = "Дата завершення занять")]
        public DateTime EndSeason { get; set; }


        [Display(Name = "Максимаотна кількість пар в день")]
        public int MaxLessonsPerDay { get; set; }

        IReadOnlyList<IGroupSubject> IFacultySeason.GroupSubjects => GroupSubjects;
    }

}
