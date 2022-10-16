using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsVillage
{
    public abstract class AiTaskGotoAndInteract : AiTaskBase
    {
        protected float maxDistance { get; set; }

        protected float moveSpeed;
        protected VillagerWaypointsTraverser villagerPathTraverser;
        protected long lastSearch;
        protected long lastExecution;
        protected bool stuck;

        protected Vec3d targetPos;

        protected AnimationMetaData interactAnim;

        protected bool targetReached;

        public AiTaskGotoAndInteract(EntityAgent entity) : base(entity)
        {
        }


        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            maxDistance = taskConfig["maxdistance"].AsFloat(5);
            moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);

            interactAnim = new AnimationMetaData
            {
                Code = "interact",
                Animation = taskConfig["interact"].AsString("interact")
            }.Init();


            villagerPathTraverser = entity.GetBehavior<EntityBehaviorAlternatePathtraverser>().villagerWaypointsTraverser;
        }

        public override bool ShouldExecute()
        {
            var elapsedMs = entity.World.ElapsedMilliseconds;
            if (5000 + lastSearch < elapsedMs)
            {
                lastSearch = elapsedMs;
                targetPos = GetTargetPos();
            }
            return targetPos != null && cooldownUntilMs + lastExecution < elapsedMs;
        }

        protected abstract Vec3d GetTargetPos();

        public override void StartExecute()
        {
            stuck = !villagerPathTraverser.NavigateTo(targetPos, moveSpeed, 0.5f, () => { }, () => stuck = true, true, 10000);
            targetReached = false;
            base.StartExecute();
        }

        public override bool ContinueExecute(float dt)
        {
            if (targetReached)
            {
                return entity.AnimManager.IsAnimationActive(interactAnim.Code);
            }
            else if (InteractionPossible())
            {
                entity.AnimManager.StartAnimation(interactAnim);
                targetReached = true;
                return true;
            }
            return !stuck && villagerPathTraverser.Active;
        }

        protected virtual bool InteractionPossible() => entity.ServerPos.SquareDistanceTo(targetPos) < 1.5f * 1.5f;

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            if (targetReached)
            {
                ApplyInteractionEffect();
                lastExecution = entity.World.ElapsedMilliseconds;
            }
            entity.AnimManager.StopAnimation("interact");
            targetPos = null;
            targetReached = false;
        }

        protected abstract void ApplyInteractionEffect();
    }
    public enum ExecutionState
    {
        ON_THE_WAY, INTERACTING, ABORTED
    }
}