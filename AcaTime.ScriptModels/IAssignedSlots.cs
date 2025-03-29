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
        /// Отримати призначені слоти за викладачем по ID
        /// </summary>
        IReadOnlyList<IScheduleSlot> GetSlotsByTeacher(long teacherId);

        /// <summary>
        /// Отримати призначені слоти за групою по ID
        /// </summary>
        IReadOnlyList<IScheduleSlot> GetSlotsByGroup(long groupId);
    }
}