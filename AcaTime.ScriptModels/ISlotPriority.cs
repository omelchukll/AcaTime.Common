namespace AcaTime.ScriptModels
{
    /// <summary>
    /// Інтерфейс для пріоритету слотів
    /// </summary>
    public interface ISlotPriority
    {
        /// <summary>
        /// Кількість можливих варіантів для розкладу
        /// </summary>
        int AvailableDomains { get; }

        /// <summary>
        /// Чи закінчується на неповному тижні
        /// </summary>
        bool EndsOnIncompleteWeek { get; }

        /// <summary>
        /// Кількість груп
        /// </summary>
        int GroupCount { get; }

        /// <summary>
        /// Довжина серії занять
        /// </summary>
        int LessonSeriesLength { get; }
    }
}