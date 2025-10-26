using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
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
        public override void OnTick(float dt)
        {
            base.OnTick(dt);
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
                    VillagePointOfInterest.gatherplace => village.Gatherplaces.ElementAt(villager.entity.World.Rand.Next() % village.Gatherplaces.Count),
                    _ => null
                };

                var path = villager.Pathfind.FindPath(startPos, endPos, villager.Village);

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
                    vas.wppathTraverser.FollowRoute(villager.Pathfind.ToWaypoints(path), WalkSpeed, 2, () => isFinished = true, () => isFinished = true);
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
    }
}