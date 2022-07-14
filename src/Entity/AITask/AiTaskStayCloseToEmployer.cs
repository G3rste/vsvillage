using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTaskStayCloseToEmployer : AiTaskStayCloseToGuardedEntity
    {
        public double employedSince => entity.WatchedAttributes.GetDouble("employedSince");
        public double employedForHours { get; set; }
        public AiTaskStayCloseToEmployer(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);
            employedForHours = aiConfig["employedForHours"].AsDouble(24);
        }

        public override bool ShouldExecute()
        {
            if (employedSince + employedForHours < entity.World.Calendar.TotalHours && employedSince > 0)
            {
                entity.WatchedAttributes.RemoveAttribute("guardedPlayerUid");
                entity.WatchedAttributes.RemoveAttribute("guardedEntityId");
                entity.WatchedAttributes.RemoveAttribute("employedSince");
            }
            return base.ShouldExecute();
        }

        public override void OnNoPath(Vec3d target)
        {
            // no random teleporting!!!
        }
    }
}