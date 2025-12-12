using Vintagestory.API.Common;

namespace VsVillage
{
    public class IntervalUtil
    {
        public static bool matchesCurrentTime(DayTimeFrame[] dayTimeFrames, IWorldAccessor world, float offset = 0)
        {
            bool match = false;
            if (dayTimeFrames != null)
            {
                var hourOfDay = world.Calendar.HourOfDay / world.Calendar.HoursPerDay * 24f;
                for (int i = 0; !match && i < dayTimeFrames.Length; i++)
                {
                    // Add offset in check to artifically randomize start/stop values
                    match |= dayTimeFrames[i].Matches(hourOfDay + offset);
                }
            }
            return match;
        }
    }
}