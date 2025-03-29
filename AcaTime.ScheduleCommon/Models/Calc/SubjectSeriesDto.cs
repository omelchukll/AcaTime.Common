﻿using AcaTime.ScriptModels;

namespace AcaTime.ScheduleCommon.Models.Calc
{
    /// <summary>
    /// DTO клас для SubjectSeries (серії уроків).
    /// </summary>
    public class SubjectSeriesDto : ISubjectSeriesDto
    {


        /// <summary>Номер серії.</summary>
        public int SeriesNumber { get; set; }

        /// <summary>
        /// Кількість уроків у серії.
        /// </summary>
        public int NumberOfLessons { get; set; }


        public SubjectSeriesSplitType SplitType { get; set; }


        /// <summary>
/// Початок серії може бути в будь-якому тижні, інакше початковий тиждень перший для однотипних серій та перший + другий для двотижневих
/// </summary>
        public bool StartInAnyWeek { get; set; }
      
    }

}
