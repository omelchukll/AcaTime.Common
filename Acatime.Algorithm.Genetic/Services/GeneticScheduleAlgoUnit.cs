using AcaTime.Algorithm.Genetic.Models;
using AcaTime.Algorithm.Genetic.Services.Calc;
using AcaTime.Algorithm.Genetic.Utils;
using AcaTime.ScheduleCommon.Models.Calc;
using AcaTime.ScheduleCommon.Models.Constraints;
using AcaTime.ScheduleCommon.Utils;
using AcaTime.ScriptModels;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Net.Security;
using System.Runtime.CompilerServices;


namespace AcaTime.Algorithm.Genetic.Services;

/// <summary>
/// Клас для реалізації алгоритму розкладу.
/// </summary>
public class GeneticScheduleAlgoUnit
{
    /// <summary>
    /// Дані для розкладу
    /// </summary>
    public FacultySeasonDTO Root { get; set; }
        
    /// <summary>
    /// Оцінки для розкладу
    /// </summary>
    public UserFunctions UserFunctions { get; set; }

    public AlgorithmParams Parameters { get; internal set; }
    // private string algorithmName = $"alg-{Guid.NewGuid()}";
    internal ILogger logger;
    private CancellationToken cancelToken;

    // public bool ignoreClassrooms { get; private set; }
    public Dictionary<IScheduleSlot, SlotTracker> Slots { get; internal set; }

    // для заміру часу виконання
    // public DebugData DebugData { get; set; } = new DebugData("none");

    // додатковий кеш для прискорення деяких функцій, клонується в Clone
    internal Dictionary<long, List<SlotTracker>> teacherSlots;
    internal Dictionary<long, List<SlotTracker>> groupsSlots;
    internal List<SlotTracker> FirstTrackers;

    // приватний кеш
    private Dictionary<int, List<SlotTracker>> slotsByStep = new Dictionary<int, List<SlotTracker>>(); // для зберігання слотів по крокам
    internal Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>> assignedSlotsByTeacherDate = new Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>>();
    internal Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>> assignedSlotsByGroupDate = new Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>>();
    // private HashSet<SlotTracker> unassignedFirstSlots;
    private Dictionary<long, List<SlotTracker>> firstSlotsByGroupSubjects;
    private Dictionary<DateTime, Dictionary<int, Dictionary<ClassroomDTO, ScheduleSlotDTO>>> assignedClassrooms = new Dictionary<DateTime, Dictionary<int, Dictionary<ClassroomDTO, ScheduleSlotDTO>>>();
    
    
    public AlgorithmResultDTO initialResult = null; // todo можливо переназвати на просто Result...

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

    #region Генетичний алгоритм
        
        private bool isInit;
        private readonly Random _random = new();
        
