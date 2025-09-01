// using AcaTime.Algorithm.Genetic.Models;
// using AcaTime.ScheduleCommon.Models.Calc;
// using AcaTime.ScheduleCommon.Models.Constraints;
// using AcaTime.ScriptModels;
//
// namespace AcaTime.Algorithm.Second.Services;
//
//     /// <summary>
//     /// Genetic algorithm operators for evolution (to be implemented)
//     /// </summary>
// public static class GeneticOperators 
//     {
//         /// <summary>
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
//         public static Individual TournamentSelection(List<Individual> population, int tournamentSize, Random random)
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
//         public static void RepairIndividual(Individual individual, 
//             Dictionary<IScheduleSlot, SlotTracker> masterSlots,
//             UserFunctions userFunctions, bool ignoreClassrooms, Random random)
//         {
//             var assignedSlots = GeneticScheduleAlgorithmUnit.GetAssignedSlots();
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
//                     // assignedSlots.AddSlot(slot); todo
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
//                         // assignedSlots.AddSlot(slot); todo
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
//         private static bool HasConflicts(ScheduleSlotDTO slot, DomainValue domain, AssignedSlotsDTO assignedSlots)
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
//     }
