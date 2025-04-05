using AcaTime.Algorithm.Default.Models;
using AcaTime.Algorithm.Default.Utils;
using AcaTime.ScheduleCommon.Models.Calc;
using AcaTime.ScheduleCommon.Models.Constraints;
using AcaTime.ScheduleCommon.Utils;
using AcaTime.ScriptModels;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AcaTime.Algorithm.Default.Services
{

    /// <summary>
    /// Клас для реалізації алгоритму розкладу.
    /// </summary>
    public class DefaultScheduleAlgorithmUnit
    {
        /// <summary>
        /// Дані для розкладу
        /// </summary>
        public FacultySeasonDTO Root { get; private set; }
   

        /// <summary>
        /// Оцінки для розкладу
        /// </summary>
        public UserFunctions UserFunctions { get; set; }

        internal ILogger logger;

        private CancellationToken cancelToken;

        public Dictionary<IScheduleSlot, SlotTracker> Slots { get; internal set; }

       
        


        // для заміру часа виконання
        public DebugData DebugData { get; set; } = new DebugData("none");
        public AlgorithmParams Parameters { get; internal set; } 

        private string algorithmName = $"alg-{Guid.NewGuid()}";

        // додатковий кеш для прискорення деякіх функцій
        internal Dictionary<long, List<SlotTracker>> teacherSlots;
        internal Dictionary<long, List<SlotTracker>> groupsSlots;
        internal List<SlotTracker> FirstTrackers;
        private Dictionary<int, List<SlotTracker>> slotsByStep = new Dictionary<int, List<SlotTracker>>(); // для зберігання слотів по крокам
        private Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>> assignedSlotsByTeacherDate = new Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>>();
        private Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>> assignedSlotsByGroupDate = new Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>>();

        public DefaultScheduleAlgorithmUnit()
        {
        }

/// <summary>
/// Налаштування алгоритму
/// </summary>
/// <param name="root"></param>
/// <param name="logger"></param>
/// <param name="userFunctions"></param>
/// <param name="parameters"></param>
        public void Setup(FacultySeasonDTO root, ILogger logger, UserFunctions userFunctions, AlgorithmParams parameters)
        {
            Root = root;
            this.logger = logger;
            UserFunctions = userFunctions;
            Parameters = parameters;

        }

        /// <summary>
        /// Отримання адаптера слоту за слотом.
        /// </summary>
        /// <param name="slot">Слот, для якого потрібно отримати адаптер.</param>
        /// <returns>Адаптер слоту.</returns>
        /// <exception cref="KeyNotFoundException">Виникає, якщо адаптер для слоту не знайдено.</exception>
        public IScheduleSlot GetAdapter(ScheduleSlotDTO slot)
        {
            return slot;
        }



        public async Task<AlgorithmResultDTO> DoAll(CancellationToken token)
        {

            cancelToken = token;

            DebugData = new DebugData(algorithmName, true);

            //var thread = new Thread(() =>
            //{
            bool success = false;

            try
            {
                success = AssignSlots(1);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning($"{algorithmName}: Відміна роботи алгоритму");
                success = false;
            }


            var res = new AlgorithmResultDTO();

            if (success)
            {

                int scheduleEstimation = 0;
                foreach (var s in UserFunctions.ScheduleEstimations)
                {
                    var extScore = s.Estimate(Root);

                    logger.LogDebug($"{algorithmName}: {s.Name} - {s.Id} - {extScore}");

                    scheduleEstimation +=  extScore;
                }
                    

                DebugData.Step("total estimation");

                res.TotalEstimation = scheduleEstimation;
                res.ScheduleSlots = Slots.Values.Where(v => v.IsAssigned).Select(x => x.ScheduleSlot).ToList();
                res.Name = algorithmName;
                logger.LogInformation($"{algorithmName}: Schedule points {scheduleEstimation}");
            }

            DebugData.Step($"finish {success}");

            logger.LogInformation(DebugData.ToString());


            //}, 1000000000);
            //thread.Start();
            //thread.Join();


            return success ? res : null;

        }




        /// <summary>
        /// Рекурсивний метод призначення доменних значень для всіх слотів із forward checking.
        /// </summary>
        /// <param name="currentStep">Поточний крок рекурсії.</param>
        /// <returns>True, якщо рішення знайдено, інакше false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private bool AssignSlots(int currentStep)
        {

            if (cancelToken!=null && cancelToken.IsCancellationRequested)
            {
               cancelToken.ThrowIfCancellationRequested();
            }
                

            // if (currentStep == 377) return true;

            //if (currentStep % 10 == 0)
            //{
            //    var sb = new StringBuilder();
            //    sb.AppendLine($"step {currentStep}");
            //    sb.AppendLine(DebugData.ToString());
            //    sb.AppendLine($"{Slots.Values.Count(x => x.IsAssigned)}/{Slots.Values.Count}");
            //    Console.WriteLine(sb.ToString());
            //    // throw new Exception(sb.ToString());
            //}

            if (!slotsByStep.ContainsKey(currentStep))
                slotsByStep[currentStep] = new List<SlotTracker>();

            DebugData.Step("start");
            

         

            // Отримуємо наступний незаповнений слот із застосуванням MRV та пріоритету для лекцій.
            SlotTracker nextSlot = GetNextUnassignedSlot();
            if (nextSlot == null)
            {
                // Якщо всі слоти вже заповнені – повертаємо успіх.
                if (Slots.Values.All(s => s.IsAssigned))
                {
                    return true;
                }
                DebugData.Step("check result");
                return false;
            }
            DebugData.Step("next slot");

            var assignedSLots = getAssignedSlots();
    

            List<DomainValue> candidateDomains = GetSortedDomains(nextSlot, assignedSLots);

          
            //if (!candidateDomains.Any())
            //{
            //    Console.WriteLine($"{currentStep} Fail candidateDomains {nextSlot.ScheduleSlot.GroupSubject.Subject.Name}");
            //}

            DebugData.Step("candidateDomains");
            foreach (var domain in candidateDomains)
            {
                var vld = ValidateAssignment(nextSlot, domain, assignedSLots);
                DebugData.Step("ValidateAssignment");
                // Перевірка базових обмежень для даного слоту.
                if (vld)
                {
                    // Призначаємо домен для слоту.
                  
                    SetSlotAssigned(nextSlot, domain, currentStep);
                    DebugData.Step("assign");
                    var syncCheck = ApplySynchronizedDomainPattern(nextSlot, assignedSLots);
                    DebugData.Step("ApplySynchronizedDomainPattern");

                    if (syncCheck)
                    {
                        var frwdcheck = ForwardCheck(nextSlot, currentStep);
                        DebugData.Step("ForwardCheck");

                        // Виконуємо forward checking із збереженням вилучених значень через RejectedDomains.
                        if (frwdcheck)
                        {
                            // Рекурсивно пробуємо наступні призначення.
                            if (AssignSlots(currentStep + 1))
                            {
                                return true;
                            }
                        }
                        else
                        {
                            //   Console.WriteLine($"{currentStep} Fail ForwardCheck {nextSlot.ScheduleSlot.GroupSubject.Subject.Name}");
                        }
                    }
                    else
                    {
                     //   Console.WriteLine($"{currentStep} Fail ApplySynchronizedDomainPattern {nextSlot.ScheduleSlot.GroupSubject.Subject.Name}");
                    }

                 

                    // Якщо подальші призначення не дали рішення, відновлюємо вилучені доменні значення.
                    foreach (var slot in slotsByStep[currentStep])
                    {
                        slot.RestoreRejectedDomains(currentStep);
                    }
                    DebugData.Step("RestoreRejectedDomains");

                    // Відновлюємо призначені значення 
                    foreach (var slot in slotsByStep[currentStep].Where(x => x.IsAssigned && x.AssignStep == currentStep))
                    {
                        SetSlotUnAssigned(slot);
                    }

                    slotsByStep[currentStep].Clear();

                    DebugData.Step("unassign");
                }
            }
            DebugData.Step("end");
            return false;
        }


        private void SetSlotAssigned(SlotTracker slot, DomainValue val, int step)
        {
            slot.SetDomain(val, step);
            slot.IsAssigned = true;  
            slotsByStep[step].Add(slot);

            if (!assignedSlotsByTeacherDate.ContainsKey(slot.ScheduleSlot.GroupSubject.Teacher.Id))
                assignedSlotsByTeacherDate[slot.ScheduleSlot.GroupSubject.Teacher.Id] = new Dictionary<DateTime, HashSet<SlotTracker>>();

            if (!assignedSlotsByTeacherDate[slot.ScheduleSlot.GroupSubject.Teacher.Id].ContainsKey(val.Date))
                assignedSlotsByTeacherDate[slot.ScheduleSlot.GroupSubject.Teacher.Id][val.Date] = new HashSet<SlotTracker>();

            assignedSlotsByTeacherDate[slot.ScheduleSlot.GroupSubject.Teacher.Id][val.Date].Add(slot);


            foreach (var group in slot.ScheduleSlot.GroupSubject.Groups)
            {
                if (!assignedSlotsByGroupDate.ContainsKey(group.Id))
                    assignedSlotsByGroupDate[group.Id] = new Dictionary<DateTime, HashSet<SlotTracker>>();

                if (!assignedSlotsByGroupDate[group.Id].ContainsKey(val.Date))
                    assignedSlotsByGroupDate[group.Id][val.Date] = new HashSet<SlotTracker>();

                assignedSlotsByGroupDate[group.Id][val.Date].Add(slot);
            }
        }

        

        private void SetSlotUnAssigned(SlotTracker slot)
        {
            if (slot.IsAssigned)
            {
                assignedSlotsByTeacherDate[slot.ScheduleSlot.GroupSubject.Teacher.Id][slot.ScheduleSlot.Date].Remove(slot);
                foreach (var group in slot.ScheduleSlot.GroupSubject.Groups)
                {
                    assignedSlotsByGroupDate[group.Id][slot.ScheduleSlot.Date].Remove(slot);
                }
            }

            slot.IsAssigned = false;


        }


        /// <summary>
        /// Групуємо слоти по серіях.
        /// </summary>
        /// <param name="nextSlot"></param>
        /// <param name="assignedSLots"></param>
        /// <returns></returns>
        private List<DomainValue> GetSortedDomains(SlotTracker nextSlot, IAssignedSlots assignedSLots)
        {

            // Отримуємо список кандидатських доменів, відсортований за сумою оцінок.

            var res = nextSlot.AvailableDomains.ToList().ResortFirstK((d) => EvaluateAssignment(nextSlot, d, assignedSLots), Parameters.DomainsTopK, Parameters.DomainsTemperature);
            return res;
            //return nextSlot.AvailableDomains
            //    .OrderByDescending(domain => EvaluateAssignment(nextSlot, domain,assignedSLots))
            //    .ToList();
        }

        private bool ApplySynchronizedDomainPattern(SlotTracker currentTracker, AssignedSlotsDTO assignedSLots)
        {
            // Отримуємо предмет із поточного трекера
            var subject = currentTracker.ScheduleSlot.GroupSubject;

            // Отримуємо всі слот-трекери для цього предмету через список слотів GroupSubject.
            // Використовуємо лише ті, що є в глобальному словнику Slots і які ще не призначені.
            var freeTRackers = subject.ScheduleSlots
                .Select(slot => Slots[slot])
                .Where(tracker => !tracker.IsAssigned && tracker.SeriesId == currentTracker.SeriesId)
                .OrderBy(tracker => tracker.ScheduleSlot.LessonNumber)
                .ToList();

            if (!freeTRackers.Any()) return true;


            DateTime minAvailable = currentTracker.ScheduleSlot.Date;

            var nextDate = new DomainValue
            {
                Date = minAvailable.AddDays(currentTracker.WeekShift * 7),
                PairNumber = currentTracker.ScheduleSlot.PairNumber
            };
            var maxAvailable = freeTRackers.Select(x => x.AvailableDomains.Max()).Max();



            while (nextDate <= maxAvailable)
            {
              //  AssignedSlotsDTO assignedSLots = getAssignedSlots();
     
                var nextTracker = freeTRackers.FirstOrDefault(tracker => !tracker.IsAssigned
               && tracker.AvailableDomains.Contains(nextDate)
               && ValidateAssignment(tracker, nextDate, assignedSLots)

             );
                if (nextTracker != null)
                {
                    SetSlotAssigned(nextTracker, nextDate, currentTracker.AssignStep);
                    freeTRackers.Remove(nextTracker);
                }

                nextDate.Date = nextDate.Date.AddDays(currentTracker.WeekShift * 7);
            }

            var res = !freeTRackers.Any(tracker => !tracker.IsAssigned);

            return res;
        }



        /// <summary>
        /// Вибір наступного незаповненого слоту із застосуванням MRV та пріоритету для лекцій.
        /// </summary>
        /// <returns>Незаповнений слот, або null якщо таких немає.</returns>
        private SlotTracker GetNextUnassignedSlot()
        {
           return FirstTrackers.Where(s => !s.IsAssigned)
           // filter by having smallest series id group by GroupSubjectId 
           .GroupBy(s => s.ScheduleSlot.GroupSubject.Id)
           .Select(g => g.OrderBy(s => s.SeriesId).First())
           .ResortFirstK((s) => GetSlotPriorityScore(s), Parameters.SlotsTopK, Parameters.SlotsTemperature).FirstOrDefault();

            //return FirstTrackers
            //   .Where(s => !s.IsAssigned)

            //   .OrderBy(s => s.AvailableDomains.Count)                        // MRV: менше можливостей – вищий пріоритет.
            //   .ThenByDescending(s => s.SeriesLength)
            //   .ThenByDescending(s => s.ScheduleSlot.GroupSubject.Groups.Count) // Більше груп (лекція) – вищий пріоритет.
            //   .ThenByDescending(s => s.ScheduleSlot.GroupSubject.ScheduleSlots.Count(x => !Slots[x].IsAssigned))
            //   .ThenBy(s => s.ScheduleSlot.LessonNumber)
            //   .FirstOrDefault();
        }

        public double GetSlotPriorityScore(SlotTracker slot)
        {
            var sp = new SlotPriorityDTO
            {
                AvailableDomains = slot.AvailableDomains.Count,
                EndsOnIncompleteWeek = slot.IsLowDaysDanger,
                GroupCount = slot.ScheduleSlot.GroupSubject.Groups.Count,
                LessonSeriesLength = slot.SeriesLength,
                GroupSubject = slot.ScheduleSlot.GroupSubject
            };

            if (UserFunctions.SlotPriorities.Any())
            {
                return UserFunctions.SlotPriorities.Sum(x => x.Estimate(sp));
            }
            else

                return DefaultSlotPriority(sp);
        }

        public static int DefaultSlotPriority(SlotPriorityDTO slot)
        {
            int priority = slot.AvailableDomains*-10+slot.LessonSeriesLength*2+slot.GroupCount;

            if (slot.EndsOnIncompleteWeek)
                priority += 100;

            return priority;
        }


        /// <summary>
        /// Forward checking: оновлює домени для всіх незаповнених слотів, видаляючи кандидати,
        /// які вже не відповідають констрейнтам. Вилучені значення записуються в RejectedDomains для поточного кроку.
        /// </summary>
        /// <param name="assignedSlot">Слот, для якого зроблено останнє призначення.</param>
        /// <param name="currentStep">Поточний крок пошуку.</param>
        /// <returns>True, якщо для всіх слотів залишається хоча б один кандидат; інакше false.</returns>
        private bool ForwardCheck(SlotTracker assignedSlot, int currentStep)
        {

            var changedSlots = assignedSlot.ScheduleSlot.GroupSubject.ScheduleSlots.Select(x => Slots[x]).Where(x => x.IsAssigned && x.AssignStep == currentStep).ToList();


            HashSet<SlotTracker> forwardSlots = new HashSet<SlotTracker>(teacherSlots[assignedSlot.ScheduleSlot.GroupSubject.Teacher.Id].Where(s => !s.IsAssigned && s.IsFirstTrackerInSeries));
            foreach (var grId in assignedSlot.ScheduleSlot.GroupSubject.Groups.Select(g => g.Id))
                foreach (var sl in groupsSlots[grId].Where(s => !s.IsAssigned && s.IsFirstTrackerInSeries))
                    forwardSlots.Add(sl);

            foreach (var slot in forwardSlots)
            {
                // Зберігаємо поточний список доступних доменів.
                var originalDomains = new List<DomainValue>(slot.AvailableDomains);
                // Оновлюємо домени: залишаємо лише ті, що відповідають обмеженням.
                slot.AvailableDomains = new SortedSet<DomainValue>(slot.AvailableDomains
                    .Where(candidate => ValidateAssignmentArc(slot, candidate, changedSlots)))
                    ;
                // Визначаємо, які доменні значення було вилучено.
                var removed = originalDomains.Except(slot.AvailableDomains).ToList();
                if (removed.Any())
                {
                    if (!slot.RejectedDomains.ContainsKey(currentStep))
                        slot.RejectedDomains[currentStep] = new List<DomainValue>();
                    slot.RejectedDomains[currentStep].AddRange(removed);
                    slotsByStep[currentStep].Add(slot);
                }
                // Якщо домен став порожнім, повертаємо false.
                if (!slot.AvailableDomains.Any())
                {
                    return false;
                }
            }
            return true;
        }

       

        #region Реалізація методів перевірок та оцінки

        /// <summary>
        /// Стандартна перевірка для слотів.
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="assignedSLots"></param>
        /// <returns></returns>
        protected static bool StandartValidation(IScheduleSlot slot, IAssignedSlots assignedSLots)
        {
            var candidateTeacherId = slot.GroupSubject.Teacher.Id;

            var hasTeacherIntersection = assignedSLots.GetSlotsByTeacherAndDate(candidateTeacherId, slot.Date.Date)
                .Any(s => s.PairNumber == slot.PairNumber);
            if (hasTeacherIntersection)
                return false;

            HashSet<IScheduleSlot> slotsByGroup = new HashSet<IScheduleSlot>();

            foreach (var groupId in slot.GroupSubject.Groups.Select(g => g.Id).Distinct())
            {
                var slots = assignedSLots.GetSlotsByGroupAndDate(groupId, slot.Date.Date).Where(s => s.PairNumber == slot.PairNumber);
                if (slots != null)
                    slotsByGroup.UnionWith(slots);
            }

            foreach (var otherSlot in slotsByGroup)
            {
                // Перевірка для студентських груп.
                foreach (var candidateGroup in slot.GroupSubject.Groups)
                {
                    foreach (var otherGroup in otherSlot.GroupSubject.Groups)
                    {
                        if (candidateGroup.Id == otherGroup.Id)
                        {
                            // Дозволено, якщо SubgroupVariantId співпадає і SubgroupId різний.
                            if (!candidateGroup.SubgroupVariantId.HasValue ||
                                !otherGroup.SubgroupVariantId.HasValue ||
                                candidateGroup.SubgroupVariantId != otherGroup.SubgroupVariantId ||
                                candidateGroup.SubgroupId == otherGroup.SubgroupId)
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Перевірка призначення для слоту в контексті вже призначених слотів.
        /// </summary>
        /// <param name="slotTracker"></param>
        /// <param name="domain"></param>
        /// <param name="assignedSLots"></param>
        /// <returns></returns>
        protected bool ValidateAssignment(SlotTracker slotTracker, DomainValue domain, IAssignedSlots assignedSLots)
        {
            if (cancelToken != null && cancelToken.IsCancellationRequested)
            {
                cancelToken.ThrowIfCancellationRequested();
            }

            slotTracker.ScheduleSlot.Date = domain.Date;
            slotTracker.ScheduleSlot.PairNumber = domain.PairNumber;

            var slotAdapter = GetAdapter(slotTracker.ScheduleSlot);

           // DebugData.Step("GetAdapter");


            var standartValidation = StandartValidation(slotAdapter, assignedSLots);
          //  DebugData.Step("StandartValidation");
            if (!standartValidation)
                return false;

            

            foreach (var validator in UserFunctions.SlotValidators)
            {
                var userValidation = validator.Validate(slotAdapter, assignedSLots);
             //   DebugData.Step("UserFunctions.SlotValidators");
                if (!userValidation)
                    return false;
            }
           

            return true;
        }

        /// <summary>
        /// Отримання списку призначених слотів.
        /// </summary>
        /// <returns></returns>
        private AssignedSlotsDTO getAssignedSlots()
        {
              
            var res = new AssignedSlotsDTO(
                slotFactory: () => Slots.Values.Where(s => s.IsAssigned).Select(s => s.ScheduleSlot),
                slotsByTeacherFactory: getAssignedByTeacher,
                slotsByGroupFactory: getAssignedByGroup,
                slotsByTeacherAndDateFactory: getAssignedByTeacherAndDate,
                slotsByGroupAndDateFactory: getAssignedByGroupAndDate 
              );

            return res;
        }

        private IEnumerable<IScheduleSlot> getAssignedByGroupAndDate(long groupId, DateTime date)
        {
            IEnumerable<IScheduleSlot> res = assignedSlotsByGroupDate.ContainsKey(groupId) && assignedSlotsByGroupDate[groupId].ContainsKey(date) 
                ? assignedSlotsByGroupDate[groupId][date].Select(s => s.ScheduleSlot) 
                : new List<IScheduleSlot>();
            return res;
        }

        private IEnumerable<IScheduleSlot> getAssignedByTeacherAndDate(long teacherId, DateTime date)
        {
            IEnumerable<IScheduleSlot> res = assignedSlotsByTeacherDate.ContainsKey(teacherId) && assignedSlotsByTeacherDate[teacherId].ContainsKey(date) 
                ? assignedSlotsByTeacherDate[teacherId][date].Select(s => s.ScheduleSlot) 
                : new List<IScheduleSlot>();
            return res;
        }

        private IEnumerable<IScheduleSlot> getAssignedByGroup(long groupId)
        {
            IEnumerable<IScheduleSlot> res2 = assignedSlotsByGroupDate.ContainsKey(groupId) 
                ? assignedSlotsByGroupDate[groupId].Values.SelectMany(x => x.Select(s => s.ScheduleSlot as IScheduleSlot)) 
                : new List<IScheduleSlot>();
            return res2;
        }

        private IEnumerable<IScheduleSlot> getAssignedByTeacher(long teacherId)
        {
            List<IScheduleSlot> res2 = assignedSlotsByTeacherDate.ContainsKey(teacherId) 
                ? assignedSlotsByTeacherDate[teacherId].Values.SelectMany(x => x.Select(s => s.ScheduleSlot as IScheduleSlot)).ToList() 
                : new List<IScheduleSlot>();
            return res2;
        }

        /// <summary>
        /// Перевірка доменного значення для слоту в контексті вже призначених слотів.
        /// </summary>
        /// <param name="slotTracker"></param>
        /// <param name="domain"></param>
        /// <param name="assigned"></param>
        /// <returns></returns>
        protected bool ValidateAssignmentArc(SlotTracker slotTracker, DomainValue domain, List<SlotTracker> assigned)
        {
            var candidateTeacherId = slotTracker.ScheduleSlot.GroupSubject.Teacher.Id;
            foreach (var otherSlot in assigned)
            {
                // Перевірка, чи відбувається призначення в один і той самий час.
                if (otherSlot.ScheduleSlot.Date.Date == domain.Date.Date && otherSlot.ScheduleSlot.PairNumber == domain.PairNumber)
                {
                    // Перевірка викладача.
                    var otherTeacherId = otherSlot.ScheduleSlot.GroupSubject.Teacher.Id;
                    if (otherTeacherId == candidateTeacherId)
                    {
                        return false;
                    }
                    // Перевірка для студентських груп.
                    foreach (var candidateGroup in slotTracker.ScheduleSlot.GroupSubject.Groups)
                    {
                        foreach (var otherGroup in otherSlot.ScheduleSlot.GroupSubject.Groups)
                        {
                            if (candidateGroup.Id == otherGroup.Id)
                            {


                                // Дозволено, якщо SubgroupVariantId співпадає і SubgroupId різний.
                                if (!candidateGroup.SubgroupVariantId.HasValue ||
                                    !otherGroup.SubgroupVariantId.HasValue ||
                                    candidateGroup.SubgroupVariantId != otherGroup.SubgroupVariantId ||
                                    candidateGroup.SubgroupId == otherGroup.SubgroupId)
                                {
                                    return false;
                                }
                            }
                        }

                    }
                }
            }

            return true;
        }

        public static int DefaultSlotEstimation(IScheduleSlot slot, IAssignedSlots assignedSLots)
        {
            int score = 0;
            // Оцінка компактності для викладача.
            var candidateTeacherId = slot.GroupSubject.Teacher.Id;

            var teacherAdjacentSlots = assignedSLots.GetSlotsByTeacher(candidateTeacherId)
               .Where(s => s.Date.Date == slot.Date.Date)
                .Select(s => s.PairNumber)
                .ToList();

            foreach (var pair in teacherAdjacentSlots)
            {
                if (Math.Abs(pair - slot.PairNumber) == 1)
                {
                    score += 1;
                }
            }
            // Оцінка компактності для студентських груп.
            foreach (var candidateGroup in slot.GroupSubject.Groups)
            {

                //   Dictionary<long,List<long>> variants

                var groupAdjacentSlots = assignedSLots.GetSlotsByGroup(candidateGroup.Id)
                    .Where(s => s.Date.Date == slot.Date.Date)
                    .ToList();

                foreach (var pair in groupAdjacentSlots)
                {
                    if (Math.Abs(pair.PairNumber - slot.PairNumber) == 1)
                    {
                        score += 3;
                    }

                    if (pair.PairNumber == slot.PairNumber) // Склєйка підгруп
                    {
                        score += 5;

                        if (pair.LessonSeriesLength == slot.LessonSeriesLength) // Довжина серії уроків співпадає
                            score += 12;

                        if (pair.GroupSubject.Subject.Id == slot.GroupSubject.Subject.Id) // Один і той сами предмет для різних підгруп
                            score += 10;
                    }
                }

                var variants = groupAdjacentSlots.Union([slot]).SelectMany(x => x.GroupSubject.Groups.Where(g => g.Id == candidateGroup.Id)).Select(x => new { variant = x.SubgroupVariantId ?? 0, subgroup = x.SubgroupId ?? 0 });
                var maxInVariants = variants
 .GroupBy(v => v.variant)
 .Select(g => new
 {
     Variant = g.Key,
     MaxCount = g.GroupBy(x => x.subgroup)
                 .Max(subGroup => subGroup.Count())
 })
 .ToList();

                var maxStudentLessons = maxInVariants.Sum(x => x.MaxCount);

                if (maxStudentLessons > 3)
                    score -= 1000;

            }
            return score;
        }

        protected int EvaluateAssignment(SlotTracker slotTracker, DomainValue domain, IAssignedSlots assignedSLots)
        {
            if (slotTracker.IsAssigned) throw new ArgumentException("WTF!");

            slotTracker.ScheduleSlot.Date = domain.Date;
            slotTracker.ScheduleSlot.PairNumber = domain.PairNumber;

            var slotAdapter = GetAdapter(slotTracker.ScheduleSlot);

            int score = 0;
            
            if (UserFunctions.ScheduleSlotEstimations.Any())
            {
                foreach (var estimation in UserFunctions.ScheduleSlotEstimations)
                {
                    score += estimation.Estimate(slotAdapter, assignedSLots);
                }
            }
            else
                score += DefaultSlotEstimation(slotAdapter, assignedSLots);
            return score;
        }


        #endregion




    }
}