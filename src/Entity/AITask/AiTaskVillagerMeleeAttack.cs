using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTaskVillagerMeleeAttack : AiTaskMeleeAttack
    {

        public AnimationMetaData baseAnimMeta { get; set; }
        public AnimationMetaData stabAnimMeta { get; set; }
        public AnimationMetaData slashAnimMeta { get; set; }
        public AiTaskVillagerMeleeAttack(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            baseAnimMeta = animMeta;

            if (taskConfig["stabanimation"].Exists)
            {
                stabAnimMeta = new AnimationMetaData()
                {
                    Code = taskConfig["stabanimation"].AsString()?.ToLowerInvariant(),
                    Animation = taskConfig["stabanimation"].AsString()?.ToLowerInvariant(),
                    AnimationSpeed = taskConfig["stabanimationSpeed"].AsFloat(1f)
                }.Init();
            }

            if (taskConfig["slashanimation"].Exists)
            {
                slashAnimMeta = new AnimationMetaData()
                {
                    Code = taskConfig["slashanimation"].AsString()?.ToLowerInvariant(),
                    Animation = taskConfig["slashanimation"].AsString()?.ToLowerInvariant(),
                    AnimationSpeed = taskConfig["slashanimationSpeed"].AsFloat(1f)
                }.Init();
            }
        }

        public override void StartExecute()
        {
            if (entity.RightHandItemSlot != null && !entity.RightHandItemSlot.Empty)
            {
                if (entity.RightHandItemSlot.Itemstack.Item.Code.Path.Contains("spear"))
                {
                    animMeta = stabAnimMeta;
                }
                else
                {
                    animMeta = slashAnimMeta;
                }
            }
            else
            {
                animMeta = baseAnimMeta;
            }
            base.StartExecute();
        }
    }
}