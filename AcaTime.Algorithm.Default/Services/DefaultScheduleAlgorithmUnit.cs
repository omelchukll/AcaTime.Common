using AcaTime.Algorithm.Default.Models;
using AcaTime.Algorithm.Default.Services.Calc;
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

        public AlgorithmParams Parameters { get; internal set; }
        private string algorithmName = $"alg-{Guid.NewGuid()}";
        internal ILogger logger;
        private CancellationToken cancelToken;

        public Dictionary<IScheduleSlot, SlotTracker> Slots { get; internal set; }

        // для заміру часа виконання
        public DebugData DebugData { get; set; } = new DebugData("none");

        // додатковий кеш для прискорення деякіх функцій
        internal Dictionary<long, List<SlotTracker>> teacherSlots;
        internal Dictionary<long, List<SlotTracker>> groupsSlots;
        internal List<SlotTracker> FirstTrackers;
        private Dictionary<int, List<SlotTracker>> slotsByStep = new Dictionary<int, List<SlotTracker>>(); // для зберігання слотів по крокам
        private Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>> assignedSlotsByTeacherDate = new Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>>();
        private Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>> assignedSlotsByGroupDate = new Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>>();

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

        #region Основні функції алгоритму

        /// <summary>
        /// Виконання алгоритму.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<AlgorithmResultDTO> Run(CancellationToken token)
        {
            cancelToken = token;
            DebugData = new DebugData(algorithmName, true);

            bool success = false;

            try
            {
                // Запуск рекурсії для пошуку розкладу
                success = AssignSlots(1);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning($"{algorithmName}: Скасування роботи алгоритму");
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
                    scheduleEstimation += extScore;
                }

                DebugData.Step("total estimation");

                res.TotalEstimation = scheduleEstimation;
                res.ScheduleSlots = Slots.Values.Where(v => v.IsAssigned).Select(x => x.ScheduleSlot).ToList();
                res.Name = algorithmName;
                logger.LogInformation($"{algorithmName}: Schedule points {scheduleEstimation}");
            }

            DebugData.Step($"finish {success}");
            logger.LogInformation(DebugData.ToString());

            return success ? res : null;
        }

        /// <summary>
        /// Рекурсивний метод призначення доменних значень для всіх слотів.
        /// </summary>
        /// <param name="currentStep">Поточний крок рекурсії.</param>
        /// <returns>True, якщо рішення знайдено, інакше false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private bool AssignSlots(int currentStep)
        {
            if (cancelToken != null && cancelToken.IsCancellationRequested)
                cancelToken.ThrowIfCancellationRequested();

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

            var assignedSLots = GetAssignedSlots();

            List<DomainValue> candidateDomains = GetSortedDomains(nextSlot, assignedSLots);
            DebugData.Step("candidateDomains");

            foreach (var domain in candidateDomains)
            {
                var vld = ValidateAssignment(nextSlot, domain, assignedSLots);
                DebugData.Step("ValidateAssignment");

                if (vld)
                {
                    SetSlotAssigned(nextSlot, domain, currentStep);
                    DebugData.Step("assign");

                    var syncCheck = ApplySynchronizedDomainPattern(nextSlot, assignedSLots);
                    DebugData.Step("ApplySynchronizedDomainPattern");

                    if (syncCheck)
                    {
                        var frwdcheck = ForwardCheck(nextSlot, currentStep);
                        DebugData.Step("ForwardCheck");

                        if (frwdcheck)
                        {
                            // Рекурсивно пробуємо наступні призначення.
                            if (AssignSlots(currentStep + 1))
                            {
                                // Якщо рекурсія повертає true, то ми знайшли рішення! Виходимо з рекурсії.
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

                    // Якщо не пройшла валідація або рекурсія не знайшла рішення, скидаємо призначення та пробуємо наступне доменне значення.

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
                    DebugData.Step("unassign");

                    slotsByStep[currentStep].Clear();
                }
            }

            // Якщо жодне доменне значення не підходить, повертаємо false.
            DebugData.Step("end");
            return false;
        }

        /// <summary>
        /// Застосовуємо доменні значення для всієї серії на основі першого слоту серії, якщо можливо.
        /// </summary>
        /// <param name="currentTracker"></param>
        /// <param name="assignedSLots"></param>
        /// <returns></returns>
        private bool ApplySynchronizedDomainPattern(SlotTracker currentTracker, AssignedSlotsDTO assignedSLots)
        {
            // Отримуємо предмет із поточного трекера
            var subject = currentTracker.ScheduleSlot.GroupSubject;

            // Отримуємо всі слот-трекери для цього предмету через список слотів GroupSubject.
            // Використовуємо лише ще не призначені з серії. 
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
            .ResortFirstK((s) => EstimateSlot(s), Parameters.SlotsTopK, Parameters.SlotsTemperature).FirstOrDefault();
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
                    .Where(candidate => Validators.ValidateAssignmentArc(slot, candidate, changedSlots)))
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

        /// <summary>
        /// Групуємо слоти по серіях.
        /// </summary>
        /// <param name="nextSlot"></param>
        /// <param name="assignedSLots"></param>
        /// <returns></returns>
        private List<DomainValue> GetSortedDomains(SlotTracker nextSlot, IAssignedSlots assignedSLots)
        {
            var res = nextSlot.AvailableDomains.ToList()
                .ResortFirstK((d) => EstimateSlotValue(nextSlot, d, assignedSLots), Parameters.DomainsTopK, Parameters.DomainsTemperature);
            return res;
        }

        #endregion

        #region Перевірка та оцінки

        /// <summary>
        /// Отримання оцінки слоту при воборі як наступного для пошуку розкладу.
        /// </summary>
        /// <param name="slot"></param>
        /// <returns></returns>
        private double EstimateSlot(SlotTracker slot)
        {
            var sp = new SlotEstimationDTO
            {
                AvailableDomains = slot.AvailableDomains.Count,
                EndsOnIncompleteWeek = slot.IsLowDaysDanger,
                GroupCount = slot.ScheduleSlot.GroupSubject.Groups.Count,
                LessonSeriesLength = slot.SeriesLength,
                GroupSubject = slot.ScheduleSlot.GroupSubject
            };

            if (UserFunctions.SlotEstimations.Any())
                return UserFunctions.SlotEstimations.Sum(x => x.Estimate(sp));
            else
                return Estimations.DefaultSlotEstimation(sp);
        }

        /// <summary>
        /// Перевірка призначення для слоту в контексті вже призначених слотів.
        /// </summary>
        /// <param name="slotTracker"></param>
        /// <param name="domain"></param>
        /// <param name="assignedSLots"></param>
        /// <returns></returns>
        private bool ValidateAssignment(SlotTracker slotTracker, DomainValue domain, IAssignedSlots assignedSLots)
        {
            slotTracker.ScheduleSlot.Date = domain.Date;
            slotTracker.ScheduleSlot.PairNumber = domain.PairNumber;

            var slotAdapter = GetAdapter(slotTracker.ScheduleSlot);

            var standartValidation = Validators.StandartValidation(slotAdapter, assignedSLots);
            if (!standartValidation)
                return false;

            foreach (var validator in UserFunctions.SlotValidators)
            {
                var userValidation = validator.Validate(slotAdapter, assignedSLots);
                if (!userValidation)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Оцінка слоту для вибору як наступного слоту в розподілі.
        /// </summary>
        /// <param name="slotTracker"></param>
        /// <param name="domain"></param>
        /// <param name="assignedSLots"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private int EstimateSlotValue(SlotTracker slotTracker, DomainValue domain, IAssignedSlots assignedSLots)
        {
            if (slotTracker.IsAssigned) throw new ArgumentException("WTF!");

            slotTracker.ScheduleSlot.Date = domain.Date;
            slotTracker.ScheduleSlot.PairNumber = domain.PairNumber;

            var slotAdapter = GetAdapter(slotTracker.ScheduleSlot);

            int score = 0;

            if (UserFunctions.SlotValueEstimations.Any())
            {
                foreach (var estimation in UserFunctions.SlotValueEstimations)
                    score += estimation.Estimate(slotAdapter, assignedSLots);
            }
            else
                score += Estimations.DefaultSlotValueEstimation(slotAdapter, assignedSLots);
            return score;
        }

        #endregion

        #region Utils

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

        /// <summary>
        /// Призначає домен для слоту. Оновлюємо кеші.
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="val"></param>
        /// <param name="step"></param>
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

        /// <summary>
        /// Відміна призначення слоту. Оновлюємо кеші.
        /// </summary>
        /// <param name="slot"></param>
        private void SetSlotUnAssigned(SlotTracker slot)
        {
            if (slot.IsAssigned)
            {
                assignedSlotsByTeacherDate[slot.ScheduleSlot.GroupSubject.Teacher.Id][slot.ScheduleSlot.Date].Remove(slot);
                foreach (var group in slot.ScheduleSlot.GroupSubject.Groups)
                    assignedSlotsByGroupDate[group.Id][slot.ScheduleSlot.Date].Remove(slot);
            }
            slot.IsAssigned = false;
        }

        /// <summary>
        /// Отримання списку призначених слотів.
        /// </summary>
        /// <returns></returns>
        private AssignedSlotsDTO GetAssignedSlots()
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

        #endregion
    }
}