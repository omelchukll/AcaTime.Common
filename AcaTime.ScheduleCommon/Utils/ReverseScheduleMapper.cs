using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AcaTime.ScheduleCommon.Models;
using AcaTime.ScheduleCommon.Models.Api;
using AcaTime.ScheduleCommon.Models.Calc;
using AcaTime.ScheduleCommon.Models.Constraints;

namespace AcaTime.ScheduleCommon.Utils
{
    /// <summary>
    /// Утилітарний клас для зворотного мапування об'єктів розкладу та компіляції скриптів
    /// </summary>
    public static class ReverseScheduleMapper
    {
        #region Зворотне мапування (для алгоритму)

        /// <summary>
        /// Конвертує спрощену модель семестру факультету в повну модель для алгоритму
        /// </summary>
        /// <param name="simplifiedFacultySeason">Спрощений DTO об'єкт для семестру факультету</param>
        /// <returns>Повна модель даних семестру для алгоритму</returns>
        public static FacultySeasonDTO FromSimplifiedDto(SimplifiedFacultySeasonDTO simplifiedFacultySeason)
        {
            if (simplifiedFacultySeason == null)
                return null;

            var facultySeason = new FacultySeasonDTO
            {
                Id = simplifiedFacultySeason.Id,
                Name = simplifiedFacultySeason.Name,
                BeginSeason = simplifiedFacultySeason.BeginSeason,
                EndSeason = simplifiedFacultySeason.EndSeason,
                MaxLessonsPerDay = simplifiedFacultySeason.MaxLessonsPerDay,
                GroupSubjects = new List<GroupSubjectDTO>()
            };

            if (simplifiedFacultySeason.GroupSubjects != null)
            {
                foreach (var simplifiedGs in simplifiedFacultySeason.GroupSubjects)
                {
                    var groupSubject = FromSimplifiedDto(simplifiedGs);
                    groupSubject.Faculty = facultySeason;
                    facultySeason.GroupSubjects.Add(groupSubject);
                }
            }

            return facultySeason;
        }

        /// <summary>
        /// Конвертує спрощену модель групового предмету в повну модель для алгоритму
        /// </summary>
        /// <param name="simplifiedGs">Спрощений DTO об'єкт для групового предмету</param>
        /// <returns>Повна модель даних групового предмету для алгоритму</returns>
        public static GroupSubjectDTO FromSimplifiedDto(SimplifiedGroupSubjectDTO simplifiedGs)
        {
            if (simplifiedGs == null)
                return null;

            return new GroupSubjectDTO
            {
                Id = simplifiedGs.Id,
                Teacher = FromSimplifiedDto(simplifiedGs.Teacher),
                Subject = FromSimplifiedDto(simplifiedGs.Subject),
                Groups = simplifiedGs.Groups?.Select(g => FromSimplifiedDto(g)).ToList() ?? new List<StudentLessonGroupDTO>(),
                ScheduleSlots = new List<ScheduleSlotDTO>()
            };
        }

        /// <summary>
        /// Конвертує спрощену модель викладача в повну модель для алгоритму
        /// </summary>
        /// <param name="simplifiedTeacher">Спрощений DTO об'єкт для викладача</param>
        /// <returns>Повна модель даних викладача для алгоритму</returns>
        public static TeacherDTO FromSimplifiedDto(SimplifiedTeacherDTO simplifiedTeacher)
        {
            if (simplifiedTeacher == null)
                return null;

            return new TeacherDTO
            {
                Id = simplifiedTeacher.Id,
                Name = simplifiedTeacher.Name,
                Position = simplifiedTeacher.Position
            };
        }

        /// <summary>
        /// Конвертує спрощену модель предмета в повну модель для алгоритму
        /// </summary>
        /// <param name="simplifiedSubject">Спрощений DTO об'єкт для предмета</param>
        /// <returns>Повна модель даних предмета для алгоритму</returns>
        public static SubjectDTO FromSimplifiedDto(SimplifiedSubjectDTO simplifiedSubject)
        {
            if (simplifiedSubject == null)
                return null;

            return new SubjectDTO
            {
                Id = simplifiedSubject.Id,
                Name = simplifiedSubject.Name,
                NumberOfLessons = simplifiedSubject.NumberOfLessons,
                DisciplineId = simplifiedSubject.DisciplineId,
                DisciplineName = simplifiedSubject.DisciplineName,
                SubjectTypeId = simplifiedSubject.SubjectTypeId,
                SubjectTypeName = simplifiedSubject.SubjectTypeName,
                SubjectTypeShortName = simplifiedSubject.SubjectTypeShortName
            };
        }

