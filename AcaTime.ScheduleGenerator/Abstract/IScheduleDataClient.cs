using AcaTime.ScheduleCommon.Models.Api;

namespace AcaTime.ScheduleGenerator.Abstract
{
    public interface IScheduleDataClient
    {
        Task<SimplifiedScheduleDataDto> GetScheduleDataAsync();
        Task<long> SaveScheduleSlotsAsync(SaveScheduleSlotsRequest saveRequest);
        string ServerUrl { get; }
    }
}