using AcaTime.ScriptModels;

namespace AcaTime.ScheduleCommon.Models.Constraints
{
    /// <summary>
    /// Оцінка вибору слота для розкладу
    /// </summary>
    public class SlotPriorityEstimation : BaseConstraint
    {
        public Func<ISlotPriority, int> Func { get; set; }

        public int Estimate(ISlotPriority slot) => Func(slot);
    }
}
