using AcaTime.Algorithm.Genetic.Models;
using AcaTime.ScheduleCommon.Models.Calc;

namespace AcaTime.Algorithm.Genetic.Services;

/// <summary>
/// Represents an individual in the genetic algorithm population
/// </summary>
public class Individual
{
    /// <summary>
    /// Maps each schedule slot to its assigned domain value (time slot)
    /// </summary>
    public Dictionary<ScheduleSlotDTO, DomainValue> Assignments { get; set; } 
        = new Dictionary<ScheduleSlotDTO, DomainValue>();

    /// <summary>
    /// Fitness score of this individual
    /// </summary>
    public double Fitness { get; set; }

    /// <summary>
    /// Creates a deep copy of this individual
    /// </summary>
    public Individual Clone()
    {
        return new Individual
        {
            Assignments = new Dictionary<ScheduleSlotDTO, DomainValue>(Assignments),
            Fitness = Fitness
        };
    }
}