        /// <summary>
        /// Конвертує спрощену модель групи студентів в повну модель для алгоритму
        /// </summary>
        /// <param name="simplifiedGroup">Спрощений DTO об'єкт для групи студентів</param>
        /// <returns>Повна модель даних групи студентів для алгоритму</returns>
        public static StudentLessonGroupDTO FromSimplifiedDto(SimplifiedStudentGroupDTO simplifiedGroup)
        {
            if (simplifiedGroup == null)
                return null;

            return new StudentLessonGroupDTO
            {
                Id = simplifiedGroup.Id,
                Name = simplifiedGroup.Name,
                SubgroupName = simplifiedGroup.SubgroupName,
                SubgroupId = simplifiedGroup.SubgroupId,
                SubgroupVariantId = simplifiedGroup.SubgroupVariantId,
                SubgroupVariantName = simplifiedGroup.SubgroupVariantName,
                SubgroupCount = simplifiedGroup.SubgroupCount,
                CourseYearId = simplifiedGroup.CourseYearId,
                CourseYearName = simplifiedGroup.CourseYearName,
                EducationalProgramId = simplifiedGroup.EducationalProgramId,
                EducationalProgramName = simplifiedGroup.EducationalProgramName
            };
        }

        /// <summary>
        /// Створює об'єкт обмеження ScheduleEstimation з спрощеного DTO без компіляції скриптів
        /// </summary>
        /// <param name="simplifiedConstraint">Спрощений DTO для обмеження</param>
        /// <returns>Об'єкт обмеження для оцінки розкладу</returns>
        public static ScheduleEstimation CreateScheduleEstimation(SimplifiedConstraintDTO simplifiedConstraint)
        {
            return new ScheduleEstimation
            {
                Id = simplifiedConstraint.Id,
                Name = simplifiedConstraint.Name,
                Description = simplifiedConstraint.Description,
                SelectorScript = simplifiedConstraint.SelectorScript,
                MainScript = simplifiedConstraint.MainScript,
                RuleType = 1
            };
        }

        /// <summary>
        /// Створює об'єкт обмеження ScheduleSlotEstimation з спрощеного DTO без компіляції скриптів
        /// </summary>
        /// <param name="simplifiedConstraint">Спрощений DTO для обмеження</param>
        /// <returns>Об'єкт обмеження для оцінки слоту розкладу</returns>
        public static ScheduleSlotEstimation CreateScheduleSlotEstimation(SimplifiedConstraintDTO simplifiedConstraint)
        {
            return new ScheduleSlotEstimation
            {
                Id = simplifiedConstraint.Id,
                Name = simplifiedConstraint.Name,
                Description = simplifiedConstraint.Description,
                SelectorScript = simplifiedConstraint.SelectorScript,
                MainScript = simplifiedConstraint.MainScript,
                RuleType = 2
            };
        }

        /// <summary>
        /// Створює об'єкт обмеження UnitaryConstraint з спрощеного DTO без компіляції скриптів
        /// </summary>
        /// <param name="simplifiedConstraint">Спрощений DTO для обмеження</param>
        /// <returns>Об'єкт унітарного обмеження</returns>
        public static UnitaryConstraint CreateUnitaryConstraint(SimplifiedConstraintDTO simplifiedConstraint)
        {
            return new UnitaryConstraint
            {
                Id = simplifiedConstraint.Id,
                Name = simplifiedConstraint.Name,
                Description = simplifiedConstraint.Description,
                SelectorScript = simplifiedConstraint.SelectorScript,
                MainScript = simplifiedConstraint.MainScript,
                RuleType = 11
            };
        }

        /// <summary>
        /// Створює об'єкт обмеження SlotPriorityEstimation з спрощеного DTO без компіляції скриптів
        /// </summary>
        /// <param name="simplifiedConstraint">Спрощений DTO для обмеження</param>
        /// <returns>Об'єкт обмеження для оцінки пріоритету слоту</returns>
        public static SlotPriorityEstimation CreateSlotPriorityEstimation(SimplifiedConstraintDTO simplifiedConstraint)
        {
            return new SlotPriorityEstimation
            {
                Id = simplifiedConstraint.Id,
                Name = simplifiedConstraint.Name,
                Description = simplifiedConstraint.Description,
                SelectorScript = simplifiedConstraint.SelectorScript,
                MainScript = simplifiedConstraint.MainScript,
                RuleType = 4
            };
        }

