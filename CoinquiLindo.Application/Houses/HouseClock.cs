using CoinquiLindo.Domain.Entities;

namespace CoinquiLindo.Application.Houses
{
    internal static class HouseClock
    {
        // Current calendar day in the house time zone: weeks and deadlines are interpreted there (ADR-018)
        public static DateOnly Today(this House house, TimeProvider timeProvider) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), house.GetTimeZone()).DateTime);
    }
}
