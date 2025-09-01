// using System.Collections.Concurrent;
// using System.Runtime.CompilerServices;
// using AcaTime.Algorithm.Genetic.Models;
// using AcaTime.Algorithm.Genetic.Services.Calc;
// using AcaTime.Algorithm.Genetic.Utils;
// using AcaTime.ScheduleCommon.Models.Calc;
// using AcaTime.ScheduleCommon.Models.Constraints;
// using AcaTime.ScheduleCommon.Utils;
// using AcaTime.ScriptModels;
// using Microsoft.Extensions.Logging;
//
// namespace AcaTime.Algorithm.Genetic.Services
// {
//
//     /// <summary>
//     /// Клас для реалізації алгоритму розкладу.
//     /// </summary>
//     public class GeneticScheduleAlgorithmUnit
//     {
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
//         private List<DomainValue> allDomainValues;
//         private Random random = new Random();
//
//         public async Task<List<AlgorithmResultDTO>> Run(FacultySeasonDTO root, UserFunctions userFunctions, 
//             Dictionary<string, string> parameters, bool ignoreClassrooms, ILogger logger, 
//             CancellationToken cancellationToken = default)
//         {
//             
//             this.logger = logger;
//             this.Root = root;
//             this.UserFunctions = userFunctions;
//             // this.RunParameters = new AlgorithmParams(parameters);
//             // this.StartTime = DateTime.Now;
//             
//             PreparePrivateCash();
//
//             // Generate all possible domain values
//             allDomainValues = GenerateAllDomainValues();
//
//             int scheduleEstimation = 0;
//
//             foreach (var s in UserFunctions.ScheduleEstimations.ToList())
//             {
//                 var extScore = s.Estimate(Root);
//                 logger.LogDebug($"{algorithmName}: {s.Name} - {s.Id} - {extScore}");
//                 scheduleEstimation += extScore;
//             }
//             logger.LogInformation($"Population result START: {scheduleEstimation}");
//             
//             // Create initial population
//             var populationSize = 5;//RunParameters.MaxIterations; // Use MaxIterations as population size
//             var population = GenerateInitialPopulation(populationSize, ignoreClassrooms, cancellationToken);
//             
//             foreach (var s in UserFunctions.ScheduleEstimations.ToList())
//             {
//                 var extScore = s.Estimate(Root);
//                 logger.LogDebug($"{algorithmName}: {s.Name} - {s.Id} - {extScore}");
//                 scheduleEstimation += extScore;
//             }
//             logger.LogInformation($"Population result0: {population[0].Fitness}");
//             logger.LogInformation($"Population result1: {scheduleEstimation}");
//             
//             // var generations = 10; // todo pass from params
//             // population = Evolve(population, Slots, allDomainValues, userFunctions, ignoreClassrooms, generations, logger, cancellationToken);
//
//             logger.LogInformation($"Generated initial population of {population.Count} individuals");
//
//             // For now, just return the best individuals from initial population
//             // In a full genetic algorithm, you would evolve this population
//             var results = population.ToList()
//                 .Where(individual => individual != null)
//                 .OrderByDescending(individual => CalculateFitness(individual))
//                 .Take(1)
//                 .Select(individual => CreateAlgorithmResult(individual))
//                 .ToList();
//
//             return results;
//         }
//         
//         private List<DomainValue> GenerateAllDomainValues()
//         {
//             var domains = new List<DomainValue>();
//             for (var date = Root.BeginSeason; date <= Root.EndSeason; date = date.AddDays(1))
//             {
//                 for (int pair = 1; pair <= Root.MaxLessonsPerDay; pair++)
//                 {
//                     domains.Add(new DomainValue() { Date = date, PairNumber = pair });
//                 }
//             }
//             return domains;
//         }
//         
//         private List<Individual> GenerateInitialPopulation(int populationSize, bool ignoreClassrooms, 
//             CancellationToken cancellationToken)
//         {
//             var population = new List<Individual>();
//             
//
//             for (int i = 0; i < populationSize; i++)
//             {
//                 Individual individual = GenerateConstraintAwareIndividual(ignoreClassrooms,  0.5);
//                 if (individual.Assignments.Count > 0)
//                 {
//                     individual.Fitness = CalculateFitness(individual);
//                     population.Add(individual);
//                 }
//
//             }
//             
//             return population;
//         }
//
//         private int currStep = 0;
//         
//         /// <summary>
//         /// Generates individual using constraint-aware assignment with configurable greediness
//         /// </summary>
//         private Individual GenerateConstraintAwareIndividual(bool ignoreClassrooms, double greedyProbability)
//         {
//             var individual = new Individual();
//
//             SlotTracker? nextSlotTracker = GetNextUnassignedSlot();
//
//             if (nextSlotTracker == null)
//             {
//                 
//             }
//             
//             // Create a working copy of slot trackers
//             var workingSlots = CloneSlotTrackers();
//             var assignedSlots = GetAssignedSlots();
//             
//             var candidateDomains = GetSortedDomains(nextSlotTracker, assignedSlots);
//             
//             ResetUnAssignedFirstSlots(nextSlotTracker);
//             
//             // Start with first trackers (series heads) using MRV heuristic
//             var unassignedFirstSlots = FirstTrackers
//                 .Select(ft => workingSlots[ft.ScheduleSlot])
//                 .Where(t => !t.IsAssigned)
//                 .ToList();
//
//             while (unassignedFirstSlots.Any())
//             {
//                 // Select next slot using MRV (Most Restricted Variable)
//                 var nextSlot = unassignedFirstSlots
//                     .OrderBy(s => s.AvailableDomains.Count)
//                     .ThenByDescending(s => EstimateSlot(s))
//                     .First();
//
//                 // Get available domains for this slot
//                 var availableDomains = nextSlot.AvailableDomains
//                     .Where(domain => ValidateAssignment(nextSlot, domain, assignedSlots, ignoreClassrooms))
//                     .ToList();
//
//                 if (!availableDomains.Any())
//                 {
//                     // If no valid domains, try to backtrack or skip this individual
//                     logger.LogDebug($"No valid domains for slot {nextSlot.ScheduleSlot.GroupSubject.Subject.Name}");
//                     break;
//                 }
//
//                 DomainValue selectedDomain;
//                 if (random.NextDouble() < greedyProbability)
//                 {
//                     // Greedy selection - choose best domain
//                     selectedDomain = availableDomains
//                         .OrderByDescending(domain => EstimateSlotValue(nextSlot, domain, assignedSlots))
//                         .First();
//                 }
//                 else
//                 {
//                     // Random selection from available domains
//                     selectedDomain = availableDomains[random.Next(availableDomains.Count)];
//                 }
//
//                 // Assign the slot
//                 AssignSlotAndSeries(nextSlot, selectedDomain, workingSlots, assignedSlots);
//
//                 // Update unassigned first slots
//                 unassignedFirstSlots.Remove(nextSlot);
//                 
//                 // Add next unassigned slot from the same group subject if exists
//                 var nextInGroupSubject = workingSlots.Values
//                     .Where(s => s.ScheduleSlot.GroupSubject.Id == nextSlot.ScheduleSlot.GroupSubject.Id 
//                                && !s.IsAssigned && s.IsFirstTrackerInSeries)
//                     .FirstOrDefault();
//                 
//                 if (nextInGroupSubject != null)
//                 {
//                     unassignedFirstSlots.Add(nextInGroupSubject);
//                 }
//             }
//
//             // Convert working slots to individual assignments
//             foreach (var slotTracker in workingSlots.Values.Where(s => s.IsAssigned))
//             {
//                 individual.Assignments[slotTracker.ScheduleSlot] = new DomainValue
//                 {
//                     Date = slotTracker.ScheduleSlot.Date,
//                     PairNumber = slotTracker.ScheduleSlot.PairNumber
//                 };
//             }
//
//             return individual;
//         }
//         
//         private bool IsBasicallyFeasible(ScheduleSlotDTO slot, DomainValue domain, 
//             Dictionary<ScheduleSlotDTO, DomainValue> currentAssignments,
//             HashSet<(DateTime date, int pair, long? teacherId, long? groupId)> usedSlots)
//         {
//             // Check teacher conflict
//             if (usedSlots.Contains((domain.Date, domain.PairNumber, slot.GroupSubject.Teacher.Id, null)))
//                 return false;
//
//             // Check group conflicts
//             foreach (var group in slot.GroupSubject.Groups)
//             {
//                 if (usedSlots.Contains((domain.Date, domain.PairNumber, null, group.Id)))
//                     return false;
//             }
//
//             // Add more basic checks as needed
//             return true;
//         }
//         
//         private double CalculateFitness(Individual individual)
//         {
//             try
//             {
//                 // Create a temporary root with assigned slots
//                 var tempRoot = CloneFacultySeasonWithAssignments(individual);
//
//                 int totalScore = 0;
//                 foreach (var estimation in UserFunctions.ScheduleEstimations)
//                 {
//                     totalScore += estimation.Estimate(tempRoot);
//                 }
//
//                 return totalScore;
//             }
//             catch (Exception ex)
//             {
//                 logger.LogWarning($"Error calculating fitness: {ex.Message}");
//                 return double.MinValue;
//             }
//         }
//         
//         public string GetName() => "Genetic";
//         private AlgorithmResultDTO CreateAlgorithmResult(Individual individual)
//         {
//             
//             int scheduleEstimation = 0;
//             foreach (var s in UserFunctions.ScheduleEstimations.ToList())
//             {
//                 var extScore = s.Estimate(Root);
//                 logger.LogDebug($"{algorithmName}: {s.Name} - {s.Id} - {extScore}");
//                 scheduleEstimation += extScore;
//             }
//
//             DebugData.Step("total estimation");
//             
//             
//             var result = new AlgorithmResultDTO
//             {
//                 Name = GetName(),
//                 TotalEstimation = scheduleEstimation,
//                 ScheduleSlots = new List<ScheduleSlotDTO>()
//             };
//
//             foreach (var assignment in individual.Assignments)
//             {
//                 var slot = assignment.Key;
//                 var domain = assignment.Value;
//                 
//                 var assignedSlot = new ScheduleSlotDTO
//                 {
//                     Id = slot.Id,
//                     Date = domain.Date,
//                     PairNumber = domain.PairNumber,
//                     GroupSubject = slot.GroupSubject,
//                     LessonNumber = slot.LessonNumber,
//                     LessonSeriesId = slot.LessonSeriesId,
//                     LessonSeriesLength = slot.LessonSeriesLength,
//                     Classroom = slot.Classroom
//                 };
//                 
//                 result.ScheduleSlots.Add(assignedSlot);
//             }
//
//             return result;
//         }
//
//         /// <summary>
//         /// Creates a copy of FacultySeasonDTO with individual's assignments
//         /// </summary>
//         private FacultySeasonDTO CloneFacultySeasonWithAssignments(Individual individual)
//         {
//             var tempRoot = new FacultySeasonDTO
//             {
//                 Id = Root.Id,
//                 Name = Root.Name,
//                 BeginSeason = Root.BeginSeason,
//                 EndSeason = Root.EndSeason,
//                 MaxLessonsPerDay = Root.MaxLessonsPerDay,
//                 Classrooms = Root.Classrooms,
//                 GroupSubjects = new List<GroupSubjectDTO>()
//             };
//
//             // Clone group subjects with updated slot assignments
//             foreach (var gs in Root.GroupSubjects)
//             {
//                 var newGs = new GroupSubjectDTO
//                 {
//                     Id = gs.Id,
//                     Subject = gs.Subject,
//                     Groups = gs.Groups,
//                     Teacher = gs.Teacher,
//                     StudentCount = gs.StudentCount,
//                     ScheduleSlots = new List<ScheduleSlotDTO>()
//                 };
//
//                 foreach (var slot in gs.ScheduleSlots)
//                 {
//                     var newSlot = new ScheduleSlotDTO
//                     {
//                         Id = slot.Id,
//                         GroupSubject = newGs,
//                         LessonNumber = slot.LessonNumber,
//                         LessonSeriesId = slot.LessonSeriesId,
//                         LessonSeriesLength = slot.LessonSeriesLength
//                     };
//
//                     if (individual.Assignments.TryGetValue(slot, out var domain))
//                     {
//                         newSlot.Date = domain.Date;
//                         newSlot.PairNumber = domain.PairNumber;
//                         // TODO: Assign classroom if needed
//                     }
//
//                     newGs.ScheduleSlots.Add(newSlot);
//                 }
//
//                 tempRoot.GroupSubjects.Add(newGs);
//             }
//
//             return tempRoot;
//         }
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
//         /// Вибір наступного незаповненого слоту із застосуванням MRV та пріоритету для лекцій.
//         /// </summary>
//         /// <returns>Незаповнений слот, або null якщо таких немає.</returns>
//         private SlotTracker? GetNextUnassignedSlot()
//         {
//             return unassignedFirstSlots
//              .ResortFirstK((s) => EstimateSlot(s), Parameters.SlotsTopK, Parameters.SlotsTemperature).FirstOrDefault();
//         }
//
//         /// <summary>
//         /// Групуємо слоти по серіях.
//         /// </summary>
//         /// <param name="nextSlot"></param>
//         /// <param name="assignedSLots"></param>
//         /// <returns></returns>
//         private List<DomainValue> GetSortedDomains(SlotTracker nextSlot, IAssignedSlots assignedSLots)
//         {
//             var res = nextSlot.AvailableDomains.ToList()
//                 .ResortFirstK((d) => EstimateSlotValue(nextSlot, d, assignedSLots), Parameters.DomainsTopK, Parameters.DomainsTemperature);
//             return res;
//         }
//         
//         private void AssignSlotAndSeries(SlotTracker firstSlot, DomainValue startDomain, 
//             Dictionary<IScheduleSlot, SlotTracker> workingSlots, AssignedSlotsDTO assignedSlots)
//         {
//             // Assign the first slot
//             firstSlot.SetDomain(startDomain, 1);
//             firstSlot.IsAssigned = true;
//
//             // Find and assign other slots in the series
//             var subject = firstSlot.ScheduleSlot.GroupSubject;
//             var seriesSlots = subject.ScheduleSlots
//                 .Select(slot => workingSlots[slot])
//                 .Where(tracker => !tracker.IsAssigned && tracker.SeriesId == firstSlot.SeriesId)
//                 .OrderBy(tracker => tracker.ScheduleSlot.LessonNumber)
//                 .ToList();
//
//             var currentDate = startDomain.Date;
//             foreach (var slot in seriesSlots)
//             {
//                 currentDate = currentDate.AddDays(firstSlot.WeekShift * 7);
//                 var seriesDomain = new DomainValue
//                 {
//                     Date = currentDate,
//                     PairNumber = startDomain.PairNumber
//                 };
//
//                 if (slot.AvailableDomains.Contains(seriesDomain) && 
//                     ValidateAssignment(slot, seriesDomain, assignedSlots, true)) // Simplified validation for series
//                 {
//                     slot.SetDomain(seriesDomain, 1);
//                     slot.IsAssigned = true;
//                     // assignedSlots.AddSlot(slot.ScheduleSlot);
//                 }
//             }
//         }
//
//         private bool ValidateAssignment(SlotTracker slotTracker, DomainValue domain, 
//             IAssignedSlots assignedSlots, bool ignoreClassrooms)
//         {
//             // Temporarily set the domain
//             var originalDate = slotTracker.ScheduleSlot.Date;
//             var originalPair = slotTracker.ScheduleSlot.PairNumber;
//             
//             slotTracker.ScheduleSlot.Date = domain.Date;
//             slotTracker.ScheduleSlot.PairNumber = domain.PairNumber;
//
//             try
//             {
//                 var slotAdapter = slotTracker.ScheduleSlot as IScheduleSlot;
//
//                 // Standard validation (teacher/group conflicts)
//                 if (!Validators.StandartValidation(slotAdapter, assignedSlots))
//                     return false;
//
//                 // User-defined slot validators
//                 foreach (var validator in UserFunctions.SlotValidators)
//                 {
//                     if (!validator.Validate(slotAdapter, assignedSlots))
//                         return false;
//                 }
//
//                 return true;
//             }
//             finally
//             {
//                 // Restore original values
//                 slotTracker.ScheduleSlot.Date = originalDate;
//                 slotTracker.ScheduleSlot.PairNumber = originalPair;
//             }
//         }
//         
//         private Dictionary<IScheduleSlot, SlotTracker> CloneSlotTrackers()
//         {
//             var cloned = new Dictionary<IScheduleSlot, SlotTracker>();
//             foreach (var kvp in Slots)
//             {
//                 var newTracker = new SlotTracker
//                 {
//                     ScheduleSlot = kvp.Value.ScheduleSlot,
//                     AvailableDomains = new SortedSet<DomainValue>(kvp.Value.AvailableDomains),
//                     IsAssigned = false,
//                     SeriesId = kvp.Value.SeriesId,
//                     SeriesLength = kvp.Value.SeriesLength,
//                     WeekShift = kvp.Value.WeekShift,
//                     IsFirstTrackerInSeries = kvp.Value.IsFirstTrackerInSeries,
//                     IsLowDaysDanger = kvp.Value.IsLowDaysDanger
//                 };
//                 cloned[kvp.Key] = newTracker;
//             }
//             return cloned;
//         }
//
//         #endregion
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
//         
//         private int EstimateSlotValue(SlotTracker slotTracker, DomainValue domain, IAssignedSlots assignedSLots)
//         {
//             // if (slotTracker.IsAssigned) throw new ArgumentException("Отакої!");
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
//
//         #endregion
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
//
//         }
//
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
//         #region Genetic Evolution
//         
//                 /// <summary>
//         /// Crossover operator - combines two parent individuals
//         /// </summary>
//         public static Individual Crossover(Individual parent1, Individual parent2, Random random)
//         {
//             var child = new Individual();
//             
//             // Simple uniform crossover - randomly choose assignment from either parent
//             var allSlots = parent1.Assignments.Keys.Union(parent2.Assignments.Keys).ToList();
//             
//             foreach (var slot in allSlots)
//             {
//                 if (random.NextDouble() < 0.5 && parent1.Assignments.ContainsKey(slot))
//                 {
//                     child.Assignments[slot] = parent1.Assignments[slot];
//                 }
//                 else if (parent2.Assignments.ContainsKey(slot))
//                 {
//                     child.Assignments[slot] = parent2.Assignments[slot];
//                 }
//             }
//
//             return child;
//         }
//
//         /// <summary>
//         /// Mutation operator - randomly changes some assignments
//         /// </summary>
//         public static void Mutate(Individual individual, List<DomainValue> allDomains, 
//             Dictionary<IScheduleSlot, SlotTracker> masterSlots, double mutationRate, Random random)
//         {
//             foreach (var assignment in individual.Assignments.ToList())
//             {
//                 if (random.NextDouble() < mutationRate)
//                 {
//                     var slot = assignment.Key;
//                     var tracker = masterSlots[slot];
//                     
//                     // Choose a random domain from available domains
//                     var availableDomains = tracker.AvailableDomains.ToList();
//                     if (availableDomains.Any())
//                     {
//                         var newDomain = availableDomains[random.Next(availableDomains.Count)];
//                         individual.Assignments[slot] = newDomain;
//                     }
//                 }
//             }
//         }
//
//         /// <summary>
//         /// Tournament selection for choosing parents
//         /// </summary>
//         private Individual TournamentSelection(List<Individual> population, int tournamentSize, Random random)
//         {
//             var tournament = new List<Individual>();
//             for (int i = 0; i < tournamentSize; i++)
//             {
//                 tournament.Add(population[random.Next(population.Count)]);
//             }
//             
//             return tournament.OrderByDescending(ind => ind.Fitness).First();
//         }
//
//         /// <summary>
//         /// Repairs an individual to make it more feasible
//         /// </summary>
//         private void RepairIndividual(Individual individual, 
//             Dictionary<IScheduleSlot, SlotTracker> masterSlots,
//             UserFunctions userFunctions, bool ignoreClassrooms, Random random)
//         {
//             var assignedSlots = GetAssignedSlots();
//             var conflictingSlots = new List<ScheduleSlotDTO>();
//
//             // First pass: identify conflicts
//             foreach (var assignment in individual.Assignments)
//             {
//                 var slot = assignment.Key;
//                 var domain = assignment.Value;
//                 
//                 // Check if this assignment creates conflicts
//                 if (HasConflicts(slot, domain, assignedSlots))
//                 {
//                     conflictingSlots.Add(slot);
//                 }
//                 else
//                 {
//                     //assignedSlots.AddSlot(slot); todo
//                 }
//             }
//
//             // Second pass: fix conflicts by reassigning
//             foreach (var slot in conflictingSlots)
//             {
//                 if (masterSlots.TryGetValue(slot, out var tracker))
//                 {
//                     var validDomains = tracker.AvailableDomains
//                         .Where(domain => !HasConflicts(slot, domain, assignedSlots))
//                         .ToList();
//
//                     if (validDomains.Any())
//                     {
//                         var newDomain = validDomains[random.Next(validDomains.Count)];
//                         individual.Assignments[slot] = newDomain;
//                         //assignedSlots.AddSlot(slot); todo
//                     }
//                     else
//                     {
//                         // Remove assignment if no valid domain found
//                         individual.Assignments.Remove(slot);
//                     }
//                 }
//             }
//         }
//
//         private bool HasConflicts(ScheduleSlotDTO slot, DomainValue domain, AssignedSlotsDTO assignedSlots)
//         {
//             // Check teacher conflicts
//             var teacherSlots = assignedSlots.GetSlotsByTeacherAndDate(slot.GroupSubject.Teacher.Id, domain.Date);
//             if (teacherSlots.Any(s => s.PairNumber == domain.PairNumber))
//                 return true;
//
//             // Check group conflicts
//             foreach (var group in slot.GroupSubject.Groups)
//             {
//                 var groupSlots = assignedSlots.GetSlotsByGroupAndDate(group.Id, domain.Date);
//                 if (groupSlots.Any(s => s.PairNumber == domain.PairNumber))
//                     return true;
//             }
//
//             return false;
//         }
//         
//         private List<Individual> Evolve(List<Individual> population, 
//             Dictionary<IScheduleSlot, SlotTracker> masterSlots,
//             List<DomainValue> allDomains, UserFunctions userFunctions,
//             bool ignoreClassrooms, int generations, ILogger logger,
//             CancellationToken cancellationToken = default)
//         {
//             var random = new Random();
//             var currentPopulation = population.ToList();
//
//             var scheduleEstimation = 0;
//             foreach (var s in UserFunctions.ScheduleEstimations.ToList())
//             {
//                 var extScore = s.Estimate(Root);
//                 logger.LogDebug($"{algorithmName}: {s.Name} - {s.Id} - {extScore}");
//                 scheduleEstimation += extScore;
//             }
//             logger.LogInformation($"Population result: {scheduleEstimation}");
//
//
//             for (int generation = 0; generation < generations; generation++)
//             {
//                 if (cancellationToken.IsCancellationRequested)
//                     break;
//
//                 logger.LogDebug($"Generation {generation + 1}/{generations}");
//
//                 var newPopulation = new List<Individual>();
//
//                 // Elitism - keep best individuals
//                 var eliteCount = Math.Max(1, currentPopulation.Count / 10);
//                 var elite = currentPopulation
//                     .OrderByDescending(ind => ind.Fitness)
//                     .Take(eliteCount)
//                     .Select(ind => ind.Clone())
//                     .ToList();
//                 
//                 newPopulation.AddRange(elite);
//
//                 // Generate offspring through crossover and mutation
//                 while (newPopulation.Count < currentPopulation.Count)
//                 {
//                     var parent1 = TournamentSelection(currentPopulation, 3, random);
//                     var parent2 = TournamentSelection(currentPopulation, 3, random);
//
//                     var child = Crossover(parent1, parent2, random);
//                     Mutate(child, allDomains, masterSlots, 0.1, random);
//                     RepairIndividual(child, masterSlots, userFunctions, ignoreClassrooms, random);
//
//                     // Calculate fitness for child
//                     child.Fitness = CalculateFitness(child); // Would need to pass fitness calculation function
//
//                     newPopulation.Add(child);
//                 }
//
//                 currentPopulation = newPopulation;
//             }
//
//             return currentPopulation
//                 .OrderByDescending(ind => ind.Fitness)
//                 .ToList();
//         }
//
//         #endregion
//     }
// }