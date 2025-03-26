using System;
using System.Collections.Generic;
using System.Linq;
using AcaTime.ScheduleCommon.Models;
using AcaTime.ScheduleCommon.Models.Api;
using AcaTime.ScheduleCommon.Models.Calc;
using AcaTime.ScheduleCommon.Models.Constraints;

namespace AcaTime.ScheduleCommon.Utils
{
    /// <summary>
    /// Утилітарний клас для мапінгу об'єктів розкладу без залежності від зовнішніх бібліотек (пряме мапування)
    /// </summary>
    public static class ScheduleMapper
    {
        /// <summary>
        /// Конвертує FacultySeason у спрощений SimplifiedFacultySeasonDTO
        /// </summary>
        /// <param name="facultySeasonData">Дані про семестр факультету</param>
        /// <returns>Спрощений DTO об'єкт для семестру факультету</returns>
        public static SimplifiedFacultySeasonDTO ToSimplifiedDto(FacultySeasonDTO facultySeasonData)
        {
            if (facultySeasonData == null)
                return null;

            return new SimplifiedFacultySeasonDTO
            {
                Id = facultySeasonData.Id,
                Name = facultySeasonData.Name,
                BeginSeason = facultySeasonData.BeginSeason,
                EndSeason = facultySeasonData.EndSeason,
                MaxLessonsPerDay = facultySeasonData.MaxLessonsPerDay,
                GroupSubjects = facultySeasonData.GroupSubjects?.Select(gs => ToSimplifiedDto(gs)).ToList() ?? new List<SimplifiedGroupSubjectDTO>()
            };
        }

        /// <summary>
        /// Конвертує GroupSubject у спрощений SimplifiedGroupSubjectDTO
        /// </summary>
        /// <param name="groupSubject">Дані про предмет для групи</param>
        /// <returns>Спрощений DTO об'єкт для предмету групи</returns>
        public static SimplifiedGroupSubjectDTO ToSimplifiedDto(GroupSubjectDTO groupSubject)
        {
            if (groupSubject == null)
                return null;

            return new SimplifiedGroupSubjectDTO
            {
                Id = groupSubject.Id,
                Teacher = ToSimplifiedDto(groupSubject.Teacher),
                Subject = ToSimplifiedDto(groupSubject.Subject),
                Groups = groupSubject.Groups?.Select(g => ToSimplifiedDto(g)).ToList() ?? new List<SimplifiedStudentGroupDTO>()
            };
        }

        /// <summary>
        /// Конвертує Teacher у спрощений SimplifiedTeacherDTO
        /// </summary>
        /// <param name="teacher">Дані про викладача</param>
        /// <returns>Спрощений DTO об'єкт для викладача</returns>
        public static SimplifiedTeacherDTO ToSimplifiedDto(TeacherDTO teacher)
        {
            if (teacher == null)
                return null;

            return new SimplifiedTeacherDTO
            {
                Id = teacher.Id,
                Name = teacher.Name,
                Position = teacher.Position
            };
        }

        /// <summary>
        /// Конвертує Subject у спрощений SimplifiedSubjectDTO
        /// </summary>
        /// <param name="subject">Дані про предмет</param>
        /// <returns>Спрощений DTO об'єкт для предмету</returns>
        public static SimplifiedSubjectDTO ToSimplifiedDto(SubjectDTO subject)
        {
            if (subject == null)
                return null;

            return new SimplifiedSubjectDTO
            {
                Id = subject.Id,
                Name = subject.Name,
                NumberOfLessons = subject.NumberOfLessons,
                DisciplineId = subject.DisciplineId,
                DisciplineName = subject.DisciplineName,
                SubjectTypeId = subject.SubjectTypeId,
                SubjectTypeName = subject.SubjectTypeName,
                SubjectTypeShortName = subject.SubjectTypeShortName
            };
        }

        /// <summary>
        /// Конвертує StudentLessonGroup у спрощений SimplifiedStudentGroupDTO
        /// </summary>
        /// <param name="group">Дані про групу студентів</param>
        /// <returns>Спрощений DTO об'єкт для групи студентів</returns>
        public static SimplifiedStudentGroupDTO ToSimplifiedDto(StudentLessonGroupDTO group)
        {
            if (group == null)
                return null;

            return new SimplifiedStudentGroupDTO
            {
                Id = group.Id,
                Name = group.Name,
                SubgroupName = group.SubgroupName,
                SubgroupId = group.SubgroupId,
                SubgroupVariantId = group.SubgroupVariantId,
                SubgroupVariantName = group.SubgroupVariantName,
                SubgroupCount = group.SubgroupCount,
                CourseYearId = group.CourseYearId,
                CourseYearName = group.CourseYearName,
                EducationalProgramId = group.EducationalProgramId,
                EducationalProgramName = group.EducationalProgramName
            };
        }