        /// <summary>
        /// Створює об'єкт обмеження ScheduleSlotValidation з спрощеного DTO без компіляції скриптів
        /// </summary>
        /// <param name="simplifiedConstraint">Спрощений DTO для обмеження</param>
        /// <returns>Об'єкт обмеження для валідації слоту</returns>
        public static ScheduleSlotValidation CreateScheduleSlotValidation(SimplifiedConstraintDTO simplifiedConstraint)
        {
            return new ScheduleSlotValidation
            {
                Id = simplifiedConstraint.Id,
                Name = simplifiedConstraint.Name,
                Description = simplifiedConstraint.Description,
                SelectorScript = simplifiedConstraint.SelectorScript,
                MainScript = simplifiedConstraint.MainScript,
                RuleType = 3
            };
        }

        /// <summary>
        /// Конвертує спрощений DTO для функцій користувача в повну модель для алгоритму без компіляції скриптів
        /// </summary>
        /// <param name="simplifiedUserFunctions">Спрощений DTO з функціями користувача</param>
        /// <returns>Повна модель з функціями користувача для алгоритму</returns>
        public static UserFunctions FromSimplifiedDto(SimplifiedUserFunctionsDTO simplifiedUserFunctions)
        {
            if (simplifiedUserFunctions == null)
                return null;

            return new UserFunctions
            {
                ScheduleEstimations = simplifiedUserFunctions.ScheduleEstimations?.Select(CreateScheduleEstimation).ToList() ?? new List<ScheduleEstimation>(),
                ScheduleSlotEstimations = simplifiedUserFunctions.ScheduleSlotEstimations?.Select(CreateScheduleSlotEstimation).ToList() ?? new List<ScheduleSlotEstimation>(),
                UnitaryConstraints = simplifiedUserFunctions.UnitaryConstraints?.Select(CreateUnitaryConstraint).ToList() ?? new List<UnitaryConstraint>(),
                SlotPriorities = simplifiedUserFunctions.SlotPriorities?.Select(CreateSlotPriorityEstimation).ToList() ?? new List<SlotPriorityEstimation>(),
                SlotValidators = simplifiedUserFunctions.SlotValidators?.Select(CreateScheduleSlotValidation).ToList() ?? new List<ScheduleSlotValidation>()
            };
        }

        /// <summary>
        /// Генерує слоти розкладу для кожного групового предмету
        /// </summary>
        /// <param name="facultySeason">Дані семестру факультету</param>
        public static void GenerateScheduleSlots(FacultySeasonDTO facultySeason)
        {
            if (facultySeason == null || facultySeason.GroupSubjects == null)
                return;

            foreach (var groupSubject in facultySeason.GroupSubjects)
            {
                if (groupSubject.Subject == null) 
                    continue;

                // Очищаємо поточні слоти, якщо вони були
                groupSubject.ScheduleSlots.Clear();

                // Створюємо нові слоти відповідно до кількості уроків
                for (int i = 0; i < groupSubject.Subject.NumberOfLessons; i++)
                {
                    groupSubject.ScheduleSlots.Add(new ScheduleSlotDTO
                    {
                        Id = 0, // Тимчасовий ID
                        LessonNumber = i + 1,
                        PairNumber = 0, // Буде визначено пізніше
                        Date = DateTime.MinValue, // Буде визначено алгоритмом
                        GroupSubject = groupSubject,
                        LessonSeriesLength = 1 // За замовчуванням 1 урок
                    });
                }
            }
        }

        /// <summary>
        /// Створює дані для запуску алгоритму розкладу на основі DTO, отриманого з API без компіляції скриптів
        /// </summary>
        /// <param name="scheduleData">DTO з даними розкладу</param>
        /// <returns>Кортеж з моделлю семестру і функціями користувача для алгоритму</returns>
        public static (FacultySeasonDTO facultySeason, UserFunctions userFunctions) CreateScheduleAlgorithmData(ScheduleDataDto scheduleData)
        {
            if (scheduleData == null)
                throw new ArgumentNullException(nameof(scheduleData));

            var facultySeason = FromSimplifiedDto(scheduleData.FacultySeason);
            var userFunctions = FromSimplifiedDto(scheduleData.UserFunctions);

            // Генеруємо слоти для всіх групових предметів
            GenerateScheduleSlots(facultySeason);

            return (facultySeason, userFunctions);
        }

