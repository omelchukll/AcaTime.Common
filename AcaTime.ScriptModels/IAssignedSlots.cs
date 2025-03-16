using System.Collections.Generic;

namespace AcaTime.ScriptModels
{
    /// <summary>
    /// Інтерфейс для призначених слотів з властивостями тільки для читання.
    /// </summary>
    public interface IAssignedSlots
    {
        /// <summary>
        /// Список призначених слотів
        /// </summary>
        IReadOnlyList<IScheduleSlot> Slots { get; }

        /// <summary>
        /// Призначені слоти за викладачем
        /// </summary>
        IReadOnlyDictionary<long, IReadOnlyList<IScheduleSlot>> SlotsByTeacher { get; }

        /// <summary>
        /// Призначені слоти за групою
        /// </summary>
        IReadOnlyDictionary<long, IReadOnlyList<IScheduleSlot>> SlotsByGroup { get; }
    }
}