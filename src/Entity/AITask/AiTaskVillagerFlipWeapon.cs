using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTraskVillagerFlipWeapon : AiTaskIdle
    {
        private string weapon;

        public AiTraskVillagerFlipWeapon(EntityAgent entity) : base(entity)
        {
        }


        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            weapon = taskConfig["weapon"].AsString("sword");
        }

        public override bool ShouldExecute()
        {
            if (base.ShouldExecute())
            {
                return (entity as EntityVillager).HasWeapon(value => value.Contains(weapon));
            }
            return false;
        }

        public override void StartExecute()
        {
            (entity as EntityVillager).DrawWeapon(value => value.Contains(weapon));
            base.StartExecute();
        }
    }
}