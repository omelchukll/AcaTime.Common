using AcaTime.Algorithm.Genetic.Models;
using AcaTime.Algorithm.Genetic.Services.Calc;
using AcaTime.Algorithm.Genetic.Utils;
using AcaTime.ScheduleCommon.Models.Calc;
using AcaTime.ScheduleCommon.Models.Constraints;
using AcaTime.ScheduleCommon.Utils;
using AcaTime.ScriptModels;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Design.Serialization;
using System.Net.Security;
using System.Runtime.CompilerServices;
using AcaTime.Algorithm.Genetic.Models.Genetic;


namespace AcaTime.Algorithm.Genetic.Services;

/// <summary>
/// Клас для реалізації алгоритму розкладу.
/// </summary>
public class GeneticScheduleAlgorithmUnit
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

        private int strategyChangeIterationCounter = 0;
        private int strategyChangeResultCounter = 0;
        private int strategy = 0;

        private void SelectStrategy(int resultShift)
        {
            strategyChangeIterationCounter++;
            strategyChangeResultCounter += resultShift;

            if(strategyChangeIterationCounter < 3)
                return;
            if (strategyChangeResultCounter > 1000)
            {
                strategyChangeIterationCounter = 0;
                strategyChangeResultCounter = 0;
                return;
            }

            if (strategy >= 3)
                strategy = 0;
            else
                strategy++;
            strategyChangeIterationCounter = 0;
            strategyChangeResultCounter = 0;
        }

        private void GeneticAlgorithm(int iteration, Individual individual)
        {
            int mainStrategy = _random.Next(0,10);
            int subStrategy = _random.Next(0,10);

            if (mainStrategy > 8)
            {
                if (subStrategy > 7)
                {
                    // logger.LogInformation($"ВИКОНУЄМО ОПЕРАЦІЮ ДЛЯ ПОПУЛЯЦІЇ {iteration} СТРАТЕГІЯ PopulationSwapGroupSubjects");
                    PopulationSwapGroupSubjects(individual);
                }
                else
                {
                    PopulationSwapTeacherSubjects(individual);
                    // logger.LogInformation($"ВИКОНУЄМО ОПЕРАЦІЮ ДЛЯ ПОПУЛЯЦІЇ {iteration} СТРАТЕГІЯ PopulationSwapTeacherSubjects");
                }
            }
            else
            {
                if (subStrategy > 7)
                {
                    PopulationMutationsForShortSeries(individual);
                    // logger.LogInformation($"ВИКОНУЄМО ОПЕРАЦІЮ ДЛЯ ПОПУЛЯЦІЇ {iteration} СТРАТЕГІЯ PopulationMutationsForShortSeries");
                }
                else
                {
                    PopulationMutationsForLongSeries(individual);
                    // logger.LogInformation($"ВИКОНУЄМО ОПЕРАЦІЮ ДЛЯ ПОПУЛЯЦІЇ {iteration} СТРАТЕГІЯ PopulationMutationsForLongSeries");
                }
            }
        }

        public async Task<AlgorithmResultDTO> Run(CancellationToken token, bool ignoreClassrooms)
        {
            PreparePrivateGeneticCache();
            
            // int baseEstimate = Estimate();
            // int prevEstimate = baseEstimate;
            
            // створити 1у популяцію
            var initialPopulation = this.CloneFromUnit();
            
            // IndividualMapper mapper = new IndividualMapper();
            // mapper.PrepareIndividual(initialPopulation);
            
            initialPopulation.currentEstimation = initialPopulation.Estimate();
            population.Add(initialPopulation);
            
            int baseEstimate = Estimate();
            int prevEstimate = baseEstimate;
            
            logger.LogInformation($"ПОЧАТОК ГЕН АЛГОРИТМУ. КІЛЬКІСТЬ ІТЕРАЦІЙ {Parameters.GeneticIterations}");


            // GeneticOperations operations = new GeneticOperations();
            // operations.Setup(logger,initialPopulation);
            // for (var i = 0; i < 50; i++)
            // {
            //     operations.MakeOperation();
            //     foreach (var combinedIndividual in operations.population)
            //     {
            //         var currEst = combinedIndividual.currentEstimation;
            //         logger.LogInformation($"ПІСЛЯ МУТ. №{i} МАЄМО: {currEst} | АБО {currEst - prevEstimate} ВІД НАЙКРАЩОГО РЕЗУЛЬТАТУ");
            //     }
            //     // var currEstimate = Estimate();
            //     
            // }
            
            for (var i = 0; i < Parameters.GeneticIterations; i++)
            {
                limit = 5;
                // if (i % 5 == 0)
                //     SwapTeacherSubjects();
                // else
                //     Mutations(prevEstimate);
                // var currEstimate = MutationsForLongSeries(prevEstimate);
                // Swap();

                // SwapTeacherSubjects();
                // робимо операції для кожної популяції
                newGeneration = new List<Individual>();
                for (var j = 0; j < population.Count; j++)
                {
                    // logger.LogInformation($"ВИКОНУЄМО ОПЕРАЦІЮ ДЛЯ ПОПУЛЯЦІЇ {j} СТРАТЕГІЯ {strategy}");
                    // PopulationSwapGroupSubjects(populations[j]);
                    // PopulationMutationsForLongSeries(populations[j]);
                    // PopulationSwapTeacherSubjects(populations[j]);
                    
                    GeneticAlgorithm(j, population[j]);
                    
                    // if (strategy == 0)
                    //     PopulationMutationsForLongSeries(populations[j]);
                    // else if (strategy == 1)
                    //     PopulationMutationsForShortSeries(populations[j]);
                    // else if (strategy == 2)
                    //     PopulationSwapTeacherSubjects(populations[j]);
                    // else if(strategy == 3)
                    //     PopulationSwapGroupSubjects(populations[j]);
                    
                    // PopulationMutations(populations[j]);
                    // PopulationMutationsForLongSeries(populations[j]);
                    // PopulationSwapTeacherSubjects(populations[j]);
                }
                foreach (var newPopulation in newGeneration)
                    population.Add(newPopulation);
                
                // далі робимо перевірку кількості популяцій прибираючі зайві
                EvaluatePopulations();
                foreach (var population in population.ToList())
                {
                    if (population.currentEstimation < baseEstimate)
                        this.population.Remove(population);
                }
                // раз в декілька операцій розмножимо дану мутацію
                if (i % 2 == 0)
                {
                    var cl = population[0].clone();
                    cl.currentEstimation = population[0].currentEstimation;
                    population.Add(cl);
                }
                
                // створимо копію до того як щось змінили
                // var copy = this.CloneWithPrivateCache();

                // SwapGroupSubjects();
                // SwapTeacherSubjects();
                var currEstimate = population[0].currentEstimation;
                
                // if(currEstimate == 0)
                //     currEstimate = Int32.MinValue;
                // var currEstimate = Estimate();
                
                // а потім візьмемо цю копію і застосуємо її
                // todo створити клас "популяція" на основі всіх цих приватних кешів і так далі, і працювати виключно з нею, щоб можна було її множити  

                logger.LogInformation($"ПІСЛЯ МУТ. №{i} МАЄМО: {currEstimate} | АБО {currEstimate - prevEstimate} ВІД НАЙКРАЩОГО РЕЗУЛЬТАТУ");
                // SelectStrategy(currEstimate - prevEstimate);

                if(currEstimate > prevEstimate)
                    prevEstimate = currEstimate;
            }

            if (prevEstimate > baseEstimate)
            {
                logger.LogInformation($"ДО АЛГОРИМУ: {baseEstimate} ПІСЛЯ АЛГОРИТМУ {prevEstimate}");
                if(baseEstimate != 0)
                    logger.LogInformation($"МИ ЗРОБИЛИ КРАЩЕ НА {prevEstimate - baseEstimate}, АБО У: {prevEstimate / (double)baseEstimate} РАЗ");

                var result = new AlgorithmResultDTO();
                
                result.TotalEstimation = prevEstimate;
                
                result.ScheduleSlots = population[0].Slots.Values.Where(v => v.IsAssigned).Select(x => x.ScheduleSlot).ToList();
                
                // var slots = mapper.RefineIndividualSchedulleSlots(population[0]);
                // result.ScheduleSlots = slots;

                // result.ScheduleSlots = Slots.Values.Where(v => v.IsAssigned).Select(x => x.ScheduleSlot).ToList();
                result.Name = "Genetic";

                return result;
            }
            return null;
        }
        
        private Dictionary<SlotTracker, int> usedTrackers = new();

        //  Спробуємо використовувати мутації лише для серій в 5+ уроків
        [Obsolete("Метод перенесено до класу Individual")]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int MutationsForLongSeries(int prevEstimation)
        {
            if(!isInit)
                PreparePrivateGeneticCache();

            var list = FirstTrackers.Select(e => e).Where(e => (!usedTrackers.ContainsKey(e) || usedTrackers[e] < 3) && e is { IsLowDaysDanger: false, ScheduleSlot.LessonSeriesLength: > 4 }).ToList();
            var firstRandomLesson = list.ElementAt(_random.Next(0, list.Count));
            
            // lets add to not repeat it...
            if (!usedTrackers.TryAdd(firstRandomLesson, 1))
            {
                usedTrackers[firstRandomLesson]++;
            }

            var candidateDomain = firstRandomLesson.AvailableDomains;

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
                cacheDomains.Add(cachedDomainVal);
            }
            var cacheSlot = firstRandomLesson.ScheduleSlot;

            var cacheDomain = new DomainValue();
            cacheDomain.PairNumber = cacheSlot.PairNumber;
            cacheDomain.Date = cacheSlot.Date;
            var aSlots = GetAssignedSlots();
            foreach (var domain in candidateDomain)
            {
                bool isVld = ValidateAssignment(firstRandomLesson, domain, aSlots);

                if (isVld)
                {
                    // візьмем трекери для інших занять дисципліни, щоб також перепризначити їх
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
                            
                            // залогуємо наші зміни щоб було легше шукати в excel таблиці різницю з дефолт алгоритмом
                            logger.LogInformation($"БУЛО: ВИКЛАДАЧ:{firstRandomLesson.ScheduleSlot.GroupSubject.Teacher.Name}|ДАТА:{cacheDomain.Date}|НОМЕР:{cacheDomain.PairNumber} СТАЛО:ДАТА:{firstRandomLesson.ScheduleSlot.Date}|НОМЕР:{firstRandomLesson.ScheduleSlot.PairNumber} ");
                            return res;
                        }
                    }
                }
            }
            
            var trackerToRestore = firstRandomLesson.ScheduleSlot.GroupSubject.ScheduleSlots
                .Select(slot => Slots[slot])
                .Where(tracker => tracker.SeriesId == firstRandomLesson.SeriesId)
                .OrderBy(tracker => tracker.ScheduleSlot.LessonNumber)
                .ToList();

            for (int i = 0; i < trackerToRestore.Count; i++)
            {
                SetSlotAssignedGenetic(trackerToRestore[i], cacheDomains[i]);
            }
            return Estimate();
        }


        private int limit = 10;
        
        [Obsolete("Метод перенесено до класу Individual")]
        // todo рідко але іноді буває розклад отриманий після мутацій не зберігається в API, необхідно знайти де могли б втратитись дані.
        // UPD: наче після того як вийшло відновлювати дані такого поки більше не відбувається...
        public int Mutations(int prevEstimation)
        {

            // todo Достатньо буде створити 1 раз, далі скопіюється з інших джерел
            if(!isInit)
                PreparePrivateGeneticCache();
            
            // Перші заняття в серії - наша популяція з якою ми граємось
            
            // беремо випадковий елемент популяції
            // todo вигадати як переробити щоб не брати кожен раз дисципліну випадково з нуля, а, наприклад, брати зі стеку, та заносити в окремий стек які дисципліни мали успішні та неуспішні мутації, бо цікаво погратись яка комбінація в середньому вигідніша
            var list = FirstTrackers.Select(e => e).Where(e => (!usedTrackers.ContainsKey(e) || usedTrackers[e] < 3) && e is { IsLowDaysDanger: false, ScheduleSlot.LessonSeriesLength: < 4 }).ToList();
            // var list = FirstTrackers.Select(e => e).Where(e => !e.IsLowDaysDanger).ToList();
            var firstRandomLesson = list.ElementAt(_random.Next(0, list.Count));
            
            if (!usedTrackers.TryAdd(firstRandomLesson, 1))
            {
                usedTrackers[firstRandomLesson]++;
            }

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
            
            // скоріш за все для дисциплін з малою кількістю занять і багатьма варіантами,
            // можна обмежити кількість тих доменів що розглядаємо до третини, якщо перша третина не підійшла,
            // мало сенсу розглядати інші
            // if (candidateDomain.Count > 40)
            // {
            //     candidateDomain = new SortedSet<DomainValue>(candidateDomain.Take(40));
            // }

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

            // якщо нічого не змінилось, ми просто перезапустимо мутацію.
            // todo це ж може переповнитись стек, треба переробити метод щоб він виконався до раз 10-20
            // а після цього вважати що мутації поки не приносять результату, і виконати інший метод
            if (limit > 0)
                Mutations(prevEstimation);
            // return Mutations(prevEstimation);
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

        
        List<long> swappedTeachers = new List<long>();
        List<int?> swappedSeries = new List<int?>();
        
        // Заміна місцями двох ідентичних дисциплін однієї і тієї самої групи

        // Зберігаємо всі популяції з якими ми зараз працюємо
        private List<Individual> population = new List<Individual>();
        private List<Individual> newGeneration = new List<Individual>();
        
        private HashSet<int> usedSeries = new HashSet<int>();

        private void PopulationMutationsForLongSeries(Individual individual)
        {
            var newPop = individual.clone();
            var mutatedSeriesDomain = newPop.Mutations(individual.currentEstimation, 4, -1, usedSeries);
            // var mutatedSeriesDomain = newPop.MutationsForLongSeries(population.currentEstimation);
            if(mutatedSeriesDomain != null)
                usedSeries.Add(mutatedSeriesDomain.Value.Key);
            
            if(newPop.currentEstimation >= individual.currentEstimation)
                newGeneration.Add(newPop);
            if (individual != population[0] && 
                individual.currentEstimation < population[0].currentEstimation && 
                newPop.currentEstimation > individual.currentEstimation &&
                newPop.currentEstimation < population[0].currentEstimation
               )
            {
                logger.LogInformation($"Намагаємось перенести зміни на кращий розклад");
                if (mutatedSeriesDomain != null)
                {
                    population[0].ApplyMutation((KeyValuePair<int, DomainValue>)mutatedSeriesDomain);
                }
            }
        }
        
        private void PopulationMutationsForShortSeries(Individual individual)
        {
            var clonedIndividual = individual.clone();
            clonedIndividual.currentEstimation = individual.currentEstimation;
            var mutatedSeriesDomain = clonedIndividual.Mutations(individual.currentEstimation, -1, 3, usedSeries);
            if(mutatedSeriesDomain != null)
                usedSeries.Add(mutatedSeriesDomain.Value.Key);
            if(clonedIndividual.currentEstimation >= individual.currentEstimation)
                newGeneration.Add(clonedIndividual);
            if (individual != population[0] && 
                individual.currentEstimation < population[0].currentEstimation && 
                clonedIndividual.currentEstimation > individual.currentEstimation &&
                clonedIndividual.currentEstimation < population[0].currentEstimation
               )
            {
                logger.LogInformation($"Намагаємось перенести зміни на кращий розклад");
                if (mutatedSeriesDomain != null)
                {
                    population[0].ApplyMutation((KeyValuePair<int, DomainValue>)mutatedSeriesDomain);
                }
            }
        }
        
        private void PopulationSwapTeacherSubjects(Individual individual)
        {
            var clonedIndividual = individual.clone();
            clonedIndividual.currentEstimation = individual.currentEstimation;
            var swappedSubjects = clonedIndividual.SwapTeacherSubjects();
            if(swappedSubjects == null)
                return;
            logger.LogInformation($"ДОДАЄМО ЦЕ У ПОПУЛЯЦІЮ");
            newGeneration.Add(clonedIndividual);
            
            // якщо в цій популяції щось стало краще, дивимось, чи можемо ми перенести цю зміну у найкращу популяцію
            if (individual != population[0] && 
                individual.currentEstimation < population[0].currentEstimation && 
                clonedIndividual.currentEstimation > individual.currentEstimation &&
                clonedIndividual.currentEstimation < population[0].currentEstimation
                )
            {   
                logger.LogInformation($"ОСЬ ТУТ ТРЕБА ПРОБУВАТИ ПЕРЕНЕСТИ ЗМІНИ!");
                if(swappedSubjects != null)
                    population[0].ApplyTeacherSubjectsSwap((KeyValuePair<int, int>)swappedSubjects);
            }
        }
        
        // особливість цієї стратегії - вона створює альтернативні варіанти розкладу з такою ж оцінкою
        private void PopulationSwapGroupSubjects(Individual individual)
        {
            var clonedIndividual = individual.clone();
            var swappedSubjects = clonedIndividual.SwapGroupSubjects();
            if(swappedSubjects == null)
                return;
            logger.LogInformation($"ДОДАЄМО СВАП ГРУП ДО ПОПУЛЯЦІЇ");
            newGeneration.Add(clonedIndividual);
            
            // якщо в цій популяції щось стало краще, дивимось, чи можемо ми перенести цю зміну у найкращу популяцію
            if (individual != population[0] && 
                individual.currentEstimation < population[0].currentEstimation && 
                clonedIndividual.currentEstimation > individual.currentEstimation &&
                clonedIndividual.currentEstimation < population[0].currentEstimation
               )
            {
                logger.LogInformation($"ОСЬ ТУТ ТРЕБА ПРОБУВАТИ ПЕРЕНЕСТИ ЗМІНИ!");
                if(swappedSubjects != null)
                    population[0].ApplyTeacherSubjectsSwap((KeyValuePair<int, int>)swappedSubjects);
            }
        }

        private int populationLimitCount = 3;
        private void EvaluatePopulations()
        {
            population.Sort((a, b) => a.currentEstimation > b.currentEstimation ? -1 : 1);
            // якщо кількість популяцій більша за допустиму межу, видалити останні
            var count = populationLimitCount > population.Count ? population.Count : populationLimitCount;
            population = population[..count];
        }
        
        
        [Obsolete("Метод перенесено до класу Individual")]
        // Заміна місцями двох ідентичних дисциплін одного і того самого викладача
        public int SwapTeacherSubjects()
        {
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

                // var seriesIdsEnumerable = firstRandomLessonTracker.ScheduleSlot.GroupSubject.ScheduleSlots.Select(e => e.LessonSeriesId).Distinct();

                var dicLen = new Dictionary<int, int>();
                
                if(teacherSubjectsTrackers.Count < 2)
                    continue;

                // var slots = teacherSubjectsTrackers.Select(e => e.ScheduleSlot.LessonSeriesLength).Where(e => e.);
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
                            // var aSlots = GetAssignedSlots();
                            var before = Estimate();
                            // замінити місцями.
                            SwapSeriesTrackers(firstSubjectFirstTracker, secondSubjectFirstTracker);
                            var after = Estimate();

                            // якщо ця зміна є негативною, повернути назад
                            if (after <= before)
                            {
                                SwapSeriesTrackers(firstSubjectFirstTracker, secondSubjectFirstTracker);
                            }
                            else
                            {
                                logger.LogInformation($"WE MADE BETTER!: {before} : {after} ");
                                return 0;
                            }
                        }
                    }
                }
                
            }
            
            return 0;
        }

        [Obsolete("Метод перенесено до класу Individual")]
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
        
        [Obsolete("Метод перенесено до класу Individual")]
        public int SwapGroupSubjects()
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

                // var firstGroupSubjects = assignedSlotsByGroupDate[groups[0].Id].Values.ToList();

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

                var before = Estimate();
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
                    
                    List<DomainValue> cacheDomainsFirst = new List<DomainValue>();
                    List<DomainValue> cacheDomainsSecond = new List<DomainValue>();
                    
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
                        if (res == before)
                        {
                        }
                        if (res >= before)
                        {
                            freeTRackersFirst.ForEach(e => Slots[e.ScheduleSlot] = e);
                            freeTRackersSecond.ForEach(e => Slots[e.ScheduleSlot] = e);

                            return res;
                        }
                    }
                        // повертаємо назад
                        SetSlotAssignedGenetic(firstRandomLessonTracker, DomainFirst);
                        SetSlotAssignedGenetic(subjectTracker, DomainSecond);
                        
                        for (int i = 0; i < freeTRackersFirst.Count; i++)
                        {
                            SetSlotAssignedGenetic(freeTRackersFirst[i], cacheDomainsFirst[i]);
                        }
                        for (int i = 0; i < freeTRackersSecond.Count; i++)
                        {
                            SetSlotAssignedGenetic(freeTRackersSecond[i], cacheDomainsSecond[i]);
                        }
                    
                        // var after = Estimate();
                        // // logger.LogInformation($"BEFORE: {before} AFTER {after}");

                }
            }
            
            return 0;
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