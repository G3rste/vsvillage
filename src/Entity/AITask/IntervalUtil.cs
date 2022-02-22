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
                var hourOfDay = world.Calendar.HourOfDay / world.Calendar.HoursPerDay * 24f;
                for (int i = 0; !match && i < dayTimeFrames.Length; i++)
                {
                    match |= dayTimeFrames[i].Matches(hourOfDay);
                }
            }
            return match;
        }
    }
}