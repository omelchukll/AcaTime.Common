using AcaTime.ScriptModels;

namespace AcaTime.ScheduleCommon.Models.Calc
{
    /// <summary>
    /// Призначені слоти
    /// </summary>
    public class AssignedSlotsDTO : IAssignedSlots
    {
       
      
        /// <summary>
        /// Список призначених слотів
        /// </summary>
        public IReadOnlyList<IScheduleSlot> Slots { get; private set; }

        /// <summary>
        /// Призначені слоти за викладачем. Сгруповано для швидкого доступу.
        /// </summary>
        public IReadOnlyDictionary<long, IReadOnlyList<IScheduleSlot>> SlotsByTeacher { get; private set; }

        /// <summary>
        /// Призначені слоти за групою. Сгруповано для швидкого доступу.
        /// </summary>
        public IReadOnlyDictionary<long, IReadOnlyList<IScheduleSlot>> SlotsByGroup { get; private set; }

        public AssignedSlotsDTO(IReadOnlyList<IScheduleSlot> slots, IReadOnlyDictionary<long, IReadOnlyList<IScheduleSlot>> slotsByTeacher, IReadOnlyDictionary<long, IReadOnlyList<IScheduleSlot>> slotsByGroup)
        {
            Slots = slots;
            SlotsByTeacher = slotsByTeacher;
            SlotsByGroup = slotsByGroup;
        }
    }
}
