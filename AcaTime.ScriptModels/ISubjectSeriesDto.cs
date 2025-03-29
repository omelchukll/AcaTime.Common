namespace AcaTime.ScriptModels
{
    public interface ISubjectSeriesDto
    {
        int NumberOfLessons { get; set; }
        int SeriesNumber { get; set; }
        SubjectSeriesSplitType SplitType { get; set; }

        /// <summary>
/// Початок серії може бути в будь-якому тижні, інакше початковий тиждень перший для однотипних серій та перший + другий для двотижневих
/// </summary>
       bool StartInAnyWeek { get; set; }
    }
}