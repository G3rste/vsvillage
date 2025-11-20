using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class CooldownCondition : IActionCondition
    {
        public const string ConditionType = "CooldownCondition";

        public string Type => ConditionType;
        [JsonProperty]
        public bool Invert { get; set; }
        [JsonProperty]
        public long CooldownInSeconds = 30;
        public long LastSuccessfulCheck = long.MinValue;

        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var guiRow = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer.AddStaticText("Cooldown in seconds", CairoFont.WhiteDetailText(), guiRow)
                .AddNumberInput(guiRow.RightCopy(), text => { }, null, "CooldownInSeconds")
                .GetNumberInput("CooldownInSeconds").SetValue(30);
        }

        public void StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            CooldownInSeconds = (long)singleComposer.GetNumberInput("CooldownInSeconds").GetValue();
        }

        public bool ConditionSatisfied(Entity entity)
        {
            var elapsedSeconds = entity.World.Calendar.ElapsedSeconds;
            if (LastSuccessfulCheck + CooldownInSeconds < elapsedSeconds)
            {
                LastSuccessfulCheck = elapsedSeconds;
                return true;
            }
            return false;
        }

        public void LoadState(ITreeAttribute tree)
        {
            LastSuccessfulCheck = tree.GetLong("LastSuccessfulCheck", long.MinValue);
        }

        public void OnLoaded(EntityActivitySystem vas)
        {
        }

        public void StoreState(ITreeAttribute tree)
        {
            tree.SetFloat("LastSuccessfulCheck", LastSuccessfulCheck);
        }

        public IActionCondition Clone()
        {
            return new CooldownCondition()
            {
                Invert = Invert,
                CooldownInSeconds = CooldownInSeconds
            };
        }

        public override string ToString()
        {
            return string.Format("Whenever villager hasn't tried this at {0} {1} seconds.", Invert ? "most " : "least", CooldownInSeconds);
        }
    }
}