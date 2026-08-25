using System.Collections.Immutable;
using AcaTime.Algorithm.Genetic.Services.Calc;
using AcaTime.Algorithm.Genetic.Utils;
using AcaTime.ScheduleCommon.Models.Calc;
using AcaTime.ScheduleCommon.Models.Constraints;
using AcaTime.ScriptModels;
using Microsoft.Extensions.Logging;

namespace AcaTime.Algorithm.Genetic.Models.Genetic;

public class Individual
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

    public Dictionary<IScheduleSlot, SlotTracker> Slots { get; internal set; }

    // додатковий кеш для прискорення деяких функцій, клонується в Clone
    internal Dictionary<long, List<SlotTracker>> teacherSlots;
    internal Dictionary<long, List<SlotTracker>> groupsSlots;
    internal List<SlotTracker> FirstTrackers;

    // приватний кеш
    // private Dictionary<int, List<SlotTracker>> slotsByStep = new Dictionary<int, List<SlotTracker>>(); // для зберігання слотів по крокам
    internal Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>> assignedSlotsByTeacherDate = new Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>>();
    internal Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>> assignedSlotsByGroupDate = new Dictionary<long, Dictionary<DateTime, HashSet<SlotTracker>>>();
    // private HashSet<SlotTracker> unassignedFirstSlots;
    private Dictionary<long, List<SlotTracker>> firstSlotsByGroupSubjects;
    private Dictionary<DateTime, Dictionary<int, Dictionary<ClassroomDTO, ScheduleSlotDTO>>> assignedClassrooms = new Dictionary<DateTime, Dictionary<int, Dictionary<ClassroomDTO, ScheduleSlotDTO>>>();
    
    public AlgorithmResultDTO initialResult = null; // todo можливо переназвати на просто Result...

    public void Setup(FacultySeasonDTO root, ILogger logger, UserFunctions userFunctions, AlgorithmParams parameters)
    {
        Root = root;
        this.logger = logger;
        UserFunctions = userFunctions;
        Parameters = parameters;
    }
    
    
    private bool isInit;
    private readonly Random _random = new();
    private void PreparePrivateGeneticCache()
    {
        firstSlotsByGroupSubjects = FirstTrackers
            .Where(x => x.IsAssigned && x.IsFirstTrackerInSeries) // Оскільки працюємо з вже розподіленими, беремо IsAssigned
            .GroupBy(s => s.ScheduleSlot.GroupSubject.Id)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SeriesId).ToList());

        isInit = true;
    }

    public int Estimate()
    {
        int scheduleEstimation = 0;
        foreach (var s in UserFunctions.ScheduleEstimations)
        {
            var extScore = s.Estimate(Root);
            scheduleEstimation += extScore;
        }
        return scheduleEstimation;
            
    }

    #region Основні операції

    List<int?> swappedSeries = new List<int?>();
    
    private Dictionary<SlotTracker, int> usedTrackers = new();
    
    public KeyValuePair<int, DomainValue>? Mutations(int prevEstimation, int minSeriesLength, int maxSeriesLength, HashSet<int> usedSeries)
    {

        // todo Достатньо буде створити 1 раз, далі скопіюється з інших джерел
        if(!isInit)
            PreparePrivateGeneticCache();
            
            // Перші заняття в серії - наша популяція з якою ми граємось
            
            // беремо випадковий елемент популяції
            // todo вигадати як переробити щоб не брати кожен раз дисципліну випадково з нуля, а, наприклад, брати зі стеку, та заносити в окремий стек які дисципліни мали успішні та неуспішні мутації, бо цікаво погратись яка комбінація в середньому вигідніша
            
            var hasMinSeriesLimit = minSeriesLength != -1;
            var hasMaxSeriesLimit = maxSeriesLength != -1;
            List<SlotTracker>? list = null;
            if (hasMinSeriesLimit && hasMaxSeriesLimit)
            {
                list = FirstTrackers
                    .Select(e => e)
                    .Where(e => 
                        !usedSeries.Contains((int)e.SeriesId) &&
                            // !usedTrackers.ContainsKey(e) || usedTrackers[e] < 3) && 
                        !e.IsLowDaysDanger && 
                        e.ScheduleSlot.LessonSeriesLength <= maxSeriesLength && 
                        e.ScheduleSlot.LessonSeriesLength >= minSeriesLength)
                    .ToList();
            }
            else if (hasMinSeriesLimit)
            {
                list = FirstTrackers
                    .Select(e => e)
                    .Where(e => 
                        !usedSeries.Contains((int)e.SeriesId) &&
                        // (!usedTrackers.ContainsKey(e) || usedTrackers[e] < 3) && 
                        !e.IsLowDaysDanger && 
                        e.ScheduleSlot.LessonSeriesLength >= minSeriesLength)
                    .ToList();
            }
            else
            {
                list = FirstTrackers
                    .Select(e => e)
                    .Where(e =>
                        !usedSeries.Contains((int)e.SeriesId) &&
                        // (!usedTrackers.ContainsKey(e) || usedTrackers[e] < 3) &&
                        !e.IsLowDaysDanger &&
                        e.ScheduleSlot.LessonSeriesLength <= maxSeriesLength)
                    .ToList();
            }

            if (list.Count == 0)
            {
                usedTrackers.Clear();
                return null;
            }
            // var list = FirstTrackers.Select(e => e).Where(e => (!usedTrackers.ContainsKey(e) || usedTrackers[e] < 3) && e is { IsLowDaysDanger: false, ScheduleSlot.LessonSeriesLength: < 4 }).ToList();
            var firstRandomLesson = list.ElementAt(_random.Next(0, list.Count));
            
            // if (!usedTrackers.TryAdd(firstRandomLesson, 1))
            // {
            //     usedTrackers[firstRandomLesson]++;
            // }

            // і змінюємо його на доступний домен, намагаємось змінити всі інші наступні заняття, перевіряючи констрейнти (is valid)
            var candidateDomain = firstRandomLesson.AvailableDomains;
            
            // збережемо інформацію про всі заняття в цій дисципліні щоб потім відновити назад якщо призначення не відбулось
            var cacheTrackers = firstRandomLesson.ScheduleSlot.GroupSubject.ScheduleSlots
                .Select(slot => Slots[slot])
                .Where(tracker => tracker.SeriesId == firstRandomLesson.SeriesId)
                .OrderBy(tracker => tracker.ScheduleSlot.LessonNumber)
                .ToList();

            var cacheDomains = new List<DomainValue>();
            foreach (var tracker in cacheTrackers)
            {
                var cachedDomainVal = new DomainValue();
                cachedDomainVal.PairNumber = tracker.ScheduleSlot.PairNumber;
                cachedDomainVal.Date = tracker.ScheduleSlot.Date;
                // var slot = tracker.ScheduleSlot.Clone(firstRandomLesson.ScheduleSlot.GroupSubject);
                cacheDomains.Add(cachedDomainVal);
            }
            
            var cacheSlot = firstRandomLesson.ScheduleSlot;

            var cacheDomain = new DomainValue();
            cacheDomain.PairNumber = cacheSlot.PairNumber;
            cacheDomain.Date = cacheSlot.Date;
            
            var aSlots = GetAssignedSlots();
            foreach (var domain in candidateDomain)
            {
                // перевірити чи можемо призначити цей домен
                var isVld = ValidateAssignment(firstRandomLesson, domain, aSlots);

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
                            
                            currentEstimation = res;
                            if (firstRandomLesson.SeriesId != null)
                            {
                                seriesNewDomain = new KeyValuePair<int, DomainValue>((int)firstRandomLesson.SeriesId, domain);
                            }
                            return seriesNewDomain;
                        }
                    }
                }
            }
            // var trackerToRestore = firstRandomLesson.ScheduleSlot.GroupSubject.ScheduleSlots
            //     .Select(slot => Slots[slot])
            //     .Where(tracker => tracker.SeriesId == firstRandomLesson.SeriesId)
            //     .OrderBy(tracker => tracker.ScheduleSlot.LessonNumber)
            //     .ToList();
            //
            // for (int i = 0; i < trackerToRestore.Count; i++)
            // {
            //     SetSlotAssignedGenetic(trackerToRestore[i], cacheDomains[i]);
            // }
            if (firstRandomLesson.SeriesId != null)
            {
                return new KeyValuePair<int, DomainValue>((int)firstRandomLesson.SeriesId, null);
            }

            return null;
    }

    private KeyValuePair<int, DomainValue> seriesNewDomain = new KeyValuePair<int, DomainValue>();

    public void ApplyMutation(KeyValuePair<int, DomainValue> pair)
    {
        var tracker = FirstTrackers
            .Select(e => e)
            .First(e => e.SeriesId == pair.Key);
        var aSlots = GetAssignedSlots();
        
        var cacheTrackers = tracker.ScheduleSlot.GroupSubject.ScheduleSlots
            .Select(slot => Slots[slot])
            .Where(t => t.SeriesId == tracker.SeriesId)
            .OrderBy(t => tracker.ScheduleSlot.LessonNumber)
            .ToList();

        var cacheDomains = new List<DomainValue>();
        foreach (var t in cacheTrackers)
        {
                var cachedDomainVal = new DomainValue();
                cachedDomainVal.PairNumber = t.ScheduleSlot.PairNumber;
                cachedDomainVal.Date = t.ScheduleSlot.Date;
                cacheDomains.Add(cachedDomainVal);
        }
        
        var cacheSlot = tracker.ScheduleSlot;
        var cacheDomain = new DomainValue();
        cacheDomain.PairNumber = cacheSlot.PairNumber;
        cacheDomain.Date = cacheSlot.Date;

        var candidateDomain = tracker.AvailableDomains;

        foreach (var domain in candidateDomain)
        {
            bool isVld = ValidateAssignment(tracker, domain, aSlots);

            if (isVld)
            {
                // візьмем трекери для інших занять дисципліни, щоб також перепризначити їх
                var freeTRackers = tracker.ScheduleSlot.GroupSubject.ScheduleSlots
                    .Select(slot => Slots[slot])
                    .Where(t => !t.IsFirstTrackerInSeries && t.SeriesId == tracker.SeriesId)
                    .OrderBy(t => t.ScheduleSlot.LessonNumber)
                    .ToList();
                freeTRackers.ForEach(SetSlotUnAssigned);

                // перепризначити перший слот
                SetSlotAssignedGenetic(tracker, domain);
                    
                // перепризначити всі інші
                var syncCheck = ApplySynchronizedDomainPatternGenetic(tracker, aSlots);
                if (syncCheck)
                {
                    bool fwdcheck = ForwardCheck(tracker,tracker.AssignStep);
                    // якщо мутація краща, зберігаємо результат
                    var res = Estimate();
                    if (fwdcheck && res > currentEstimation)
                    {
                        freeTRackers.ForEach(e => Slots[e.ScheduleSlot] = e);
                            
                        // залогуємо наші зміни щоб було легше шукати в excel таблиці різницю з дефолт алгоритмом
                        logger.LogInformation($"ПЕРЕНОС ДАВ РЕЗУЛЬТАТ З {currentEstimation} НА {res}");
                        logger.LogInformation($"БУЛО: ВИКЛАДАЧ:{tracker.ScheduleSlot.GroupSubject.Teacher.Name}|ДАТА:{cacheDomain.Date}|НОМЕР:{cacheDomain.PairNumber} СТАЛО:ДАТА:{tracker.ScheduleSlot.Date}|НОМЕР:{tracker.ScheduleSlot.PairNumber} ");

                        this.currentEstimation = res;
                        // if (tracker.SeriesId != null)
                        // {
                        //     seriesNewDomain = new KeyValuePair<int, DomainValue>((int)firstRandomLesson.SeriesId, domain);
                        // }
                        return;
                    }
                }
            }
        }
            
        // var isVld = ValidateAssignment(tracker, pair.Value, aSlots);
        // if (isVld)
        // {
        //     // візьмем трекери для інших занять дисципліни, щоб також перепризначити їх
        //     var freeTRackers = tracker.ScheduleSlot.GroupSubject.ScheduleSlots
        //         .Select(slot => Slots[slot])
        //         .Where(t =>  t.SeriesId == tracker.SeriesId)
        //         .OrderBy(t => t.ScheduleSlot.LessonNumber)
        //         .ToList();
        //     freeTRackers.ForEach(SetSlotUnAssigned);
        //
        //     // перепризначити перший слот
        //     SetSlotAssignedGenetic(tracker, pair.Value);
        //     var syncCheck = ApplySynchronizedDomainPatternGenetic(tracker, aSlots);
        //     if (syncCheck)
        //     {
        //         bool fwdcheck = ForwardCheck(tracker, tracker.AssignStep);
        //         // якщо мутація краща, зберігаємо результат
        //         var res = Estimate();
        //         if (fwdcheck && res > currentEstimation)
        //         {
        //             freeTRackers.ForEach(e => Slots[e.ScheduleSlot] = e);
        //
        //             logger.LogInformation($"ВСЕ ВИЙШЛО!");
        //
        //             // залогуємо наші зміни щоб було легше шукати в excel таблиці різницю з дефолт алгоритмом
        //             logger.LogInformation(
        //                 $"БУЛО: ВИКЛАДАЧ:{tracker.ScheduleSlot.GroupSubject.Teacher.Name}|ДАТА:{pair.Value.Date}|НОМЕР:{pair.Value.PairNumber} СТАЛО:ДАТА:{tracker.ScheduleSlot.Date}|НОМЕР:{tracker.ScheduleSlot.PairNumber} ");
        //
        //             currentEstimation = res;
        //             return;
        //         }
        //     }
        // }
        var trackerToRestore = tracker.ScheduleSlot.GroupSubject.ScheduleSlots
            .Select(slot => Slots[slot])
            .Where(t => t.SeriesId == tracker.SeriesId)
            .OrderBy(t => t.ScheduleSlot.LessonNumber)
            .ToList();

        for (int i = 0; i < trackerToRestore.Count; i++)
        {
            SetSlotAssignedGenetic(trackerToRestore[i], cacheDomains[i]);
        }
    }

    public KeyValuePair<int, int>? SwapTeacherSubjects()
    {
        if (!isInit)
            PreparePrivateGeneticCache();
        // беремо предмети які можна змінювати
        // var list = FirstTrackers.Select(e => e).Where(e => !e.IsLowDaysDanger && e.ScheduleSlot.GroupSubject.Groups.Count > 1).ToList();
        var list = FirstTrackers.Select(e => e).Where(e => !e.IsLowDaysDanger && e.SeriesLength > 3 && !swappedSeries.Contains(e.SeriesId)).ToList();
            
        // цикл, оскільки вірогідність з 1го разу натрапити на такий предмет що має ідентичний у того ж викладача не висока
        int stop = 100;
        while (true)
        {
            if(stop == 0)
                break;
            stop--;
            // перестворимо swappedSeries якщо list пустий
            if (list.Count == 0)
            {
                swappedSeries = new List<int?>();
                break;
            }
            var firstRandomLessonTracker = list.ElementAt(_random.Next(0, list.Count));
            // якщо нещодавно вже намагались замінити цей предмет, спробуємо інший
            if (swappedSeries.Contains(firstRandomLessonTracker.SeriesId))
            {
                continue;
            }
            swappedSeries.Add(firstRandomLessonTracker.SeriesId);
            var teacher = firstRandomLessonTracker.ScheduleSlot.GroupSubject.Teacher.Id;
                
            var teacherSubjectsList = assignedSlotsByTeacherDate[teacher].Values.ToList();
            if(teacherSubjectsList.Count < 2)
                continue;

            // всі предмети викладача
            var teacherSubjectsTrackers = FirstTrackers.Select(e => e).Where(e => !e.IsLowDaysDanger && e.ScheduleSlot.GroupSubject.Teacher.Id == firstRandomLessonTracker.ScheduleSlot.GroupSubject.Teacher.Id).ToList();
            
            var dicLen = new Dictionary<int, int>();
                
            if(teacherSubjectsTrackers.Count < 2)
                continue;

            for (var i = 0; i < teacherSubjectsTrackers.Count(); i++)
            {
                int l = teacherSubjectsTrackers[i].ScheduleSlot.LessonSeriesLength;
                if (!dicLen.ContainsKey(l))
                {
                    dicLen.Add(l, i);
                }
                else
                {
                    // тобто ці 2 предмети мають однакову довжину і кількість можливих доменів для першого слоту (а відповідно і для всіх інших теж)
                    if (teacherSubjectsTrackers[i].AvailableDomains.Count ==
                        teacherSubjectsTrackers[dicLen[l]].AvailableDomains.Count)
                    {
                        // тоді пробуємо замінити їх місцями
                        var firstSubjectFirstTracker = teacherSubjectsTrackers[i];
                        var secondSubjectFirstTracker = teacherSubjectsTrackers[dicLen[l]];
                        // var before = Estimate();
                        var before = currentEstimation;
                        // замінити місцями.
                        SwapSeriesTrackers(firstSubjectFirstTracker, secondSubjectFirstTracker);
                        // SwapSeriesTrackers(firstSubjectFirstTracker, secondSubjectFirstTracker);
                        var after = Estimate();
                        
                        // якщо ця зміна є негативною, повернути назад
                        if (after < before)
                        {
                            // не потрібно повертати, популяція просто потім видалиться
                            currentEstimation = after;
                            // SwapSeriesTrackers(firstSubjectFirstTracker, secondSubjectFirstTracker);
                            // якщо стає гірше не повертаємо нічого,
                            // нову зберігаємо лише коли вона не гірше за поточну
                            return null;
                        }
                        // зберігаємо оцінку нової популяції
                        currentEstimation = after;
                        
                        logger.LogInformation($"ЗРОБИЛИ КРАЩЕ!: {before} : {after} ");
                        if(firstSubjectFirstTracker.SeriesId != null && secondSubjectFirstTracker.SeriesId != null)
                            return new KeyValuePair<int, int>((int)firstSubjectFirstTracker.SeriesId, (int)secondSubjectFirstTracker.SeriesId);
                    }
                }
            }
        }
        return null;
    }
    
    public KeyValuePair<int, int>? SwapGroupSubjects()
    {
            // беремо предмети які можна змінювати
            // var list = FirstTrackers.Select(e => e).Where(e => !e.IsLowDaysDanger && e.ScheduleSlot.GroupSubject.Groups.Count > 1).ToList();
            var list = FirstTrackers.Select(e => e).Where(e => !e.IsLowDaysDanger && e.SeriesLength > 3 && !swappedSeries.Contains(e.SeriesId)).ToList();
            
            // цикл, оскільки вірогідність з 1го разу натрапити на такий предмет що має ідентичні групи не висока
            int stop = 5;
            while (true)
            {
                if(stop == 0)
                    break;
                stop--;
                // перестворимо swappedSeries якщо list пустий
                if (list.Count == 0)
                {
                    swappedSeries = new List<int?>();
                    break;
                }
                var firstRandomLessonTracker = list.ElementAt(_random.Next(0, list.Count));
                // якщо нещодавно вже намагались замінити цей предмет, спробуємо інший
                if (swappedSeries.Contains(firstRandomLessonTracker.SeriesId))
                {
                    continue;
                }
                swappedSeries.Add(firstRandomLessonTracker.SeriesId);

                // ми можемо замінити лише якщо ті самі групи
                // додаємо у список всі групи що беруть участь в цій дисципліні
                var groups = firstRandomLessonTracker.ScheduleSlot.GroupSubject.Groups;
                
                // дивимось по першій групі, чи є ще дисципліна в якій задіяні лише групи з цього списку
                var slots = groupsSlots[groups[0].Id]
                    .Select(s => s)
                    .Where(s => s.IsFirstTrackerInSeries)
                    .ToList();
                
                var potentialSubjectToSwap = new List<SlotTracker>();

                foreach (var slot in slots)
                {
                    // пропускаємо ту серію що змінюємо та слоти які не є першими у серії
                    if(slot.SeriesId == firstRandomLessonTracker.SeriesId || !slot.IsFirstTrackerInSeries)
                        continue;
                    
                    // якщо у якоїсь з груп немає такої дисципліни, то вона нам не підходить, дивимось інші дисципліни
                    var subjectGroups = slot.ScheduleSlot.GroupSubject.Groups;
                    var isPresent = true;
                    // дивимось як ті що є в даній дисципліні яку ми змінюємо
                    foreach (var group in subjectGroups)
                    {
                        if (!groups.Select(g => g.Id).Contains(group.Id))
                        {
                            isPresent = false;
                            break;
                        }
                    }
                    if(!isPresent)
                        continue;
                    // дивимось і ті що є в іншій дисципліні з якою ми змінюємо
                    foreach (var group in groups)
                    {
                        if (!subjectGroups.Select(g => g.Id).Contains(group.Id))
                        {
                            isPresent = false;
                            break;
                        }
                    }
                    if(!isPresent)
                        continue;
                    potentialSubjectToSwap.Add(slot);
                }
                
                // якщо немає дисциплін для свапу з цією, шукаємо для іншої дисципліни
                if (potentialSubjectToSwap.Count == 0)
                    continue;
                
                // пробуваємо перепризначити місцями 2 дисципліни

                var before = currentEstimation;
                var aSlots = GetAssignedSlots();
                
                foreach (var subjectTracker in potentialSubjectToSwap)
                {
                    var DomainFirst = new DomainValue()
                    {
                        Date = firstRandomLessonTracker.ScheduleSlot.Date,
                        PairNumber = firstRandomLessonTracker.ScheduleSlot.PairNumber,
                    };
                    var DomainSecond = new DomainValue()
                    {
                        Date = subjectTracker.ScheduleSlot.Date,
                        PairNumber = subjectTracker.ScheduleSlot.PairNumber,
                    };
                    
                    var cacheDomainsFirst = new List<DomainValue>();
                    var cacheDomainsSecond = new List<DomainValue>();
                    
                    if(!Slots.ContainsKey(firstRandomLessonTracker.ScheduleSlot))
                        continue;
                    
                    var freeTRackersFirst = firstRandomLessonTracker.ScheduleSlot.GroupSubject.ScheduleSlots
                        .Select(slot => Slots[slot])
                        .Where(tracker => tracker.SeriesId == firstRandomLessonTracker.SeriesId)
                        .OrderBy(tracker => tracker.ScheduleSlot.LessonNumber)
                        .ToList();
                    
                    var freeTRackersSecond = subjectTracker.ScheduleSlot.GroupSubject.ScheduleSlots
                        .Select(slot => Slots[slot])
                        .Where(tracker => tracker.SeriesId == subjectTracker.SeriesId)
                        .OrderBy(tracker => tracker.ScheduleSlot.LessonNumber)
                        .ToList();
                    
                    foreach (var tracker in freeTRackersFirst)
                    {
                        var cachedDomainVal = new DomainValue();
                        cachedDomainVal.PairNumber = tracker.ScheduleSlot.PairNumber;
                        cachedDomainVal.Date = tracker.ScheduleSlot.Date;
                        cacheDomainsFirst.Add(cachedDomainVal);
                    }
                    foreach (var tracker in freeTRackersSecond)
                    {
                        var cachedDomainVal = new DomainValue();
                        cachedDomainVal.PairNumber = tracker.ScheduleSlot.PairNumber;
                        cachedDomainVal.Date = tracker.ScheduleSlot.Date;
                        cacheDomainsSecond.Add(cachedDomainVal);
                    }
                    
                    freeTRackersFirst.ForEach(SetSlotUnAssigned);
                    SetSlotAssignedGenetic(firstRandomLessonTracker, DomainSecond);
                    
                    freeTRackersSecond.ForEach(SetSlotUnAssigned);
                    SetSlotAssignedGenetic(subjectTracker, DomainFirst);
                    
                    var syncCheck = ApplySynchronizedDomainPatternGenetic(firstRandomLessonTracker, aSlots);
                    syncCheck = syncCheck && ApplySynchronizedDomainPatternGenetic(subjectTracker, aSlots);

                    if (syncCheck)
                    {
                        var res = Estimate();
                        // якщо стає не гірше - зберігаємо
                        // якщо замінити не строге >= на строге >=, шукає набагато довше
                        // тобто простіше знаходити ідентичні розклади, можна тоді зберігати 2 варіанти замість 1го
                        // і використовувати 2 популяції, ніби ми їх так множимо.
                        if (res > before)
                        {
                            freeTRackersFirst.ForEach(e => Slots[e.ScheduleSlot] = e);
                            freeTRackersSecond.ForEach(e => Slots[e.ScheduleSlot] = e);
                            
                            currentEstimation = res;
                            logger.LogInformation($"ЗРОБИЛИ КРАЩЕ СВАПОМ ГРУП");

                            if(firstRandomLessonTracker.SeriesId != null && subjectTracker.SeriesId != null)
                                return new KeyValuePair<int, int>((int)firstRandomLessonTracker.SeriesId, (int)subjectTracker.SeriesId);
                            return null;
                        }
                        currentEstimation = res;
                        // if (res == before)
                        // {
                        //     logger.LogInformation($"СВАП ДАВ АЛЬТЕРНАТИВНИЙ ВАРІАНТ З ТАКОЮ Ж ОЦІНКОЮ! ");
                        //     freeTRackersFirst.ForEach(e => Slots[e.ScheduleSlot] = e);
                        //     freeTRackersSecond.ForEach(e => Slots[e.ScheduleSlot] = e);
                        //     if(firstRandomLessonTracker.SeriesId != null && subjectTracker.SeriesId != null)
                        //         return new KeyValuePair<int, int>((int)firstRandomLessonTracker.SeriesId, (int)subjectTracker.SeriesId);
                        //     return null;
                        // }
                    }
                        // повертаємо назад
                        // SetSlotAssignedGenetic(firstRandomLessonTracker, DomainFirst);
                        // SetSlotAssignedGenetic(subjectTracker, DomainSecond);
                        //
                        // for (int i = 0; i < freeTRackersFirst.Count; i++)
                        // {
                        //     SetSlotAssignedGenetic(freeTRackersFirst[i], cacheDomainsFirst[i]);
                        // }
                        // for (int i = 0; i < freeTRackersSecond.Count; i++)
                        // {
                        //     SetSlotAssignedGenetic(freeTRackersSecond[i], cacheDomainsSecond[i]);
                        // }
                        // currentEstimation = Estimate();
                }

            }
            return null;
    }

    public void ApplyTeacherSubjectsSwap(KeyValuePair<int, int> subjectsForSwap)
    {
        var firstSubject = FirstTrackers
            .Select(e => e)
            .First(e => e.SeriesId == subjectsForSwap.Key);
        var secondSubject = FirstTrackers
            .Select(e => e)
            .First(e => e.SeriesId == subjectsForSwap.Value);
        var before = currentEstimation;
        SwapSeriesTrackers(firstSubject, secondSubject);
        var after = Estimate();

        if (after <= before)
        {
            SwapSeriesTrackers(firstSubject, secondSubject);
        }
        else
            currentEstimation = after;
    } 

    #endregion

    public Individual SetSlotAssignedGeneticClone(SlotTracker slot, DomainValue val)
    {
        var population = this.clone();
        population.SetSlotAssignedGenetic(slot, val);
        return population;
    }
    
    // різниця полягає в тому що нам не потрібно зберігати крок.
    public void SetSlotAssignedGenetic(SlotTracker slot, DomainValue val)
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

    internal int currentEstimation = Int32.MinValue;
    
    public Individual SwapSeriesTrackersClone(SlotTracker first, SlotTracker second)
    {
        var population = this.clone();
        population.SwapSeriesTrackers(first, second);
        
        return population;
    }
    
    // Міняє серії місцями, при цьому залишаючи лише кращий.
    // Виконується лише якщо кількість занять є однаковою.
    private void SwapSeriesTrackers(SlotTracker first, SlotTracker second)
    {
        var freeTrackersFirst = first.ScheduleSlot.GroupSubject.ScheduleSlots
            .Select(slot => Slots[slot])
            .Where(tracker => tracker.SeriesId == first.SeriesId)
            .OrderBy(tracker => tracker.ScheduleSlot.LessonNumber)
            .ToList();
        var freeTrackersSecond = second.ScheduleSlot.GroupSubject.ScheduleSlots
            .Select(slot => Slots[slot])
            .Where(tracker => tracker.SeriesId == second.SeriesId)
            .OrderBy(tracker => tracker.ScheduleSlot.LessonNumber)
            .ToList();
            
        // Якщо кількість занять не однакова, неможливо просто замінити серії місцями
        // todo подумати, можливо
        // просто необхідно доповнити цей метод для більшого числа занять,
        // при цьому використовуючи не SetSlotAssignedGenetic а ApplySynchronizedDomainPatternGenetic
        if(freeTrackersFirst.Count != freeTrackersSecond.Count)
            return;

        for (int j = 0; j < freeTrackersFirst.Count; j++)
        {
            var domainFirst = new DomainValue()
            {
                Date = freeTrackersFirst[j].ScheduleSlot.Date,
                PairNumber = freeTrackersFirst[j].ScheduleSlot.PairNumber,
            };
            var domainSecond = new DomainValue()
            {
                Date = freeTrackersSecond[j].ScheduleSlot.Date,
                PairNumber = freeTrackersSecond[j].ScheduleSlot.PairNumber,
            };    
            SetSlotAssignedGenetic(freeTrackersFirst[j], domainSecond);
            SetSlotAssignedGenetic(freeTrackersSecond[j], domainFirst);
        }
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
                // slotsByStep[currentStep].Add(slot);
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