        #endregion

        #region Компіляція скриптів

        /// <summary>
        /// Створює дані для запуску алгоритму розкладу на основі DTO з компіляцією скриптів
        /// </summary>
        /// <param name="scheduleData">DTO з даними розкладу</param>
        /// <param name="scriptService">Сервіс для компіляції скриптів</param>
        /// <returns>Кортеж з моделлю семестру і функціями користувача для алгоритму</returns>
        public static async Task<(FacultySeasonDTO facultySeason, UserFunctions userFunctions)> CreateScheduleAlgorithmDataWithScripts(
            ScheduleDataDto scheduleData, 
            ScriptExecutionService scriptService)
        {
            if (scheduleData == null)
                throw new ArgumentNullException(nameof(scheduleData));
            
            if (scriptService == null)
                throw new ArgumentNullException(nameof(scriptService));

            var facultySeason = FromSimplifiedDto(scheduleData.FacultySeason);
            var userFunctions = await FromSimplifiedDtoWithScripts(scheduleData.UserFunctions, scriptService);

            // Генеруємо слоти для всіх групових предметів
            GenerateScheduleSlots(facultySeason);

            return (facultySeason, userFunctions);
        }

        /// <summary>
        /// Конвертує спрощений DTO для функцій користувача в повну модель для алгоритму з компіляцією скриптів
        /// </summary>
        /// <param name="simplifiedUserFunctions">Спрощений DTO з функціями користувача</param>
        /// <param name="scriptService">Сервіс для компіляції скриптів</param>
        /// <returns>Повна модель з функціями користувача для алгоритму</returns>
        public static async Task<UserFunctions> FromSimplifiedDtoWithScripts(
            SimplifiedUserFunctionsDTO simplifiedUserFunctions, 
            ScriptExecutionService scriptService)
        {
            if (simplifiedUserFunctions == null)
                return null;

            if (scriptService == null)
                throw new ArgumentNullException(nameof(scriptService));

            var userFunctions = new UserFunctions
            {
                ScheduleEstimations = new List<ScheduleEstimation>(),
                ScheduleSlotEstimations = new List<ScheduleSlotEstimation>(),
                UnitaryConstraints = new List<UnitaryConstraint>(),
                SlotPriorities = new List<SlotPriorityEstimation>(),
                SlotValidators = new List<ScheduleSlotValidation>()
            };

            // Компіляція скриптів для ScheduleEstimations
            if (simplifiedUserFunctions.ScheduleEstimations != null)
            {
                foreach (var constraint in simplifiedUserFunctions.ScheduleEstimations)
                {
                    var estimation = await CreateScheduleEstimationWithScript(constraint, scriptService);
                    userFunctions.ScheduleEstimations.Add(estimation);
                }
            }

            // Компіляція скриптів для ScheduleSlotEstimations
            if (simplifiedUserFunctions.ScheduleSlotEstimations != null)
            {
                foreach (var constraint in simplifiedUserFunctions.ScheduleSlotEstimations)
                {
                    var estimation = await CreateScheduleSlotEstimationWithScript(constraint, scriptService);
                    userFunctions.ScheduleSlotEstimations.Add(estimation);
                }
            }

            // Компіляція скриптів для UnitaryConstraints
            if (simplifiedUserFunctions.UnitaryConstraints != null)
            {
                foreach (var constraint in simplifiedUserFunctions.UnitaryConstraints)
                {
                    var unitaryConstraint = await CreateUnitaryConstraintWithScript(constraint, scriptService);
                    userFunctions.UnitaryConstraints.Add(unitaryConstraint);
                }
            }

            // Компіляція скриптів для SlotPriorities
            if (simplifiedUserFunctions.SlotPriorities != null)
            {
                foreach (var constraint in simplifiedUserFunctions.SlotPriorities)
                {
                    var priority = await CreateSlotPriorityEstimationWithScript(constraint, scriptService);
                    userFunctions.SlotPriorities.Add(priority);
                }
            }

            // Компіляція скриптів для SlotValidators
            if (simplifiedUserFunctions.SlotValidators != null)
            {
                foreach (var constraint in simplifiedUserFunctions.SlotValidators)
                {
                    var validator = await CreateScheduleSlotValidationWithScript(constraint, scriptService);
                    userFunctions.SlotValidators.Add(validator);
                }
            }

            return userFunctions;
        }

