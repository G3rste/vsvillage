using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class IntervalUtil
    {
        public static bool matchesCurrentTime(DayTimeFrame[] dayTimeFrames, IWorldAccessor world)
        {
            bool match = false;
            if (dayTimeFrames != null)
            {
                // introduce a bit of randomness so that (e.g.) hens do not all wake up simultaneously at 06:00, which looks artificial
                var hourOfDay = world.Calendar.HourOfDay / world.Calendar.HoursPerDay * 24f + (world.Rand.NextDouble() * 0.3f - 0.15f);
                for (int i = 0; !match && i < dayTimeFrames.Length; i++)
                {
                    match |= dayTimeFrames[i].Matches(hourOfDay);
                }
            }
            return match;
        }
    }
}