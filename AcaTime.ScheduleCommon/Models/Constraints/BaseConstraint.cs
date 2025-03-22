namespace AcaTime.ScheduleCommon.Models.Constraints
{
    public class BaseConstraint
    {
        /// <summary>Ідентифікатор правила (тільки для оновлення).</summary>
        public long? Id { get; set; }

        /// <summary>Назва правила.</summary>
        public string Name { get; set; }

        /// <summary>Опис правила.</summary>
        public string? Description { get; set; }

        /// <summary>Тип правила.</summary>
        public long RuleType { get; set; }
    }
}