        /// <summary>
        /// Створює об'єкт обмеження ScheduleEstimation з скомпільованим скриптом
        /// </summary>
        /// <param name="simplifiedConstraint">Спрощений DTO для обмеження</param>
        /// <param name="scriptService">Сервіс для компіляції скриптів</param>
        /// <returns>Завдання, що повертає об'єкт обмеження для оцінки розкладу</returns>
        public static async Task<ScheduleEstimation> CreateScheduleEstimationWithScript(
            SimplifiedConstraintDTO simplifiedConstraint, 
            ScriptExecutionService scriptService)
        {
            var estimation = CreateScheduleEstimation(simplifiedConstraint);
            estimation.Func = await scriptService.CompileScheduleEstimationAsync(simplifiedConstraint.MainScript);
            return estimation;
        }

        /// <summary>
        /// Створює об'єкт обмеження ScheduleSlotEstimation з скомпільованим скриптом
        /// </summary>
        /// <param name="simplifiedConstraint">Спрощений DTO для обмеження</param>
        /// <param name="scriptService">Сервіс для компіляції скриптів</param>
        /// <returns>Завдання, що повертає об'єкт обмеження для оцінки слоту розкладу</returns>
        public static async Task<ScheduleSlotEstimation> CreateScheduleSlotEstimationWithScript(
            SimplifiedConstraintDTO simplifiedConstraint, 
            ScriptExecutionService scriptService)
        {
            var estimation = CreateScheduleSlotEstimation(simplifiedConstraint);
            estimation.Func = await scriptService.CompileSlotEstimationAsync(simplifiedConstraint.MainScript);
            return estimation;
        }

        /// <summary>
        /// Створює об'єкт обмеження UnitaryConstraint з скомпільованим скриптом
        /// </summary>
        /// <param name="simplifiedConstraint">Спрощений DTO для обмеження</param>
        /// <param name="scriptService">Сервіс для компіляції скриптів</param>
        /// <returns>Завдання, що повертає об'єкт унітарного обмеження</returns>
        public static async Task<UnitaryConstraint> CreateUnitaryConstraintWithScript(
            SimplifiedConstraintDTO simplifiedConstraint, 
            ScriptExecutionService scriptService)
        {
            var constraint = CreateUnitaryConstraint(simplifiedConstraint);
            constraint.Func = await scriptService.CompileUnarySlotValidationAsync(simplifiedConstraint.MainScript);
            constraint.SelectorFunc = await scriptService.CompileSlotsSelectorAsync(simplifiedConstraint.SelectorScript);
            return constraint;
        }

        /// <summary>
        /// Створює об'єкт обмеження SlotPriorityEstimation з скомпільованим скриптом
        /// </summary>
        /// <param name="simplifiedConstraint">Спрощений DTO для обмеження</param>
        /// <param name="scriptService">Сервіс для компіляції скриптів</param>
        /// <returns>Завдання, що повертає об'єкт обмеження для оцінки пріоритету слоту</returns>
        public static async Task<SlotPriorityEstimation> CreateSlotPriorityEstimationWithScript(
            SimplifiedConstraintDTO simplifiedConstraint, 
            ScriptExecutionService scriptService)
        {
            var priority = CreateSlotPriorityEstimation(simplifiedConstraint);
            priority.Func = await scriptService.CompileSlotPriorityEstimationAsync(simplifiedConstraint.MainScript);
            return priority;
        }

        /// <summary>
        /// Створює об'єкт обмеження ScheduleSlotValidation з скомпільованим скриптом
        /// </summary>
        /// <param name="simplifiedConstraint">Спрощений DTO для обмеження</param>
        /// <param name="scriptService">Сервіс для компіляції скриптів</param>
        /// <returns>Завдання, що повертає об'єкт обмеження для валідації слоту</returns>
        public static async Task<ScheduleSlotValidation> CreateScheduleSlotValidationWithScript(
            SimplifiedConstraintDTO simplifiedConstraint, 
            ScriptExecutionService scriptService)
        {
            var validator = CreateScheduleSlotValidation(simplifiedConstraint);
            validator.Func = await scriptService.CompileSlotValidationAsync(simplifiedConstraint.MainScript);
            return validator;
        }

