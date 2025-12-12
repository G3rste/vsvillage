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
        public AiTaskStayCloseToEmployer(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
          employedForHours = aiConfig["employedForHours"].AsDouble(24);
        }
        
        public override bool ShouldExecute()
        {
            if (employedSince + employedForHours < entity.World.Calendar.TotalHours && employedSince > 0)
            {
                entity.WatchedAttributes.RemoveAttribute("guardedPlayerUid");
                entity.WatchedAttributes.RemoveAttribute("guardedEntityId");
                entity.WatchedAttributes.RemoveAttribute("employedSince");
                entity.WatchedAttributes.MarkPathDirty("guardedPlayerUid");
                entity.WatchedAttributes.MarkPathDirty("guardedEntityId");
            }
            return base.ShouldExecute();
        }

        public override void OnNoPath(Vec3d target)
        {
            // no random teleporting!!!
        }
    }
}