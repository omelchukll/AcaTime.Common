using AcaTime.ScriptModels;

namespace AcaTime.ScheduleCommon.Models.Calc
{
    /// <summary>
    /// Призначені слоти з підтримкою відкладеного прорахунку, кешування та потокобезпечного доступу.
    /// </summary>
    public class AssignedSlotsDTO : IAssignedSlots
    {
        private readonly Func<IEnumerable<IScheduleSlot>> _slotFactory;
        private readonly Func<long, IEnumerable<IScheduleSlot>> _teacherSlotFactory;
        private readonly Func<long, IEnumerable<IScheduleSlot>> _groupSlotFactory;

        private IReadOnlyList<IScheduleSlot> _slots;
        private readonly object _slotsLock = new();

        private readonly Dictionary<long, IReadOnlyList<IScheduleSlot>> _teacherCache = new();
        private readonly object _teacherLock = new();

        private readonly Dictionary<long, IReadOnlyList<IScheduleSlot>> _groupCache = new();
        private readonly object _groupLock = new();

        /// <summary>
        /// Конструктор з підтримкою фабрик для відкладеного обчислення.
        /// </summary>
        public AssignedSlotsDTO(
            Func<IEnumerable<IScheduleSlot>> slotFactory,
            Func<long, IEnumerable<IScheduleSlot>> slotsByTeacherFactory,
            Func<long, IEnumerable<IScheduleSlot>> slotsByGroupFactory)
        {
            _slotFactory = slotFactory;
            _teacherSlotFactory = slotsByTeacherFactory;
            _groupSlotFactory = slotsByGroupFactory;
        }

        /// <summary>
        /// Всі призначені слоти (відкладене обчислення, кешоване, потокобезпечне).
        /// </summary>
        public IReadOnlyList<IScheduleSlot> Slots
        {
            get
            {
                if (_slots != null)
                    return _slots;

                lock (_slotsLock)
                {
                    if (_slots == null)
                    {
                        _slots = _slotFactory().ToList();
                    }
                }

                return _slots;
            }
        }

        /// <summary>
        /// Призначені слоти для конкретного викладача (потокобезпечне кешування).
        /// </summary>
        public IReadOnlyList<IScheduleSlot> GetSlotsByTeacher(long teacherId)
        {
            lock (_teacherLock)
            {
                if (_teacherCache.TryGetValue(teacherId, out var result))
                    return result;

                result = _teacherSlotFactory(teacherId).ToList();
                _teacherCache[teacherId] = result;
                return result;
            }
        }

        /// <summary>
        /// Призначені слоти для конкретної групи (потокобезпечне кешування).
        /// </summary>
        public IReadOnlyList<IScheduleSlot> GetSlotsByGroup(long groupId)
        {
            lock (_groupLock)
            {
                if (_groupCache.TryGetValue(groupId, out var result))
                    return result;

                result = _groupSlotFactory(groupId).ToList();
                _groupCache[groupId] = result;
                return result;
            }
        }
    }


}