        /// <summary>
        /// Створює дані для запуску алгоритму розкладу на основі DTO з компіляцією скриптів паралельно для кращої продуктивності
        /// </summary>
        /// <param name="scheduleData">DTO з даними розкладу</param>
        /// <param name="scriptService">Сервіс для компіляції скриптів</param>
        /// <returns>Кортеж з моделлю семестру і функціями користувача для алгоритму</returns>
        public static async Task<(FacultySeasonDTO facultySeason, UserFunctions userFunctions)> CreateScheduleAlgorithmDataWithScriptsParallel(
            ScheduleDataDto scheduleData, 
            ScriptExecutionService scriptService)
        {
            if (scheduleData == null)
                throw new ArgumentNullException(nameof(scheduleData));
            
            if (scriptService == null)
                throw new ArgumentNullException(nameof(scriptService));

            var facultySeason = FromSimplifiedDto(scheduleData.FacultySeason);

            // Генеруємо слоти для всіх групових предметів (не залежить від компіляції скриптів)
            GenerateScheduleSlots(facultySeason);

            if (scheduleData.UserFunctions == null)
                return (facultySeason, new UserFunctions());

            var userFunctions = new UserFunctions();

            // Створюємо задачі для паралельної компіляції різних типів обмежень
            var compilationTasks = new List<Task>();

            // ScheduleEstimations
            if (scheduleData.UserFunctions.ScheduleEstimations != null && scheduleData.UserFunctions.ScheduleEstimations.Any())
            {
                var scheduleTasks = scheduleData.UserFunctions.ScheduleEstimations.Select(async constraint => 
                {
                    var estimation = await CreateScheduleEstimationWithScript(constraint, scriptService);
                    lock (userFunctions.ScheduleEstimations)
                    {
                        userFunctions.ScheduleEstimations.Add(estimation);
                    }
                });
                compilationTasks.AddRange(scheduleTasks);
            }

            // ScheduleSlotEstimations
            if (scheduleData.UserFunctions.ScheduleSlotEstimations != null && scheduleData.UserFunctions.ScheduleSlotEstimations.Any())
            {
                var slotEstimationTasks = scheduleData.UserFunctions.ScheduleSlotEstimations.Select(async constraint => 
                {
                    var estimation = await CreateScheduleSlotEstimationWithScript(constraint, scriptService);
                    lock (userFunctions.ScheduleSlotEstimations)
                    {
                        userFunctions.ScheduleSlotEstimations.Add(estimation);
                    }
                });
                compilationTasks.AddRange(slotEstimationTasks);
            }

            // UnitaryConstraints
            if (scheduleData.UserFunctions.UnitaryConstraints != null && scheduleData.UserFunctions.UnitaryConstraints.Any())
            {
                var unitaryTasks = scheduleData.UserFunctions.UnitaryConstraints.Select(async constraint => 
                {
                    var unitaryConstraint = await CreateUnitaryConstraintWithScript(constraint, scriptService);
                    lock (userFunctions.UnitaryConstraints)
                    {
                        userFunctions.UnitaryConstraints.Add(unitaryConstraint);
                    }
                });
                compilationTasks.AddRange(unitaryTasks);
            }

            // SlotPriorities
            if (scheduleData.UserFunctions.SlotPriorities != null && scheduleData.UserFunctions.SlotPriorities.Any())
            {
                var priorityTasks = scheduleData.UserFunctions.SlotPriorities.Select(async constraint => 
                {
                    var priority = await CreateSlotPriorityEstimationWithScript(constraint, scriptService);
                    lock (userFunctions.SlotPriorities)
                    {
                        userFunctions.SlotPriorities.Add(priority);
                    }
                });
                compilationTasks.AddRange(priorityTasks);
            }

            // SlotValidators
            if (scheduleData.UserFunctions.SlotValidators != null && scheduleData.UserFunctions.SlotValidators.Any())
            {
                var validatorTasks = scheduleData.UserFunctions.SlotValidators.Select(async constraint => 
                {
                    var validator = await CreateScheduleSlotValidationWithScript(constraint, scriptService);
                    lock (userFunctions.SlotValidators)
                    {
                        userFunctions.SlotValidators.Add(validator);
                    }
                });
                compilationTasks.AddRange(validatorTasks);
            }

            // Очікуємо завершення всіх задач компіляції
            await Task.WhenAll(compilationTasks);

            return (facultySeason, userFunctions);
        }

        #endregion
    }
} 