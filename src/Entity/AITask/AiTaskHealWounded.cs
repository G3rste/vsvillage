using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class AiTraskHealWounded : AiTaskBase
    {
        private float maxDistance { get; set; }

        private float moveSpeed;
        private VillagerWaypointsTraverser villagerPathTraverser;
        private long lastCheck;

        Entity woundedEntity;
        private bool stuck;

        public AiTraskHealWounded(EntityAgent entity) : base(entity)
        {
        }


        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            maxDistance = taskConfig["maxdistance"].AsFloat(25);
            moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);


            villagerPathTraverser = entity.GetBehavior<EntityBehaviorAlternatePathtraverser>().villagerWaypointsTraverser;
        }

        public override bool ShouldExecute()
        {
            var elapsedMs = entity.World.ElapsedMilliseconds;
            if (cooldownUntilMs + lastCheck < elapsedMs)
            {
                lastCheck = elapsedMs;
                if (woundedEntity == null || entity.ServerPos.SquareDistanceTo(woundedEntity.ServerPos.XYZ) > maxDistance * maxDistance * 4)
                {
                    var villagers = entity.World.GetEntitiesAround(entity.ServerPos.XYZ, maxDistance, 5, entity => entity is EntityVillager || entity is EntityPlayer);
                    int maxHpLossIndex = 0;
                    float maxHpLoss = 0;
                    for (int i = 0; i < villagers.Length; i++)
                    {
                        var health = villagers[i].GetBehavior<EntityBehaviorHealth>();
                        if (health != null && maxHpLoss < health.MaxHealth - health.Health)
                        {
                            maxHpLoss = health.MaxHealth - health.Health;
                            maxHpLossIndex = i;
                        }
                    }
                    if (maxHpLoss > 0.5f)
                    {
                        woundedEntity = villagers[maxHpLossIndex];
                    }
                }
                if (woundedEntity != null) { return true; }
            }
            return false;
        }

        public override void StartExecute()
        {
            if (woundedEntity != null)
            {
                stuck = !villagerPathTraverser.NavigateTo(woundedEntity.ServerPos.XYZ, moveSpeed, 0.5f, () => { }, () => stuck = true, true, 10000);
            }
            else
            {
                stuck = true;
            }
            base.StartExecute();
        }

        public override bool ContinueExecute(float dt)
        {
            return !stuck && entity.ServerPos.SquareDistanceTo(woundedEntity.ServerPos.XYZ) > 1f * 1f;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            var interactAnim = new AnimationMetaData
            {
                Code = "Interact",
                Animation = "interact"
            }.Init();
            if (woundedEntity.Alive)
            {
                woundedEntity.ReceiveDamage(new DamageSource()
                {
                    DamageTier = 0,
                    HitPosition = woundedEntity.ServerPos.XYZ,
                    Source = EnumDamageSource.Internal,
                    SourceEntity = null, // otherwise the basegame wants to retaliate attacks ^^
                    Type = EnumDamageType.Heal
                }, 100);
            }
            else { woundedEntity.Revive(); }

            SimpleParticleProperties smoke = new SimpleParticleProperties(
                    10, 15,
                    ColorUtil.ToRgba(75, 146, 175, 122),
                    new Vec3d(),
                    new Vec3d(2, 1, 2),
                    new Vec3f(-0.25f, 0f, -0.25f),
                    new Vec3f(0.25f, 0f, 0.25f),
                    0.6f,
                    -0.075f,
                    0.5f,
                    3f,
                    EnumParticleModel.Quad
                );

            smoke.MinPos = woundedEntity.ServerPos.XYZ.AddCopy(-1.5, -0.5, -1.5);
            entity.World.SpawnParticles(smoke);
            lastCheck = entity.World.ElapsedMilliseconds;
            woundedEntity = null;
        }
    }
}