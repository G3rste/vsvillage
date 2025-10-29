using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsVillage
{
    [JsonObject(MemberSerialization.OptIn)]
    public class GotoPointOfInterestAction : EntityActionBase
    {
        public const string ActionType = "GotoPointOfInterest";
        public override string Type => ActionType;
        [JsonProperty]
        public VillagePointOfInterest Target = VillagePointOfInterest.bed;
        [JsonProperty]
        public float AnimSpeed = 1;
        [JsonProperty]
        public float WalkSpeed = 0.02f;
        [JsonProperty]
        public string AnimCode = "walk";

        private bool isFinished = true;
        private List<VillagerPathNode> path = null;
        private int index = 0;
        public override void OnTick(float dt)
        {
            var entityPos = vas.Entity.ServerPos;
            if (index < path?.Count - 1)
            {
                var distance = path[index].BlockPos.DistanceSqTo(entityPos.X, entityPos.Y, entityPos.Z);
                if (distance > 1)
                {
                    for (int i = index; i < path.Count; i++)
                    {
                        if (path[i].BlockPos.DistanceSqTo(entityPos.X, entityPos.Y, entityPos.Z) < distance)
                        {
                            index = i;
                            break;
                        }
                    }
                }
                if (path.Count >= index && index > 0 && path[index - 1].IsDoor)
                {
                    toggleDoor(false, path[index - 1].BlockPos);

                }
                if (path.Count > index && path[index].IsDoor)
                {
                    toggleDoor(true, path[index].BlockPos);
                }
                if (path.Count > index + 1 && path[index + 1].IsDoor)
                {
                    toggleDoor(true, path[index + 1].BlockPos);
                }
            }
        }

        public override void Start(EntityActivity entityActivity)
        {
            var villager = vas.Entity.GetBehavior<EntityBehaviorVillager>();
            var village = villager?.Village;
            if (village != null)
            {
                var startPos = vas.Entity.ServerPos.AsBlockPos;
                var endPos = Target switch
                {
                    VillagePointOfInterest.workstation => villager.Workstation,
                    VillagePointOfInterest.bed => villager.Bed,
                    VillagePointOfInterest.gatherplace => village.Gatherplaces.Count > 0 ? village.Gatherplaces.ElementAt(villager.entity.World.Rand.Next() % village.Gatherplaces.Count) : null,
                    _ => null
                };
                index = 0;
                path = villager.Pathfind.FindPath(startPos, endPos, villager.Village);

                if (path != null)
                {
                    isFinished = false;
                    vas.Entity.AnimManager.StartAnimation(new AnimationMetaData()
                    {
                        Animation = AnimCode,
                        Code = AnimCode,
                        AnimationSpeed = AnimSpeed,
                        BlendMode = EnumAnimationBlendMode.Average
                    }.Init());
                    vas.wppathTraverser.FollowRoute(villager.Pathfind.ToWaypoints(path), WalkSpeed, 1f, () => isFinished = true, () => isFinished = true);
                }
            }
            base.Start(entityActivity);
        }

        public override void Pause(EnumInteruptionType interuptionType)
        {
            Finish();
        }

        public override void Resume()
        {
            Start(null);
        }

        public override bool IsFinished()
        {
            return isFinished;
        }

        public override void Finish()
        {
            isFinished = true;
            vas.linepathTraverser.Stop();
            vas.wppathTraverser.Stop();
            vas.Entity.AnimManager.StopAnimation(AnimCode);
            vas.Entity.Controls.StopAllMovement();
        }

        public override IEntityAction Clone()
        {
            return new GotoPointOfInterestAction()
            {
                vas = vas,
                Target = Target,
                AnimCode = AnimCode,
                AnimSpeed = AnimSpeed,
                WalkSpeed = WalkSpeed
            };
        }

        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var poiRow = ElementBounds.Fixed(0, 0, 200, 25);
            var walkspeedRow = poiRow.BelowCopy();
            var animationRow = walkspeedRow.BelowCopy();
            var animationSpeedRow = animationRow.BelowCopy();
            var pointsOfInterest = new List<VillagePointOfInterest>(Enum.GetValues<VillagePointOfInterest>()).ConvertAll(poi => poi.ToString()).ToArray();
            singleComposer.AddStaticText("Point of Interest", CairoFont.WhiteDetailText(), poiRow)
                .AddDropDown(pointsOfInterest, pointsOfInterest, 0, (index, selected) => { }, poiRow.RightCopy(), "Target")
                .AddStaticText("Walkspeed", CairoFont.WhiteDetailText(), walkspeedRow)
                .AddNumberInput(walkspeedRow.RightCopy(), text => { }, null, "Walkspeed")
                .AddStaticText("Animation", CairoFont.WhiteDetailText(), animationRow)
                .AddTextInput(animationRow.RightCopy(), text => { }, null, "Animation")
                .AddStaticText("Animationspeed", CairoFont.WhiteDetailText(), animationSpeedRow)
                .AddNumberInput(animationSpeedRow.RightCopy(), text => { }, null, "Animationspeed");

            singleComposer.GetDropDown("Target").SetSelectedIndex((int)Target);
            singleComposer.GetNumberInput("Walkspeed").SetValue(WalkSpeed);
            singleComposer.GetTextInput("Animation").SetValue(AnimCode);
            singleComposer.GetNumberInput("Animationspeed").SetValue(AnimSpeed);
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            Target = Enum.Parse<VillagePointOfInterest>(singleComposer.GetDropDown("Target").SelectedValue);
            WalkSpeed = singleComposer.GetNumberInput("Walkspeed").GetValue();
            AnimCode = singleComposer.GetTextInput("Animation").GetText();
            AnimSpeed = singleComposer.GetNumberInput("Animationspeed").GetValue();

            return true;
        }

        public override string ToString()
        {
            return string.Format("Goto {0}, Walkspeed {1}, Animation {2}, AnimSpeed {3}", Target, WalkSpeed, AnimCode, AnimSpeed);
        }

        private void toggleDoor(bool opened, BlockPos target)
        {
            var block = vas.Entity.Api.World.BlockAccessor.GetBlock(target);
            var blockSel = new BlockSelection()
            {
                Block = block,
                Position = target,
                HitPosition = new Vec3d(0.5, 0.5, 0.5),
                Face = BlockFacing.NORTH
            };
            var args = new TreeAttribute();
            args.SetBool("opened", opened);

            block.Activate(vas.Entity.World, new Caller() { Entity = vas.Entity, Type = EnumCallerType.Entity, Pos = vas.Entity.Pos.XYZ }, blockSel, args);
        }
    }
}