        /// <summary>
        /// Конвертує UserFunctions у спрощений SimplifiedUserFunctionsDTO
        /// </summary>
        /// <param name="userFunctions">Користувацькі функції для розкладу</param>
        /// <returns>Спрощений DTO об'єкт для користувацьких функцій</returns>
        public static SimplifiedUserFunctionsDTO ToSimplifiedDto(UserFunctions userFunctions)
        {
            if (userFunctions == null)
                return null;

            return new SimplifiedUserFunctionsDTO
            {
                ScheduleEstimations = MapConstraints(userFunctions.ScheduleEstimations),
                ScheduleSlotEstimations = MapConstraints(userFunctions.ScheduleSlotEstimations),
                UnitaryConstraints = MapConstraints(userFunctions.UnitaryConstraints),
                SlotPriorities = MapConstraints(userFunctions.SlotPriorities),
                SlotValidators = MapConstraints(userFunctions.SlotValidators)
            };
        }

        /// <summary>
        /// Конвертує набір обмежень у спрощені DTO. Метод працює з базовим класом BaseConstraint або будь-яким його нащадком.
        /// </summary>
        /// <typeparam name="T">Тип обмеження, що наслідує BaseConstraint</typeparam>
        /// <param name="constraints">Список обмежень</param>
        /// <returns>Список спрощених DTO для обмежень</returns>
        private static List<SimplifiedConstraintDTO> MapConstraints<T>(IEnumerable<T> constraints) where T : BaseConstraint
        {
            if (constraints == null)
                return new List<SimplifiedConstraintDTO>();

            return constraints.Select(e => new SimplifiedConstraintDTO
            {
                Id = e.Id ?? 0,
                Name = e.Name,
                Description = e.Description ?? string.Empty,
                SelectorScript = e.SelectorScript,
                MainScript = e.MainScript
            }).ToList();
        }

        /// <summary>
        /// Конвертує спрощений слот розкладу у повну модель ScheduleSlotDTO
        /// </summary>
        /// <param name="simplifiedSlot">Спрощена модель слоту</param>
        /// <returns>Повна модель слоту розкладу</returns>
        public static ScheduleSlotDTO ToScheduleSlotDto(SimplifiedScheduleSlotDTO simplifiedSlot)
        {
            if (simplifiedSlot == null)
                return null;

            return new ScheduleSlotDTO
            {
                Id = simplifiedSlot.Id,
                LessonNumber = simplifiedSlot.LessonNumber,
                Date = simplifiedSlot.Date,
                PairNumber = simplifiedSlot.PairNumber,
                LessonSeriesLength = simplifiedSlot.LessonSeriesLength,
                GroupSubject = new GroupSubjectDTO { Id = simplifiedSlot.GroupSubjectId }
            };
        }

        /// <summary>
        /// Конвертує набір спрощених слотів у набір повних моделей ScheduleSlotDTO
        /// </summary>
        /// <param name="simplifiedSlots">Список спрощених слотів</param>
        /// <returns>Список повних моделей слотів</returns>
        public static List<ScheduleSlotDTO> ToScheduleSlotDtos(IEnumerable<SimplifiedScheduleSlotDTO> simplifiedSlots)
        {
            if (simplifiedSlots == null)
                return new List<ScheduleSlotDTO>();

            return simplifiedSlots.Select(slot => ToScheduleSlotDto(slot)).ToList();
        }

        /// <summary>
        /// Створює повний DTO для даних розкладу
        /// </summary>
        /// <param name="facultySeason">Дані семестру факультету</param>
        /// <param name="userFunctions">Користувацькі функції</param>
        /// <returns>DTO з даними розкладу</returns>
        public static ScheduleDataDto CreateScheduleDataDto(FacultySeasonDTO facultySeason, UserFunctions userFunctions)
        {
            return new ScheduleDataDto
            {
                FacultySeason = ToSimplifiedDto(facultySeason),
                UserFunctions = ToSimplifiedDto(userFunctions)
            };
        }
    }
} 