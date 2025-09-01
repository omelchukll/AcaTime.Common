// using AcaTime.ScheduleCommon.Models.Calc;
// using AcaTime.ScheduleCommon.Models.Constraints;
// using AcaTime.ScheduleCommon.Utils;
// using AcaTime.ScriptModels;
// using Microsoft.Extensions.Logging;
// using System.Collections.Generic;
// using System.ComponentModel.Design.Serialization;
// using System.Net.Security;
// using System.Runtime.CompilerServices;
// using AcaTime.Algorithm.Genetic.Models;
// using AcaTime.Algorithm.Genetic.Models.Genetic;
// using AcaTime.Algorithm.Genetic.Services.Calc;
// using AcaTime.Algorithm.Genetic.Utils;
//
// namespace AcaTime.Algorithm.Genetic.Services
// {
//     /// <summary>
//     /// Клас для реалізації алгоритму розкладу.
//     /// </summary>
//     public class SecondScheduleAlgorithmUnit
//     {
//         public string Name => "Genetic Algorithm";
//         private const int PopulationSize = 10;
//         private const int MaxGenerations = 10;
//         private readonly Random _random = new();
//         /// тимчасові поля
//         public List<ScheduleSlotDTO> initialPopulation { get; set; }
//
//         /// <summary>
//         /// Дані для розкладу
//         /// </summary>
//         public FacultySeasonDTO Root { get; private set; }
//
//         /// <summary>
//         /// Оцінки для розкладу
//         /// </summary>
//         public UserFunctions UserFunctions { get; set; }
//
//         public AlgorithmParams Parameters { get; internal set; }
//         private string algorithmName = $"alg-{Guid.NewGuid()}";
//         internal ILogger logger;
//         private CancellationToken cancelToken;
//
//         public bool ignoreClassrooms { get; private set; }
//         public Dictionary<IScheduleSlot, SlotTracker> Slots { get; internal set; }
//
//         // для заміру часа виконання
//         public DebugData DebugData { get; set; } = new DebugData("none");
//
//         // додатковий кеш для прискорення деякіх функцій, клонується в Clone
//         internal Dictionary<long, List<SlotTracker>> teacherSlots;
//         internal Dictionary<long, List<SlotTracker>> groupsSlots;
//         internal List<SlotTracker> FirstTrackers;
//
//         // приватний кеш
//         private Dictionary<int, List<SlotTracker>> slotsByStep = new Dictionary<int, List<SlotTracker>>(); // для зберігання слотів по крокам
//         private Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>> assignedSlotsByTeacherDate = new Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>>();
//         private Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>> assignedSlotsByGroupDate = new Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>>();
//         private HashSet<SlotTracker> unassignedFirstSlots;
//         private Dictionary<long, List<SlotTracker>> firstSlotsByGroupSubjects;
//         private Dictionary<DateTime, Dictionary<int, Dictionary<ClassroomDTO, ScheduleSlotDTO>>> assignedClassrooms = new Dictionary<DateTime, Dictionary<int, Dictionary<ClassroomDTO, ScheduleSlotDTO>>>();
//
//
//         private int lastStep = 0; // для відстеження останнього кроку рекурсії
//         private ScheduleSlotDTO lastProcessingSlot = null; // для відстеження останнього слоту в обробці
//         private ScheduleSlotDTO lastAssignedSlot = null; // для відстеження останнього призначеного слоту
//         
//         
//         /// <summary>
//         /// Налаштування алгоритму
//         /// </summary>
//         /// <param name="root"></param>
//         /// <param name="logger"></param>
//         /// <param name="userFunctions"></param>
//         /// <param name="parameters"></param>
//         public void Setup(FacultySeasonDTO root, ILogger logger, UserFunctions userFunctions, AlgorithmParams parameters)
//         {
//             Root = root;
//             this.logger = logger;
//             UserFunctions = userFunctions;
//             Parameters = parameters;
//         }
//
//         private void PreparePrivateCash()
//         {
//             firstSlotsByGroupSubjects = FirstTrackers
//                 .Where(x => !x.IsAssigned && x.IsFirstTrackerInSeries)
//                 .GroupBy(s => s.ScheduleSlot.GroupSubject.Id)
//             .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SeriesId).ToList());
//
//             unassignedFirstSlots = firstSlotsByGroupSubjects.Values.Select(x => x.First())
//                 .ToHashSet();
//         }
//
//
//         #region Основні функції алгоритму
//
//         /// <summary>
//         /// Виконання алгоритму.
//         /// </summary>
//         /// <param name="token"></param>
//         /// <returns></returns>
//         public async Task<AlgorithmResultDTO> Run(CancellationToken token, bool ignoreClassrooms)
//         {
//             cancelToken = token;
//             this.ignoreClassrooms = ignoreClassrooms;
//             DebugData = new DebugData(algorithmName, true);
//
//             PreparePrivateCash();
//             
//             bool success = false;
//             try
//             {
//                 // Запуск рекурсії для пошуку розкладу
//                 success = AssignSlots(1);
//
//             }
//             catch (OperationCanceledException)
//             {
//                 logger.LogWarning($"{algorithmName}: Скасування роботи алгоритму"); 
//                  logger.LogWarning($"   Останній шаг: {lastStep}");
//                  logger.LogWarning($"   Останній слот: {lastProcessingSlot.GroupSubject.Subject.Name} {lastProcessingSlot.GroupSubject.Groups.First().EducationalProgramName} {String.Join(",", lastProcessingSlot.GroupSubject.Groups.Select(g => g.Name))}");
//                  logger.LogWarning($"   Останнє призначення: {lastAssignedSlot.GroupSubject.Subject.Name} {lastAssignedSlot.GroupSubject.Groups.First().EducationalProgramName} {String.Join(",", lastAssignedSlot.GroupSubject.Groups.Select(g => g.Name))}");
//                 success = false;
//             }
//
//             
//                 // ButifyResult();
//
//                 var res = Calculate(Root);
//
//                 int scheduleEstimation = 0;
//                 foreach (var s in UserFunctions.ScheduleEstimations)
//                 {
//                     var extScore = s.Estimate(Root);
//                     logger.LogDebug($"{algorithmName}: {s.Name} - {s.Id} - {extScore}");
//                     scheduleEstimation += extScore;
//                 }
//
//                 DebugData.Step("total estimation");
//
//                 res.TotalEstimation = scheduleEstimation;
//                 // todo readd
//                 res.ScheduleSlots = Slots.Values.Where(v => v.IsAssigned).Select(x => x.ScheduleSlot).ToList();
//                 res.Name = algorithmName;
//
//                 CheckResult(res);
//
//                 logger.LogInformation($"{algorithmName}: Schedule points {scheduleEstimation}");
//                 
//                 /// Маючи готовий розклад як початкову популяцію, використаємо генетичний алгоритм щоб спробувати покращити його
//
//
//
//             
//
//             DebugData.Step($"finish {success}");
//             logger.LogInformation(DebugData.ToString());
//
//             return success ? res : null;
//         }
//
//         
//         #endregion
//
//         #region Перевірка та оцінки
//
//         /// <summary>
//         /// Отримання оцінки слоту при воборі як наступного для пошуку розкладу.
//         /// </summary>
//         /// <param name="slot"></param>
//         /// <returns></returns>
//         private double EstimateSlot(SlotTracker slot)
//         {
//             var sp = new SlotEstimationDTO
//             {
//                 AvailableDomains = slot.AvailableDomains.Count,
//                 EndsOnIncompleteWeek = slot.IsLowDaysDanger,
//                 GroupCount = slot.ScheduleSlot.GroupSubject.Groups.Count,
//                 LessonSeriesLength = slot.SeriesLength,
//                 GroupSubject = slot.ScheduleSlot.GroupSubject
//             };
//
//
//             if (UserFunctions.SlotEstimations.Any())
//                 return UserFunctions.SlotEstimations.Sum(x => x.Estimate(sp));
//             else
//                 return Estimations.DefaultSlotEstimation(sp);
//         }
//
//         /// <summary>
//         /// Перевірка призначення для слоту в контексті вже призначених слотів.
//         /// </summary>
//         /// <param name="slotTracker"></param>
//         /// <param name="domain"></param>
//         /// <param name="assignedSLots"></param>
//         /// <returns></returns>
//         private bool ValidateAssignment(SlotTracker slotTracker, DomainValue domain, IAssignedSlots assignedSLots)
//         {
//             slotTracker.ScheduleSlot.Date = domain.Date;
//             slotTracker.ScheduleSlot.PairNumber = domain.PairNumber;
//
//             var slotAdapter = GetAdapter(slotTracker.ScheduleSlot);
//
//             var standartValidation = Validators.StandartValidation(slotAdapter, assignedSLots);
//             if (!standartValidation)
//                 return false;
//
//             // Перевірка на аудиторії, якщо алгоритм має враховувати їх
//             // if (!ignoreClassrooms && !slotTracker.ScheduleSlot.GroupSubject.Subject.NoClassroom)
//             // {
//             //     var classroomValidation = ValidateAndSelectClassroom(slotTracker.ScheduleSlot, domain, assignedSLots);
//             //     if (!classroomValidation)
//             //         return false;
//             // }
//
//             foreach (var validator in UserFunctions.SlotValidators)
//             {
//                 var userValidation = validator.Validate(slotAdapter, assignedSLots);
//                 if (!userValidation)
//                     return false;
//             }
//             
//             return true;
//         }
//         
//         /// <summary>
//         /// Перевірка результату
//         /// </summary>
//         private void CheckResult(AlgorithmResultDTO res)
//         {
//             // якщо зазначено розподіл аудиторій, то перевіряємо, чи всі слоти призначено аудиторії
//             if (!this.ignoreClassrooms)
//             {
//                 foreach (var slot in res.ScheduleSlots.Where(x => !x.GroupSubject.Subject.NoClassroom))
//                 {
//                     if (slot.Classroom == null)
//                         throw new Exception($"Слот {slot.GroupSubject.Subject.Name} {slot.GroupSubject.Groups.First().Name} {slot.Date.ToShortDateString()} {slot.PairNumber} парі не призначено аудиторію");
//
//                     // перевірка на переповнення аудиторій
//                     if (slot.Classroom.StudentCount < slot.GroupSubject.StudentCount)
//                         throw new Exception($"Аудиторія {slot.Classroom.Name} переповнена. Заплановано {slot.GroupSubject.StudentCount} студентів, але вміст аудиторії {slot.Classroom.StudentCount}");
//
//                     // перевірка на тип аудиторії
//                     if (!slot.Classroom.ClassroomTypes.Any(x => slot.GroupSubject.Subject.ClassroomTypes.Select(x => x.ClassroomTypeId).Contains(x.ClassroomTypeId)))
//                         throw new Exception($"Аудиторія {slot.Classroom.Name} не підходить для предмета {slot.GroupSubject.Subject.Name} {slot.GroupSubject.Groups.First().Name}");
//                 }
//
//
//                 // перевірка на унікальність аудиторій в розрізі дати та пари
//                 var groupedSlots = res.ScheduleSlots.Where(x => x.Classroom != null).GroupBy(x => new { x.Date, x.PairNumber });
//                 foreach (var group in groupedSlots)
//                 {
//                     var classrooms = group.Select(x => x.Classroom.Id).ToHashSet();
//                     if (classrooms.Count != group.Count())
//                         throw new Exception($"Аудиторія {string.Join(", ", classrooms)} вже призначена для слоту {string.Join(", ", group.Select(x => x.GroupSubject.Subject.Name))} {string.Join(", ", group.Select(x => x.GroupSubject.Groups.First().Name))} {group.Key.Date.ToShortDateString()} {group.Key.PairNumber} парі");
//                 }
//             }
//         }
//
//         #endregion
//
//         #region Utils
//
//         /// <summary>
//         /// Отримання адаптера слоту за слотом.
//         /// </summary>
//         /// <param name="slot">Слот, для якого потрібно отримати адаптер.</param>
//         /// <returns>Адаптер слоту.</returns>
//         /// <exception cref="KeyNotFoundException">Виникає, якщо адаптер для слоту не знайдено.</exception>
//         public IScheduleSlot GetAdapter(ScheduleSlotDTO slot)
//         {
//             return slot;
//         }
//
//         /// <summary>
//         /// Отримання списку призначених слотів.
//         /// </summary>
//         /// <returns></returns>
//         private AssignedSlotsDTO GetAssignedSlots()
//         {
//
//             var res = new AssignedSlotsDTO(
//                 slotFactory: () => Slots.Values.Where(s => s.IsAssigned).Select(s => s.ScheduleSlot),
//                 slotsByTeacherFactory: getAssignedByTeacher,
//                 slotsByGroupFactory: getAssignedByGroup,
//                 slotsByTeacherAndDateFactory: getAssignedByTeacherAndDate,
//                 slotsByGroupAndDateFactory: getAssignedByGroupAndDate
//               );
//
//             return res;
//         }
//         private IEnumerable<IScheduleSlot> getAssignedByGroupAndDate(long groupId, DateTime date)
//         {
//             IEnumerable<IScheduleSlot> res = assignedSlotsByGroupDate.ContainsKey(groupId) && assignedSlotsByGroupDate[groupId].ContainsKey(date)
//                 ? assignedSlotsByGroupDate[groupId][date].Select(s => s.ScheduleSlot)
//                 : new List<IScheduleSlot>();
//             return res;
//         }
//         private IEnumerable<IScheduleSlot> getAssignedByTeacherAndDate(long teacherId, DateTime date)
//         {
//             IEnumerable<IScheduleSlot> res = assignedSlotsByTeacherDate.ContainsKey(teacherId) && assignedSlotsByTeacherDate[teacherId].ContainsKey(date)
//                 ? assignedSlotsByTeacherDate[teacherId][date].Select(s => s.ScheduleSlot)
//                 : new List<IScheduleSlot>();
//             return res;
//         }
//         private IEnumerable<IScheduleSlot> getAssignedByGroup(long groupId)
//         {
//             IEnumerable<IScheduleSlot> res2 = assignedSlotsByGroupDate.ContainsKey(groupId)
//                 ? assignedSlotsByGroupDate[groupId].Values.SelectMany(x => x.Select(s => s.ScheduleSlot as IScheduleSlot))
//                 : new List<IScheduleSlot>();
//             return res2;
//         }
//         private IEnumerable<IScheduleSlot> getAssignedByTeacher(long teacherId)
//         {
//             List<IScheduleSlot> res2 = assignedSlotsByTeacherDate.ContainsKey(teacherId)
//                 ? assignedSlotsByTeacherDate[teacherId].Values.SelectMany(x => x.Select(s => s.ScheduleSlot as IScheduleSlot)).ToList()
//                 : new List<IScheduleSlot>();
//             return res2;
//         }
//
//         #endregion
//         
//         
//         private void CheckUnitaryContext(ScheduleChromosome chromosome)
//         {
//             int penanty = 0;
//
//             foreach (var slot in chromosome.Slots)
//             {
//                 foreach (var constraint in UserFunctions.UnitaryConstraints)
//                 {
//                     if (!constraint.Check(slot))
//                     {
//                         // todo think about disqualification
//                         penanty += 1000; // High penalty for violating hard constraint
//                     }
//                 }
//
//             }
//         }
//
//         private void Estimate(ScheduleChromosome chromosome)
//         {
//             int reward = 0;
//
//             foreach (var slot in chromosome.Slots)
//             {
//                 foreach (var estimation in UserFunctions.SlotEstimations)
//                 {
//                     // todo rewrite GetAssignedSlots for genetic algorithm
//                     reward += Estimations.DefaultSlotValueEstimation(slot, GetAssignedSlots());
//
//                 }
//             }
//         }
//
//         private int ScheduleEstimation(FacultySeasonDTO facultySeason ,ScheduleChromosome chromosome)
//         {
//             int reward = 0;
//
//             foreach (var estimation in UserFunctions.ScheduleEstimations)
//             {
//                 reward += estimation.Estimate(facultySeason);
//             }
//             return reward;
//         }
//         
//         
//         private int CalculateFitness(ScheduleChromosome chromosome, FacultySeasonDTO season, UserFunctions functions)
//         {
//             int penalty = 0;
//             int reward = 0;
//
//             foreach (var slot in chromosome.Slots)
//             {
//                 foreach (var constraint in functions.UnitaryConstraints)
//                 {
//                     if (!constraint.Check(slot))
//                         penalty += 1000;
//                 }
//
//                 foreach (var estimation in functions.SlotEstimations)
//                 {
//                     // todo refactor this
//                     reward += Estimations.DefaultSlotValueEstimation(slot, GetAssignedSlots());
//                     // reward += estimation.Estimate(season, slot);
//                 }
//             }
//
//             foreach (var estimation in functions.ScheduleEstimations)
//             {
//                 reward += estimation.Estimate(season);
//             }
//
//             return reward - penalty;
//         }
//         
//         
//         public AlgorithmResultDTO Calculate(FacultySeasonDTO seasonDTO)
//         {
//             var userFunctions = UserFunctions;
//             // ось тут ми вже маємо створити початкову популяцію - фактично призначити домені значення як в AssignSlots.
//             // не потрібно щоб початкова популяція виконувала всі contraints, генетичний алгоритм потім якраз має покращувати цю популяцію.
//             // 
//             // todo призначити слоти для популяції більш дешево, але поки використаємо default алгоритм
//             var population = GenerateInitialPopulation(seasonDTO);
//
//             foreach (var chromosome in population)
//                 chromosome.Fitness = CalculateFitness(chromosome, seasonDTO, userFunctions);
//
//             for (int gen = 0; gen < MaxGenerations; gen++)
//             {
//                 var newPopulation = new List<ScheduleChromosome>();
//
//                 newPopulation.Add(GetBest(population));
//
//                 while (newPopulation.Count < PopulationSize)
//                 {
//                     // var parent1 = new List<ScheduleChromosome>(population);
//                     // var parent2 = new List<ScheduleChromosome>(population);
//                     var parent1 = TournamentSelection(population);
//                     var parent2 = TournamentSelection(population);
//                     var (child1, child2) = Crossover(parent1, parent2);
//                     Mutate(child1, seasonDTO);
//                     Mutate(child2, seasonDTO);
//
//                     child1.Fitness = CalculateFitness(child1, seasonDTO, userFunctions);
//                     child2.Fitness = CalculateFitness(child2, seasonDTO, userFunctions);
//
//                     newPopulation.Add(child1);
//                     if (newPopulation.Count < PopulationSize)
//                         newPopulation.Add(child2);
//                 }
//
//                 population = newPopulation;
//             }
//
//             var best = GetBest(population);
//             return new AlgorithmResultDTO
//             {
//                 Name = Name,
//                 ScheduleSlots = best.Slots,
//                 TotalEstimation = best.Fitness
//             };
//
//         }
//
//         int stepCount = 100_000;
//         private List<ScheduleChromosome> GenerateInitialPopulation(FacultySeasonDTO season)
//         {
//             // todo випадково призначити слоти для початкової популяції (щоб вийшло більш-менш дешево), але поки використаємо результат з default алгоритму
//
//
//             var population = new List<ScheduleChromosome>();
//
//             for (int i = 0; i < PopulationSize; i++)
//             {
//                 var chromosome = new ScheduleChromosome();
//
//                 // foreach (var _ in initialPopulation)
//                 // {
//                 //     chromosome.Slots.Add(_);
//                 // }
//                 foreach (var s in Slots)
//                 {
//                     var slot = s.Key;
//                     var tracker = s.Value;
//                     // lets try random first
//                     if (tracker.AvailableDomains.FirstOrDefault() == null)
//                     { 
//                         continue;
//                     }
//                     var domain = tracker.AvailableDomains.FirstOrDefault();
//                     var date = domain.Date;
//                     tracker.AvailableDomains.Remove(domain);
//                     var rejected = new List<DomainValue>(tracker.AvailableDomains);
//                     rejected.Remove(domain);
//                     tracker.RejectedDomains.Add(stepCount,rejected);
//                     stepCount++;
//                     var pairNum = domain.PairNumber;
//                     
//                     EstimateSlot(tracker);
//                     
//                     
//                     var slotToInsert = new ScheduleSlotDTO
//                     {
//                         GroupSubject = tracker.ScheduleSlot.GroupSubject,
//                         Date = date,
//                         PairNumber = pairNum,
//                         Classroom = tracker.ScheduleSlot.Classroom
//                     };
//
//                     var slotTracker = new SlotTracker()
//                     {
//                         AvailableDomains = tracker.AvailableDomains,
//                         IsAssigned = false,
//                         ScheduleSlot = slotToInsert
//                     };
//                     
//                     
//                     chromosome.SlotTrackers.Add(slotToInsert, slotTracker);
//                     chromosome.Slots.Add(slotToInsert);
//                 }
//
//                 // foreach (var gs in season.GroupSubjects)
//                 // {
//                 //     var possibleAssignments = gs.ScheduleSlots.Select(slot => new
//                 //     {
//                 //         Slot = slot,
//                 //         Estimate = EstimateSlot(Slots[slot])
//                 //     })
//                 //         .OrderByDescending(x => x.Estimate)
//                 //         .ToList();
//                 //     foreach (var assignment in possibleAssignments)
//                 //     {
//                 //         var slot = new ScheduleSlotDTO
//                 //         {
//                 //             GroupSubject = gs,
//                 //             Date = assignment.Slot.Date,
//                 //             PairNumber = assignment.Slot.PairNumber,
//                 //             Classroom = assignment.Slot.Classroom
//                 //         };
//                 //
//                 //         chromosome.Slots.Add(slot); //
//                 //         
//                 //     }
//                     // foreach (var _ in gs.ScheduleSlots)
//                     // {
//                     //     // var scheduleSlot = new ScheduleSlotDTO
//                     //     // {
//                     //     //     GroupSubject = gs,
//                     //     //     Date = RandomDate(season.BeginSeason, season.EndSeason),
//                     //     //     PairNumber = _random.Next(1, season.MaxLessonsPerDay + 1),
//                     //     //     Classroom = season.Classrooms[_random.Next(season.Classrooms.Count)]
//                     //     // };
//                     //     if (ValidateAssignment(Slots[_], null, GetAssignedSlots()))
//                     //     {
//                     //         chromosome.Slots.Add(_);
//                     //     }
//                     // }
//                 // }
//                 population.Add(chromosome);
//             }
//
//             return population;
//         }
//         
//         private List<DomainValue> GetSortedDomains(SlotTracker nextSlot, IAssignedSlots assignedSLots)
//         {
//             var res = nextSlot.AvailableDomains.ToList()
//                 .ResortFirstK((d) => EstimateSlotValue(nextSlot, d, assignedSLots), Parameters.DomainsTopK, Parameters.DomainsTemperature);
//             return res;
//         }
//         
//         /// <summary>
//         /// Оцінка слоту для вибору як наступного слоту в розподілі.
//         /// </summary>
//         /// <param name="slotTracker"></param>
//         /// <param name="domain"></param>
//         /// <param name="assignedSLots"></param>
//         /// <returns></returns>
//         /// <exception cref="ArgumentException"></exception>
//         private int EstimateSlotValue(SlotTracker slotTracker, DomainValue domain, IAssignedSlots assignedSLots)
//         {
//             if (slotTracker.IsAssigned) throw new ArgumentException("Отакої!");
//
//             slotTracker.ScheduleSlot.Date = domain.Date;
//             slotTracker.ScheduleSlot.PairNumber = domain.PairNumber;
//
//             var slotAdapter = GetAdapter(slotTracker.ScheduleSlot);
//
//             int score = 0;
//
//             if (UserFunctions.SlotValueEstimations.Any())
//             {
//                 foreach (var estimation in UserFunctions.SlotValueEstimations)
//                     score += estimation.Estimate(slotAdapter, assignedSLots);
//             }
//             else
//                 score += Estimations.DefaultSlotValueEstimation(slotAdapter, assignedSLots);
//             return score;
//         }
//
//         private ScheduleChromosome GetBest(List<ScheduleChromosome> population) =>
//             population.OrderByDescending(p => p.Fitness).First();
//
//         private ScheduleChromosome TournamentSelection(List<ScheduleChromosome> population, int size = 5)
//         {
//             var selected = population.OrderBy(_ => _random.Next()).Take(size);
//             return selected.OrderByDescending(c => c.Fitness).First();
//         }
//
//         private (ScheduleChromosome, ScheduleChromosome) Crossover(ScheduleChromosome p1, ScheduleChromosome p2)
//         {
//             int cut = _random.Next(p1.Slots.Count);
//             var child1 = new ScheduleChromosome();
//             var child2 = new ScheduleChromosome();
//
//             child1.Slots.AddRange(p1.Slots.Take(cut));
//             child1.Slots.AddRange(p2.Slots.Skip(cut));
//
//             child2.Slots.AddRange(p2.Slots.Take(cut));
//             child2.Slots.AddRange(p1.Slots.Skip(cut));
//
//             return (child1, child2);
//         }
//         
//         private void Mutate(ScheduleChromosome chromosome, FacultySeasonDTO season)
//         {
//             foreach (var slot in chromosome.Slots)
//             {
//                 if (_random.NextDouble() < 0.05)
//                 {
//                     
//                     // slot.Date = RandomDate(season.BeginSeason, season.EndSeason); // todo вибирати дату інакше - наприклад, через domainvalues.
//                     slot.PairNumber = _random.Next(1, season.MaxLessonsPerDay + 1);
//                     slot.Classroom = season.Classrooms[_random.Next(season.Classrooms.Count)]; // todo вибирати клас з урахуванням типу
//                 }
//             }
//         }
//         
//         private DateTime RandomDate(DateTime start, DateTime end)
//         {
//             var range = (end - start).Days;
//             return start.AddDays(_random.Next(range));
//         }
//
//
//         #region Initial population creation
//
//                 /// <summary>
//         /// Рекурсивний метод призначення доменних значень для всіх слотів.
//         /// </summary>
//         /// <param name="currentStep">Поточний крок рекурсії.</param>
//         /// <returns>True, якщо рішення знайдено, інакше false.</returns>
//         [MethodImpl(MethodImplOptions.AggressiveOptimization)]
//         private bool AssignSlots(int currentStep)
//         {
//
//             lastStep = currentStep;
//
//             if (cancelToken != null && cancelToken.IsCancellationRequested)
//                 cancelToken.ThrowIfCancellationRequested();
//
//             if (!slotsByStep.ContainsKey(currentStep))
//                 slotsByStep[currentStep] = new List<SlotTracker>();
//
//             DebugData.Step("start");
//
//             // Отримуємо наступний незаповнений слот із застосуванням MRV та пріоритету для лекцій.
//             SlotTracker? nextSlot = GetNextUnassignedSlot();
//
//
//             if (nextSlot == null)
//             {
//                 // Якщо всі слоти вже заповнені – повертаємо успіх.
//                 if (Slots.Values.All(s => s.IsAssigned))
//                 {
//                     return true;
//                 }
//                 DebugData.Step("check result");
//                 return false;
//             }
//
//             lastProcessingSlot = nextSlot.ScheduleSlot;
//
//             DebugData.Step("next slot");
//
//             var assignedSLots = GetAssignedSlots();
//
//             List<DomainValue> candidateDomains = GetSortedDomains(nextSlot, assignedSLots);
//             DebugData.Step("candidateDomains");
//
//
//             // оновлення кешу для перших слотів
//             ResetUnAssignedFirstSlots(nextSlot);
//
//             foreach (var domain in candidateDomains)
//             {
//                 var vld = ValidateAssignment(nextSlot, domain, assignedSLots);
//                 DebugData.Step("ValidateAssignment");
//
//                 if (vld)
//                 {
//                     SetSlotAssigned(nextSlot, domain, currentStep);
//                     DebugData.Step("assign");
//
//                     var syncCheck = ApplySynchronizedDomainPattern(nextSlot, assignedSLots);
//                     DebugData.Step("ApplySynchronizedDomainPattern");
//
//                     if (syncCheck)
//                     {
//                         var frwdcheck = ForwardCheck(nextSlot, currentStep);
//                         DebugData.Step("ForwardCheck");
//
//                         if (frwdcheck)
//                         {
//                             lastAssignedSlot = nextSlot.ScheduleSlot;
//
//                             // Рекурсивно пробуємо наступні призначення.
//                             if (AssignSlots(currentStep + 1))
//                             {
//                                 // Якщо рекурсія повертає true, то ми знайшли рішення! Виходимо з рекурсії.
//                                 return true;
//                             }
//                         }
//                         else
//                         {
//                             //   Console.WriteLine($"{currentStep} Fail ForwardCheck {nextSlot.ScheduleSlot.GroupSubject.Subject.Name}");
//                         }
//                     }
//                     else
//                     {
//                         //   Console.WriteLine($"{currentStep} Fail ApplySynchronizedDomainPattern {nextSlot.ScheduleSlot.GroupSubject.Subject.Name}");
//                     }
//
//                     // Якщо не пройшла валідація або рекурсія не знайшла рішення, скидаємо призначення та пробуємо наступне доменне значення.
//
//                     // Якщо подальші призначення не дали рішення, відновлюємо вилучені доменні значення.
//                     foreach (var slot in slotsByStep[currentStep])
//                     {
//                         slot.RestoreRejectedDomains(currentStep);
//                     }
//                     DebugData.Step("RestoreRejectedDomains");
//
//                     // Відновлюємо призначені значення 
//                     foreach (var slot in slotsByStep[currentStep].Where(x => x.IsAssigned && x.AssignStep == currentStep))
//                     {
//                         SetSlotUnAssigned(slot);
//                     }
//                     DebugData.Step("unassign");
//
//                     slotsByStep[currentStep].Clear();
//                 }
//             }
//
//             // Відновлюємо кеш для перших слотів.
//             RestoreUnAssignedFirstSlots(nextSlot);
//
//             // Якщо жодне доменне значення не підходить, повертаємо false.
//             DebugData.Step("end");
//             return false;
//         }
//
//         /// <summary>
//         /// Вибір наступного незаповненого слоту із застосуванням MRV та пріоритету для лекцій.
//         /// </summary>
//         /// <returns>Незаповнений слот, або null якщо таких немає.</returns>
//         private SlotTracker? GetNextUnassignedSlot()
//         {
//             return unassignedFirstSlots
//                 .ResortFirstK((s) => EstimateSlot(s), Parameters.SlotsTopK, Parameters.SlotsTemperature).FirstOrDefault();
//         }
//
//         /// <summary>
//         /// Оновлення кешу перших слотів для групи.
//         /// </summary>
//         /// <param name="slot"></param>
//         private void ResetUnAssignedFirstSlots(SlotTracker slot)
//         {
//             if (slot.IsFirstTrackerInSeries)
//             {
//                 unassignedFirstSlots.Remove(slot);
//                 var firstUnassignedSlot = firstSlotsByGroupSubjects[slot.ScheduleSlot.GroupSubject.Id].FirstOrDefault(x => !x.IsAssigned && x != slot);
//                 if (firstUnassignedSlot != null)
//                     unassignedFirstSlots.Add(firstUnassignedSlot);
//             }
//         }
//
//                 /// <summary>
//         /// Призначає домен для слоту. Оновлюємо кеші.
//         /// </summary>
//         /// <param name="slot"></param>
//         /// <param name="val"></param>
//         /// <param name="step"></param>
//         private void SetSlotAssigned(SlotTracker slot, DomainValue val, int step)
//         {
//             slot.SetDomain(val, step);
//             slot.IsAssigned = true;
//             slotsByStep[step].Add(slot);
//
//             if (!assignedSlotsByTeacherDate.ContainsKey(slot.ScheduleSlot.GroupSubject.Teacher.Id))
//                 assignedSlotsByTeacherDate[slot.ScheduleSlot.GroupSubject.Teacher.Id] = new Dictionary<DateTime, HashSet<SlotTracker>>();
//
//             if (!assignedSlotsByTeacherDate[slot.ScheduleSlot.GroupSubject.Teacher.Id].ContainsKey(val.Date))
//                 assignedSlotsByTeacherDate[slot.ScheduleSlot.GroupSubject.Teacher.Id][val.Date] = new HashSet<SlotTracker>();
//
//             assignedSlotsByTeacherDate[slot.ScheduleSlot.GroupSubject.Teacher.Id][val.Date].Add(slot);
//
//             foreach (var group in slot.ScheduleSlot.GroupSubject.Groups)
//             {
//                 if (!assignedSlotsByGroupDate.ContainsKey(group.Id))
//                     assignedSlotsByGroupDate[group.Id] = new Dictionary<DateTime, HashSet<SlotTracker>>();
//
//                 if (!assignedSlotsByGroupDate[group.Id].ContainsKey(val.Date))
//                     assignedSlotsByGroupDate[group.Id][val.Date] = new HashSet<SlotTracker>();
//
//                 assignedSlotsByGroupDate[group.Id][val.Date].Add(slot);
//             }
//
//             if (slot.ScheduleSlot.Classroom != null)
//             {
//                 if (!assignedClassrooms.ContainsKey(val.Date))
//                     assignedClassrooms[val.Date] = new Dictionary<int, Dictionary<ClassroomDTO, ScheduleSlotDTO>>();
//
//                 if (!assignedClassrooms[val.Date].ContainsKey(val.PairNumber))
//                     assignedClassrooms[val.Date][val.PairNumber] = new Dictionary<ClassroomDTO, ScheduleSlotDTO>();
//
//                 if (assignedClassrooms[val.Date][val.PairNumber].ContainsKey(slot.ScheduleSlot.Classroom))
//                     throw new Exception($"Аудиторія {slot.ScheduleSlot.Classroom.Name} вже зайнята на {val.Date.ToShortDateString()} {val.PairNumber} парі");
//
//                 assignedClassrooms[val.Date][slot.ScheduleSlot.PairNumber][slot.ScheduleSlot.Classroom] = slot.ScheduleSlot;
//             }
//         }
//
//         
//         /// <summary>
//         /// Застосовуємо доменні значення для всієї серії на основі першого слоту серії, якщо можливо.
//         /// </summary>
//         /// <param name="currentTracker"></param>
//         /// <param name="assignedSLots"></param>
//         /// <returns></returns>
//         private bool ApplySynchronizedDomainPattern(SlotTracker currentTracker, AssignedSlotsDTO assignedSLots)
//         {
//             // Отримуємо предмет із поточного трекера
//             var subject = currentTracker.ScheduleSlot.GroupSubject;
//
//             // Отримуємо всі слот-трекери для цього предмету через список слотів GroupSubject.
//             // Використовуємо лише ще не призначені з серії. 
//             var freeTRackers = subject.ScheduleSlots
//                 .Select(slot => Slots[slot])
//                 .Where(tracker => !tracker.IsAssigned && tracker.SeriesId == currentTracker.SeriesId)
//                 .OrderBy(tracker => tracker.ScheduleSlot.LessonNumber)
//                 .ToList();
//
//             if (!freeTRackers.Any()) return true;
//
//             DateTime minAvailable = currentTracker.ScheduleSlot.Date;
//
//             var nextDate = new DomainValue
//             {
//                 Date = minAvailable.AddDays(currentTracker.WeekShift * 7),
//                 PairNumber = currentTracker.ScheduleSlot.PairNumber
//             };
//
//             var maxAvailable = freeTRackers.Select(x => x.AvailableDomains.Max()).Max();
//
//             while (nextDate <= maxAvailable)
//             {
//                 var nextTracker = freeTRackers.FirstOrDefault(tracker => !tracker.IsAssigned
//                     && tracker.AvailableDomains.Contains(nextDate)
//                     && ValidateAssignment(tracker, nextDate, assignedSLots)
//               );
//
//                 if (nextTracker != null)
//                 {
//                     SetSlotAssigned(nextTracker, nextDate, currentTracker.AssignStep);
//                     freeTRackers.Remove(nextTracker);
//                 }
//
//                 nextDate.Date = nextDate.Date.AddDays(currentTracker.WeekShift * 7);
//             }
//
//             var res = !freeTRackers.Any(tracker => !tracker.IsAssigned);
//             return res;
//         }
//
//                 /// <summary>
//         /// Forward checking: оновлює домени для всіх незаповнених слотів, видаляючи кандидати,
//         /// які вже не відповідають констрейнтам. Вилучені значення записуються в RejectedDomains для поточного кроку.
//         /// </summary>
//         /// <param name="assignedSlot">Слот, для якого зроблено останнє призначення.</param>
//         /// <param name="currentStep">Поточний крок пошуку.</param>
//         /// <returns>True, якщо для всіх слотів залишається хоча б один кандидат; інакше false.</returns>
//         private bool ForwardCheck(SlotTracker assignedSlot, int currentStep)
//         {
//             var changedSlots = assignedSlot.ScheduleSlot.GroupSubject.ScheduleSlots.Select(x => Slots[x]).Where(x => x.IsAssigned && x.AssignStep == currentStep).ToList();
//
//             HashSet<SlotTracker> forwardSlots = new HashSet<SlotTracker>(teacherSlots[assignedSlot.ScheduleSlot.GroupSubject.Teacher.Id].Where(s => !s.IsAssigned && s.IsFirstTrackerInSeries));
//             foreach (var grId in assignedSlot.ScheduleSlot.GroupSubject.Groups.Select(g => g.Id))
//                 foreach (var sl in groupsSlots[grId].Where(s => !s.IsAssigned && s.IsFirstTrackerInSeries))
//                     forwardSlots.Add(sl);
//
//             foreach (var slot in forwardSlots)
//             {
//                 // Зберігаємо поточний список доступних доменів.
//                 var originalDomains = new List<DomainValue>(slot.AvailableDomains);
//
//                 // Оновлюємо домени: залишаємо лише ті, що відповідають обмеженням.
//                 slot.AvailableDomains = new SortedSet<DomainValue>(slot.AvailableDomains
//                     .Where(candidate => Validators.ValidateAssignmentArc(slot, candidate, changedSlots)))
//                     ;
//
//                 // Визначаємо, які доменні значення було вилучено.
//                 var removed = originalDomains.Except(slot.AvailableDomains).ToList();
//                 if (removed.Any())
//                 {
//                     if (!slot.RejectedDomains.ContainsKey(currentStep))
//                         slot.RejectedDomains[currentStep] = new List<DomainValue>();
//                     slot.RejectedDomains[currentStep].AddRange(removed);
//                     slotsByStep[currentStep].Add(slot);
//                 }
//
//                 // Якщо домен став порожнім, повертаємо false.
//                 if (!slot.AvailableDomains.Any())
//                 {
//                     return false;
//                 }
//             }
//             return true;
//         }
//                 
//         /// <summary>
//         /// Відміна призначення слоту. Оновлюємо кеші.
//         /// </summary>
//         /// <param name="slot"></param>
//         private void SetSlotUnAssigned(SlotTracker slot)
//         {
//             if (slot.IsAssigned)
//             {
//                 assignedSlotsByTeacherDate[slot.ScheduleSlot.GroupSubject.Teacher.Id][slot.ScheduleSlot.Date].Remove(slot);
//                 foreach (var group in slot.ScheduleSlot.GroupSubject.Groups)
//                     assignedSlotsByGroupDate[group.Id][slot.ScheduleSlot.Date].Remove(slot);
//
//                 if (slot.ScheduleSlot.Classroom != null)
//                     assignedClassrooms[slot.ScheduleSlot.Date][slot.ScheduleSlot.PairNumber].Remove(slot.ScheduleSlot.Classroom);
//
//                 // Очищаємо аудиторію
//                 slot.ScheduleSlot.Classroom = null;
//             }
//             slot.IsAssigned = false;
//             
//         }
//
//         /// <summary>
//         /// Відновлення кешу перших слотів для групи.
//         /// </summary>
//         /// <param name="slot"></param>
//         private void RestoreUnAssignedFirstSlots(SlotTracker slot)
//         {
//             if (slot.IsFirstTrackerInSeries)
//             {
//                 var firstUnassignedSlot = firstSlotsByGroupSubjects[slot.ScheduleSlot.GroupSubject.Id].FirstOrDefault(x => !x.IsAssigned && x != slot);
//                 if (firstUnassignedSlot != null)
//                     unassignedFirstSlots.Remove(firstUnassignedSlot);
//                 unassignedFirstSlots.Add(slot);
//             }
//         }
//         #endregion
//
//
//
//
//     }
// }