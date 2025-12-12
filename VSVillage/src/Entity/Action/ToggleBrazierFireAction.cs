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
    public class ToggleBrazierFireAction : EntityActionBase
    {
        public string[] states = ["extinguish", "ignite"];
        public const string ActionType = "ToggleBrazierFire";
        public override string Type => ActionType;
        [JsonProperty]
        public float MaxDistance = 3;
        [JsonProperty]
        public bool Ignite = true;

        public override void Start(EntityActivity entityActivity)
        {
            var pos = vas.Entity.ServerPos;
            vas.Entity.GetBehavior<EntityBehaviorVillager>()?.Village?.Gatherplaces?.Foreach(gatherplace =>
            {
                if (gatherplace.DistanceSqTo(pos.X, pos.Y, pos.Z) < MaxDistance * MaxDistance)
                {
                    var brazier = vas.Entity.World.BlockAccessor.GetBlockEntity<BlockEntityVillagerBrazier>(gatherplace);
                    brazier?.Toggle(Ignite);
                }
            });
        }

        public override IEntityAction Clone()
        {
            return new ToggleBrazierFireAction()
            {
                vas = vas,
                Ignite = Ignite,
                MaxDistance = MaxDistance
            };
        }

        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var igniteRow = ElementBounds.Fixed(0, 0, 200, 25);
            var maxDistanceRow = igniteRow.BelowCopy();
            var pointsOfInterest = new List<VillagePointOfInterest>(Enum.GetValues<VillagePointOfInterest>()).ConvertAll(poi => poi.ToString()).ToArray();
            singleComposer.AddStaticText("Action", CairoFont.WhiteDetailText(), igniteRow)
                .AddDropDown(states, states, 0, (code, selected) => { }, igniteRow.RightCopy(), "Action")
                .AddStaticText("MaxDistance", CairoFont.WhiteDetailText(), maxDistanceRow)
                .AddNumberInput(maxDistanceRow.RightCopy(), text => { }, null, "MaxDistance");
            singleComposer.GetDropDown("Action").SetSelectedIndex(Ignite ? 1 : 0);
            singleComposer.GetNumberInput("MaxDistance").SetValue(MaxDistance);
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            Ignite = singleComposer.GetDropDown("Action").SelectedValue == "ignite";
            MaxDistance = singleComposer.GetNumberInput("MaxDistance").GetValue();

            return true;
        }

        public override string ToString()
        {
            return string.Format("{0} brazier within {1} Blocks", Ignite ? "Ignite" : "Extinguish", MaxDistance);
        }
    }
}