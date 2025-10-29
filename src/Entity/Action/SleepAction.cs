using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Markup;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VsVillage
{
    [JsonObject(MemberSerialization.OptIn)]
    public class SleepAction : EntityActionBase
    {
        public const string ActionType = "Sleep";
        public override string Type => ActionType;
        [JsonProperty]
        public float AnimSpeed = 1;
        [JsonProperty]
        public string AnimCode = "Lie";

        private TimeOfDayCondition timeOfDayCondition;

        public override void Start(EntityActivity entityActivity)
        {
            var villager = vas.Entity.GetBehavior<EntityBehaviorVillager>();
            if (villager?.Bed != null)
            {
                var bed = vas.Entity.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerBed>(villager.Bed);
                if (bed != null)
                {
                    vas.Entity.ServerPos.SetPos(getPos(bed));
                    vas.Entity.ServerPos.Yaw = bed.Yaw;
                    vas.Entity.AnimManager.StartAnimation(new AnimationMetaData()
                    {
                        Code = AnimCode,
                        Animation = AnimCode,
                        AnimationSpeed = AnimSpeed
                    });
                }
            }
            entityActivity?.Conditions.Foreach(candidate =>
            {
                if (candidate is TimeOfDayCondition condition)
                {
                    timeOfDayCondition = condition;
                }
            });
        }

        private Vec3d getPos(BlockEntityVillagerBed bed)
        {
            var cardinal = bed.Block.Variant["side"] switch
            {
                "north" => Cardinal.North,
                "east" => Cardinal.East,
                "south" => Cardinal.South,
                _ => Cardinal.West,
            };
            return bed.Pos.ToVec3d().Add(0.5, 0, 0.5).Add(cardinal.Normalf.Clone().Mul(0.7f));
        }

        public override bool IsFinished()
        {
            return !timeOfDayCondition?.ConditionSatisfied(vas.Entity) ?? true;
        }
        public override void Finish()
        {
            vas.Entity.AnimManager.StopAnimation(AnimCode);
        }

        public override void Pause(EnumInteruptionType interuptionType)
        {
            Finish();
        }

        public override void Resume()
        {
            Start(null);
        }

        public override IEntityAction Clone()
        {
            return new SleepAction()
            {
                vas = vas,
                AnimCode = AnimCode,
                AnimSpeed = AnimSpeed,
                timeOfDayCondition = timeOfDayCondition
            };
        }

        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var animationRow = ElementBounds.Fixed(0, 0, 200, 25);
            var animationSpeedRow = animationRow.BelowCopy();
            var pointsOfInterest = new List<VillagePointOfInterest>(Enum.GetValues<VillagePointOfInterest>()).ConvertAll(poi => poi.ToString()).ToArray();
            singleComposer.AddStaticText("Animation", CairoFont.WhiteDetailText(), animationRow)
                .AddTextInput(animationRow.RightCopy(), text => { }, null, "Animation")
                .AddStaticText("Animationspeed", CairoFont.WhiteDetailText(), animationSpeedRow)
                .AddNumberInput(animationSpeedRow.RightCopy(), text => { }, null, "Animationspeed");
            singleComposer.GetTextInput("Animation").SetValue(AnimCode);
            singleComposer.GetNumberInput("Animationspeed").SetValue(AnimSpeed);
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            AnimCode = singleComposer.GetTextInput("Animation").GetText();
            AnimSpeed = singleComposer.GetNumberInput("Animationspeed").GetValue();

            return true;
        }

        public override string ToString()
        {
            return string.Format("Sleep, Animation {0}, AnimSpeed {1}", AnimCode, AnimSpeed);
        }
    }
}