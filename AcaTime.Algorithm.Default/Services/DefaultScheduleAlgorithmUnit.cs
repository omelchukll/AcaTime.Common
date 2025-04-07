using AcaTime.Algorithm.Default.Models;
using AcaTime.Algorithm.Default.Services.Calc;
using AcaTime.Algorithm.Default.Utils;
using AcaTime.ScheduleCommon.Models.Calc;
using AcaTime.ScheduleCommon.Models.Constraints;
using AcaTime.ScheduleCommon.Utils;
using AcaTime.ScriptModels;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
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

        public bool ignoreClassrooms { get; private set; }
        public Dictionary<IScheduleSlot, SlotTracker> Slots { get; internal set; }

        // для заміру часа виконання
        public DebugData DebugData { get; set; } = new DebugData("none");

        // додатковий кеш для прискорення деякіх функцій, клонується в Clone
        internal Dictionary<long, List<SlotTracker>> teacherSlots;
        internal Dictionary<long, List<SlotTracker>> groupsSlots;
        internal List<SlotTracker> FirstTrackers;

        // приватний кеш
        private Dictionary<int, List<SlotTracker>> slotsByStep = new Dictionary<int, List<SlotTracker>>(); // для зберігання слотів по крокам
        private Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>> assignedSlotsByTeacherDate = new Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>>();
        private Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>> assignedSlotsByGroupDate = new Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>>();
        private HashSet<SlotTracker> unassignedFirstSlots;
        private Dictionary<long, List<SlotTracker>> firstSlotsByGroupSubjects;
        private Dictionary<DateTime, Dictionary<int, Dictionary<ClassroomDTO, ScheduleSlotDTO>>> assignedClassrooms = new Dictionary<DateTime, Dictionary<int, Dictionary<ClassroomDTO, ScheduleSlotDTO>>>();


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

        private void PreparePrivateCash() 
        {
            firstSlotsByGroupSubjects = FirstTrackers
                .Where(x => !x.IsAssigned && x.IsFirstTrackerInSeries)
                .GroupBy(s => s.ScheduleSlot.GroupSubject.Id)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SeriesId).ToList());

            unassignedFirstSlots = firstSlotsByGroupSubjects.Values.Select(x => x.First())
                .ToHashSet();
        }


        #region Основні функції алгоритму

        /// <summary>
        /// Виконання алгоритму.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<AlgorithmResultDTO> Run(CancellationToken token, bool ignoreClassrooms)
        {
            cancelToken = token;
            this.ignoreClassrooms = ignoreClassrooms;
            DebugData = new DebugData(algorithmName, true);

            PreparePrivateCash();

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
            SlotTracker? nextSlot = GetNextUnassignedSlot();            

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


            // оновлення кешу для перших слотів
            ResetUnAssignedFirstSlots(nextSlot);

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

            // Відновлюємо кеш для перших слотів.
            RestoreUnAssignedFirstSlots(nextSlot);

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
        private SlotTracker? GetNextUnassignedSlot()
        {
           return unassignedFirstSlots           
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

            // Перевірка на аудиторії, якщо алгоритм має враховувати їх
            if (!ignoreClassrooms && !slotTracker.ScheduleSlot.GroupSubject.Subject.NoClassroom)
            {
                var classroomValidation = ValidateAndSelectClassroom(slotTracker.ScheduleSlot, domain, assignedSLots);
                if (!classroomValidation)
                    return false;
            }

            foreach (var validator in UserFunctions.SlotValidators)
            {
                var userValidation = validator.Validate(slotAdapter, assignedSLots);
                if (!userValidation)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Перевіряє, чи є доступні аудиторії для даного слоту у вказаний час і вибирає найкращу
        /// </summary>
        /// <param name="slot">Слот розкладу</param>
        /// <param name="domain">Доменне значення (дата і номер пари)</param>
        /// <param name="assignedSLots">Вже призначені слоти</param>
        /// <returns>true, якщо є доступні аудиторії, false - якщо немає</returns>
        private bool ValidateAndSelectClassroom(ScheduleSlotDTO slot, DomainValue domain, IAssignedSlots assignedSLots)
        {
            // Якщо предмет не потребує аудиторії, перевірка не потрібна
            if (ignoreClassrooms || slot.GroupSubject.Subject.NoClassroom)
                return true;

            // Кількість студентів, для яких потрібна аудиторія
            int requiredStudentCount = slot.GroupSubject.StudentCount;

            // Отримання списку підходящих аудиторій за типом
            var requiredClassroomTypes = slot.GroupSubject.Subject.ClassroomTypes
                .Select(ct => ct.ClassroomTypeId)
                .ToHashSet();

            // Отримуємо пріоритетний тип аудиторії, враховуючи сортування в ClassroomTypes згідно приоритету
            var priorityClassroomTypeId = slot.GroupSubject.Subject.ClassroomTypes.FirstOrDefault()?.ClassroomTypeId;

            if (priorityClassroomTypeId == null)
                throw new Exception($"Не вказаний тип аудиторії для предмету {slot.GroupSubject.Subject.Name}");

            // Фільтр для аудиторій, які підходять за типом та розміром
            var filterLambda = (ClassroomDTO x) => x.StudentCount >= requiredStudentCount && x.ClassroomTypes.Any(ct => requiredClassroomTypes.Contains(ct.ClassroomTypeId));
            
            // Стандартний жадібний метод
            var freeClassrooms = (assignedClassrooms.ContainsKey(domain.Date) && assignedClassrooms[domain.Date].ContainsKey(domain.PairNumber)) 
                ? Root.Classrooms.Where(x => !assignedClassrooms[domain.Date][domain.PairNumber].ContainsKey(x) && filterLambda(x)) 
                : Root.Classrooms.Where(filterLambda);

            // Сортування аудиторій за пріоритетом
            var sortLambda = (ClassroomDTO x) => {
                if (x.ClassroomTypes.First().ClassroomTypeId == priorityClassroomTypeId)
                    return 1000000;
                if (requiredClassroomTypes.Contains(x.ClassroomTypes.First().ClassroomTypeId))
                    return 1000;
                return 100;
            };

            // Вибір найкращої аудиторії
            var classrooms = freeClassrooms.OrderByDescending(x => sortLambda(x)).ThenBy(x => x.StudentCount).FirstOrDefault();
            
            // Якщо жадібний алгоритм знайшов аудиторію, використовуємо її
            if (classrooms != null)
            {
                // Призначаємо аудиторію
                slot.Classroom = classrooms;
                return true;
            }
            
            // Якщо жадібний алгоритм не знайшов аудиторію, спробуємо використати алгоритм Хопкрофта-Карпа
            // для оптимального перерозподілу аудиторій
            return TryReallocateClassroomsWithHopcroftKarp(slot, domain);
        }

        /// <summary>
        /// Намагається перерозподілити аудиторії за допомогою алгоритму Хопкрофта-Карпа
        /// </summary>
        /// <param name="slot">Слот, для якого потрібна аудиторія</param>
        /// <param name="domain">Доменне значення (дата і номер пари)</param>
        /// <returns>true, якщо вдалося призначити аудиторію, false - якщо ні</returns>
        private bool TryReallocateClassroomsWithHopcroftKarp(ScheduleSlotDTO slot, DomainValue domain)
        {
            // Якщо немає призначених аудиторій або немає потреби в аудиторії, виходимо
            if (!assignedClassrooms.ContainsKey(domain.Date) || 
                !assignedClassrooms[domain.Date].ContainsKey(domain.PairNumber) || 
                slot.GroupSubject.Subject.NoClassroom)
                return false;
            
            // Збираємо всі слоти, включаючи поточний, для яких потрібно призначити аудиторії
            var slotsWithClassrooms = new List<ScheduleSlotDTO>(
                assignedClassrooms[domain.Date][domain.PairNumber].Values);
            slotsWithClassrooms.Add(slot);
            
            // Виконуємо алгоритм Хопкрофта-Карпа для пошуку оптимального розподілу
            var bipartiteMatching = HopcroftKarpAlgorithm.FindOptimalClassroomAssignment(slotsWithClassrooms, Root.Classrooms);
            
            // Якщо знайдено розподіл і в ньому є поточний слот
            if (bipartiteMatching != null && bipartiteMatching.ContainsKey(slot) && bipartiteMatching.Count == slotsWithClassrooms.Count)
            {
                // Тимчасово запам'ятовуємо призначення для нового слоту
                var assignedClassroom = bipartiteMatching[slot];
                
                // Не застосовуємо зміни одразу, лише призначаємо аудиторію для поточного слоту
                slot.Classroom = assignedClassroom;

                assignedClassrooms[domain.Date][domain.PairNumber] = new Dictionary<ClassroomDTO, ScheduleSlotDTO>();

                foreach (var pair in bipartiteMatching.Where(x => x.Key != slot))
                {
                    assignedClassrooms[domain.Date][domain.PairNumber][pair.Value] = pair.Key;
                    pair.Key.Classroom = pair.Value;
                }
                
                return true;
            }            
            return false;
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
            if (slotTracker.IsAssigned) throw new ArgumentException("Отакої!");

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
            
            if (slot.ScheduleSlot.Classroom != null)
            {
                if (!assignedClassrooms.ContainsKey(val.Date))
                    assignedClassrooms[val.Date] = new Dictionary<int, Dictionary<ClassroomDTO, ScheduleSlotDTO>>();
                
                if (!assignedClassrooms[val.Date].ContainsKey(val.PairNumber))
                    assignedClassrooms[val.Date][val.PairNumber] = new Dictionary<ClassroomDTO, ScheduleSlotDTO>();

                if (assignedClassrooms[val.Date][val.PairNumber].ContainsKey(slot.ScheduleSlot.Classroom))
                    throw new Exception($"Аудиторія {slot.ScheduleSlot.Classroom.Name} вже зайнята на {val.Date.ToShortDateString()} {val.PairNumber} парі");

                assignedClassrooms[val.Date][slot.ScheduleSlot.PairNumber][slot.ScheduleSlot.Classroom] = slot.ScheduleSlot;
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

                if (slot.ScheduleSlot.Classroom!= null)
                   assignedClassrooms[slot.ScheduleSlot.Date][slot.ScheduleSlot.PairNumber].Remove(slot.ScheduleSlot.Classroom); 

                // Очищаємо аудиторію
                slot.ScheduleSlot.Classroom = null;
            }
            slot.IsAssigned = false;

                   
        }


        /// <summary>
        /// Оновлення кешу перших слотів для групи.
        /// </summary>
        /// <param name="slot"></param>
        private void ResetUnAssignedFirstSlots(SlotTracker slot)
        {
            if (slot.IsFirstTrackerInSeries)
            {
                unassignedFirstSlots.Remove(slot);
                var firstUnassignedSlot = firstSlotsByGroupSubjects[slot.ScheduleSlot.GroupSubject.Id].FirstOrDefault(x => !x.IsAssigned && x!=slot);
                if (firstUnassignedSlot != null)                
                    unassignedFirstSlots.Add(firstUnassignedSlot);
            }
        }

        /// <summary>
        /// Відновлення кешу перших слотів для групи.
        /// </summary>
        /// <param name="slot"></param>
        private void RestoreUnAssignedFirstSlots(SlotTracker slot)
        {
            if (slot.IsFirstTrackerInSeries)
            {
                var firstUnassignedSlot = firstSlotsByGroupSubjects[slot.ScheduleSlot.GroupSubject.Id].FirstOrDefault(x => !x.IsAssigned && x != slot);
                if (firstUnassignedSlot != null)
                    unassignedFirstSlots.Remove(firstUnassignedSlot);
                unassignedFirstSlots.Add(slot);
            }               
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