        private void PreparePrivateGeneticCache()
        {
            firstSlotsByGroupSubjects = FirstTrackers
                .Where(x => x.IsAssigned && x.IsFirstTrackerInSeries) // Оскільки працюємо з вже розподіленими, беремо IsAssigned
                .GroupBy(s => s.ScheduleSlot.GroupSubject.Id)
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SeriesId).ToList());

            // unassignedFirstSlots = firstSlotsByGroupSubjects.Values.Select(x => x.First())
            //     .ToHashSet();
            // можливо треба буде також призначати і
            // assignedSlotsByTeacherDate
            // assignedSlotsByGroupDate
            isInit = true;
        }
        
        // todo Коли розберемось з відновленням до попереднього стану у випадку невдалої мутації,
        // todo створити окремий клас та перенести туди.
        // public async Task<SecondScheduleAlgorithmUnit> RunMutations(CancellationToken token, bool ignoreClassrooms, int prevEstimation)
        // {
        //     cancelToken = token;
        //     this.ignoreClassrooms = ignoreClassrooms;
        //     DebugData = new DebugData(algorithmName, true);
        //
        //     Mutations(prevEstimation);
        //
        //     return this;
        // }

        // todo рідко але іноді буває розклад отриманий після мутацій не зберігається в API, необхідно знайти де могли б втратитись дані.
        // UPD: наче після того як вийшло відновлювати дані такого поки більше не відбувається...
        // todo оскільки вийшло з відновленням даних, можна буде пробувати переносити все до окремого класу
        public int Mutations(int prevEstimation)
        {

            // todo Достатньо буде створити 1 раз, далі скопіюється з інших джерел
            if(!isInit)
                PreparePrivateGeneticCache();

            // Перші заняття в серії - наша популяція з якою ми граємось
            
            // беремо випадковий елемент популяції
            // todo вигадати як переробити щоб не брати кожен раз дисципліну випадково з нуля, а, наприклад, брати зі стеку, та заносити в окремий стек які дисципліни мали успішні та неуспішні мутації, бо цікаво погратись яка комбінація в середньому вигідніша
            var list = FirstTrackers.Select(e => e).Where(e => !e.IsLowDaysDanger).ToList();
            var firstRandomLesson = list.ElementAt(_random.Next(0, list.Count));
            
            // беремо лише той який не стоїть першою можливою датою (тому, що так покращення не дуже очевидне)
            
            // var firstRandomLesson = _population.ElementAt(_random.Next(0,_population.Count));
            
            // відмінити призначення 1 слоту
            // upd не потрібно бо ми робимо ValidateAssignment а потім SetSlotAssignedGenetic
            // firstRandomLesson.IsAssigned = false;
            // SetSlotUnAssigned(firstRandomLesson);
            
            // ResetUnAssignedFirstSlots(firstRandomLesson);
            // SetSlotUnAssigned(firstRandomLesson);
            // і змінюємо його на доступний домен, намагаємось змінити всі інші наступні заняття, перевіряючи констрейнти (is valid)
            var candidateDomain = firstRandomLesson.AvailableDomains;
            
            // збережемо інформацію про всі заняття в цій дисципліні щоб потім відновити назад якщо призначення не відбулось
            var cacheTrackers = firstRandomLesson.ScheduleSlot.GroupSubject.ScheduleSlots
                .Select(slot => Slots[slot])
                .Where(tracker => tracker.SeriesId == firstRandomLesson.SeriesId)
                .OrderBy(tracker => tracker.ScheduleSlot.LessonNumber)
                .ToList();

            List<DomainValue> cacheDomains = new List<DomainValue>();
            foreach (var tracker in cacheTrackers)
            {
                var cachedDomainVal = new DomainValue();
                cachedDomainVal.PairNumber = tracker.ScheduleSlot.PairNumber;
                cachedDomainVal.Date = tracker.ScheduleSlot.Date;
                // var slot = tracker.ScheduleSlot.Clone(firstRandomLesson.ScheduleSlot.GroupSubject);
                cacheDomains.Add(cachedDomainVal);
            }
            
            // cacheTrackers.ForEach(t => t.Clone(t.ScheduleSlot.Clone(firstRandomLesson.ScheduleSlot.GroupSubject)));

            var cacheSlot = firstRandomLesson.ScheduleSlot;

            var cacheDomain = new DomainValue();
            cacheDomain.PairNumber = cacheSlot.PairNumber;
            cacheDomain.Date = cacheSlot.Date;
            
            // var cacheSlotDate = cacheSlot.Date;
            // var cacheSlotLessonNumber = cacheSlot.LessonNumber;
            // var cacheSlotLessonSeriesLength = cacheSlot.LessonSeriesLength;
            // var cacheSlotGroupSubject = cacheSlot.GroupSubject;
            // var lessonShift = firstRandomLesson.WeekShift;
            var aSlots = GetAssignedSlots();
            foreach (var domain in candidateDomain)
            {
                // var currDate = domain.Date;
                // var currPairNum = domain.PairNumber;

                // перевірити чи можемо призначити цей домен
                bool isVld = ValidateAssignment(firstRandomLesson, domain, aSlots);

                if (isVld)
                {
                    // візьмем трекери для інших занять дисципліни, щоб також перепризначити їх
                    // todo подивитись як у розкладі змінюються підгрупи після мутацій, чи всі разом перепризначаються чи окремо
                    var freeTRackers = firstRandomLesson.ScheduleSlot.GroupSubject.ScheduleSlots
                        .Select(slot => Slots[slot])
                        .Where(tracker => !tracker.IsFirstTrackerInSeries && tracker.SeriesId == firstRandomLesson.SeriesId)
                        .OrderBy(tracker => tracker.ScheduleSlot.LessonNumber)
                        .ToList();
                    freeTRackers.ForEach(SetSlotUnAssigned);

                    // перепризначити перший слот
                    SetSlotAssignedGenetic(firstRandomLesson, domain);
                    
                    // перепризначити всі інші
                    var syncCheck = ApplySynchronizedDomainPatternGenetic(firstRandomLesson, aSlots);
                    if (syncCheck)
                    {
                        bool fwdcheck = ForwardCheck(firstRandomLesson,firstRandomLesson.AssignStep);
                        // якщо мутація краща, зберігаємо результат
                        var res = Estimate();
                        if (fwdcheck && res > prevEstimation)
                        {
                            freeTRackers.ForEach(e => Slots[e.ScheduleSlot] = e);
                            
                            // todo перевірити, чи ми змінюємо лише одну підгрупу чи всі (наче всі, але варто ще раз подивитись)
                            
                            // залогуємо наші зміни щоб було легше шукати в excel таблиці різницю з дефолт алгоритмом
                            logger.LogInformation($"БУЛО: ВИКЛАДАЧ:{firstRandomLesson.ScheduleSlot.GroupSubject.Teacher.Name}|ДАТА:{cacheDomain.Date}|НОМЕР:{cacheDomain.PairNumber} СТАЛО:ДАТА:{firstRandomLesson.ScheduleSlot.Date}|НОМЕР:{firstRandomLesson.ScheduleSlot.PairNumber} ");
                            
                            // якщо вже стало краще - варто зберегти зміни, ніж далі шукати інші варіанти
                            return res;
                            
                            // todo а що якщо ми отримуємо певну кращу оцінку після мутації
                            // ми будемо перевіряти і інші варіанти бо раптом саме для цієї серії є ще щось краще?
                            // тоді можна буде перебрати всі варіанти, і вибрати найкращий з усіх
                        }
                    }
                }
            }
            
            // зараз нам нічого не треба відновлювати
            // але в майбутньому треба буде перепризначати все назад на тих самих даних
            // не залишаючи слідів у випадку якщо призначення було не вигідне / не успішне
            // щоб не відновлювати все копіюванням
            // return Estimate();
            // UPD: зроблено! І навіть наче працює :)
            // поки закоментуємо
            // var before = Estimate();
            
            // todo доробити повернення до попереднього стану
            // UPD: зроблено! І навіть наче працює :)
            
            var trackerToRestore = firstRandomLesson.ScheduleSlot.GroupSubject.ScheduleSlots
                .Select(slot => Slots[slot])
                .Where(tracker => tracker.SeriesId == firstRandomLesson.SeriesId)
                .OrderBy(tracker => tracker.ScheduleSlot.LessonNumber)
                .ToList();

            for (int i = 0; i < trackerToRestore.Count; i++)
            {
                SetSlotAssignedGenetic(trackerToRestore[i], cacheDomains[i]);
            }
            // var after = Estimate();
            // logger.LogInformation($"BEFORE: {before} AFTER {after}");

            return Estimate();

            SetSlotAssignedGenetic(firstRandomLesson, cacheDomain);
            // firstRandomLesson.IsAssigned = true;
            
            var freeTR = firstRandomLesson.ScheduleSlot.GroupSubject.ScheduleSlots
                .Select(slot => Slots[slot])
                .Where(tracker => !tracker.IsFirstTrackerInSeries && tracker.SeriesId == firstRandomLesson.SeriesId)
                .OrderBy(tracker => tracker.ScheduleSlot.LessonNumber)
                .ToList();
            
            freeTR.ForEach(SetSlotUnAssigned);
            freeTR.ForEach(e => Slots[e.ScheduleSlot] = e);

            ApplySynchronizedDomainPatternGenetic(firstRandomLesson, aSlots);
            ForwardCheck(firstRandomLesson,firstRandomLesson.AssignStep);
            return Estimate();

        }
        
        private bool ApplySynchronizedDomainPatternGenetic(SlotTracker currentTracker, AssignedSlotsDTO assignedSLots)
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
                    SetSlotAssignedGenetic(nextTracker, nextDate);
                    freeTRackers.Remove(nextTracker);
                }

                nextDate.Date = nextDate.Date.AddDays(currentTracker.WeekShift * 7);
            }

            var res = !freeTRackers.Any(tracker => !tracker.IsAssigned);
            return res;
        }

        // todo
        public int Swap()
        {
            PreparePrivateGeneticCache();
            

            return Estimate();
        }

        public int Estimate()
        {
            int scheduleEstimation = 0;
            foreach (var s in UserFunctions.ScheduleEstimations)
            {
                var extScore = s.Estimate(Root);
                scheduleEstimation += extScore;
            }
            // logger.LogInformation($"Estimate: {scheduleEstimation}");
            return scheduleEstimation;
            
        }
        
                // різниця полягає в тому що нам не потрібно зберігати крок.
        private void SetSlotAssignedGenetic(SlotTracker slot, DomainValue val)
        {
            Slots[slot.ScheduleSlot] = slot; // let's try this?
            slot.SetDomain(val);
            slot.IsAssigned = true;

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

        #endregion
        
        
        #region Необхідні нам дефолтні методи
        
        
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

                if (slot.ScheduleSlot.Classroom != null)
                    assignedClassrooms[slot.ScheduleSlot.Date][slot.ScheduleSlot.PairNumber].Remove(slot.ScheduleSlot.Classroom);

                // Очищаємо аудиторію
                slot.ScheduleSlot.Classroom = null;
            }
            slot.IsAssigned = false;

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

            // // Перевірка на аудиторії, якщо алгоритм має враховувати їх
            // if (!ignoreClassrooms && !slotTracker.ScheduleSlot.GroupSubject.Subject.NoClassroom)
            // {
            //     var classroomValidation = ValidateAndSelectClassroom(slotTracker.ScheduleSlot, domain, assignedSLots);
            //     if (!classroomValidation)
            //         return false;
            // }

            foreach (var validator in UserFunctions.SlotValidators)
            {
                var userValidation = validator.Validate(slotAdapter, assignedSLots);
                if (!userValidation)
                    return false;
            }

            return true;
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