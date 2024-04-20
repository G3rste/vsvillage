using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VsVillage
{
    public class ManagementGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;
        private VillageManagementMessage managementMessage = new();

        public ManagementGui(ICoreClientAPI capi, BlockPos pos, Village village = null) : base(capi)
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            if (village == null || village.Pos == null)
            {
                managementMessage.Pos = pos ?? capi.World.BlockAccessor.GetChunkAtBlockPos(capi.World.Player.Entity.Pos.AsBlockPos).BlockEntities.Where(entry => entry.Value.Block is BlockMayorWorkstation).First().Value.Pos;
                SingleComposer = capi.Gui.CreateCompo("VillageManagementDialog-", dialogBounds)
                    .AddShadedDialogBG(bgBounds)
                    .AddDialogTitleBar(Lang.Get("vsvillage:management-title"), () => TryClose())
                    .BeginChildElements(bgBounds)
                        .AddStaticText(Lang.Get("vsvillage:management-no-village-found"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 20, 500, 30))
                        .AddStaticText(Lang.Get("vsvillage:management-village-name"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 80, 200, 30))
                        .AddTextInput(ElementBounds.Fixed(100, 80, 200, 30), name => managementMessage.Name = name, CairoFont.WhiteSmallishText())
                        .AddStaticText(Lang.Get("vsvillage:management-village-radius"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 110, 200, 30))
                        .AddNumberInput(ElementBounds.Fixed(100, 110, 200, 30), radius => int.TryParse(radius, out managementMessage.Radius))
                        .AddButton(Lang.Get("vsvillage:management-found-new-village"), () => createVillage(capi), ElementBounds.Fixed(0, 140, 200, 30))
                    .EndChildElements()
                    .Compose();
            }
            else
            {
                managementMessage.Id = village.Id;
                managementMessage.Radius = village.Radius;
                managementMessage.Name = village.Name;

                recompose(capi, village, dialogBounds, bgBounds);
            }
        }

        private void recompose(ICoreClientAPI capi, Village village, ElementBounds dialogBounds, ElementBounds bgBounds, int curTab = 0)
        {
            GuiTab[] tabs = new GuiTab[] {
                new GuiTab() { Name = Lang.Get("vsvillage:tab-management-residents"), DataInt = 0, Active = curTab == 0 },
                new GuiTab() { Name = Lang.Get("vsvillage:tab-management-structures"), DataInt = 1, Active = curTab == 1 },
                new GuiTab() { Name = Lang.Get("vsvillage:tab-management-stats"), DataInt = 2, Active = curTab == 2 },
                new GuiTab() { Name = Lang.Get("vsvillage:tab-management-destroy"), DataInt = 3, Active = curTab == 3 }
            };

            SingleComposer = capi.Gui.CreateCompo("VillageManagementDialog-", dialogBounds)
                                            .AddShadedDialogBG(bgBounds)
                                            .AddDialogTitleBar(Lang.Get("vsvillage:management-title"), () => TryClose())
                                            .AddVerticalTabs(tabs, ElementBounds.Fixed(-200, 35, 200, 200), (id, tab) => recompose(capi, village, dialogBounds, bgBounds, id), "tabs")
                                            .BeginChildElements(bgBounds);
            switch (curTab)
            {
                case 0:
                    var villagerIds = village.VillagerSaveData.ConvertAll(data => data.Id.ToString()).ToArray();
                    var villagerNames = village.VillagerSaveData.ConvertAll(data => data.Name).ToArray();
                    if (villagerIds.Length > 0)
                    {
                        SingleComposer
                            .AddStaticText(Lang.Get("vsvillage:management-select-villager"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 20, 200, 30))
                            .AddDropDown(villagerIds, villagerNames, 0, (code, sel) => SingleComposer.GetDynamicText("villager-note").SetNewText(villagerNote(code, capi)), ElementBounds.Fixed(200, 20, 300, 30), "villagers")
                            .AddButton(Lang.Get("vsvillage:management-remove-villager"), () => removeVillager(capi), ElementBounds.Fixed(520, 20, 200, 30))
                            .AddDynamicText(villagerNote(villagerIds[0], capi), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 60, 500, 150), "villager-note");
                    }
                    else
                    {
                        SingleComposer
                            .AddStaticText(Lang.Get("vsvillage:management-emtpy"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 20, 500, 30));
                    }
                    break;
                case 1:
                    var structureIds = village.Workstations.ConvertAll(workstation => workstation.Pos.ToString());
                    structureIds.AddRange(village.Beds.ConvertAll(bed => bed.Pos.ToString()));
                    structureIds.AddRange(village.Gatherplaces.ConvertAll(gatherplace => gatherplace.ToString()));

                    var structureNames = village.Workstations.ConvertAll(workstation => string.Format("{0}, {1}", Lang.Get(workstation.Profession), BlockPosToString(workstation.Pos, capi)));
                    structureNames.AddRange(village.Beds.ConvertAll(bed => string.Format("{0}, {1}", Lang.Get("bed"), BlockPosToString(bed.Pos, capi))));
                    structureNames.AddRange(village.Gatherplaces.ConvertAll(gatherplace => string.Format("{0}, {1}", Lang.Get("gatherplace"), BlockPosToString(gatherplace, capi))));
                    if (structureIds.Count > 0)
                    {
                        SingleComposer
                            .AddStaticText(Lang.Get("vsvillage:management-select-structure"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 20, 200, 30))
                            .AddDropDown(structureIds.ToArray(), structureNames.ToArray(), 0, (code, sel) => SingleComposer.GetDynamicText("structure-note").SetNewText(structureNote(village, code, capi)), ElementBounds.Fixed(200, 20, 300, 30), "structures")
                            .AddButton(Lang.Get("vsvillage:management-remove-structure"), () => removeStructure(capi), ElementBounds.Fixed(520, 20, 200, 30))
                            .AddDynamicText(structureNote(village, structureIds[0], capi), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 60, 500, 150), "structure-note");
                    }
                    else
                    {
                        SingleComposer
                            .AddStaticText(Lang.Get("vsvillage:management-emtpy"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 20, 500, 30));
                    }
                    break;
                case 2:
                    SingleComposer
                        .AddStaticText(Lang.Get("vsvillage:management-village-name"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 20, 200, 30))
                        .AddTextInput(ElementBounds.Fixed(100, 20, 200, 30), name => managementMessage.Name = name, CairoFont.WhiteSmallishText(), "villagename")
                        .AddStaticText(Lang.Get("vsvillage:management-village-radius"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 60, 200, 30))
                        .AddNumberInput(ElementBounds.Fixed(100, 60, 200, 30), radius => int.TryParse(radius, out managementMessage.Radius), null, "villageradius")
                        .AddButton(Lang.Get("vsvillage:management-update-village-button"), () => changeStatsVillage(capi), ElementBounds.Fixed(0, 100, 200, 30));
                    break;
                case 3:
                    SingleComposer.AddStaticText(Lang.Get("vsvillage:management-destroy-village-text"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 20, 500, 30))
                        .AddButton(Lang.Get("vsvillage:management-destroy-village-button"), () => destroyVillage(capi), ElementBounds.Fixed(0, 50, 200, 30));
                    break;
            }
            SingleComposer.EndChildElements()
                    .Compose();

            SingleComposer.GetTextInput("villagename")?.SetValue(village.Name);
            SingleComposer.GetTextInput("villageradius")?.SetValue(village.Radius);
        }

        private string villagerNote(string code, ICoreClientAPI capi)
        {
            if (capi.World.GetEntityById(long.Parse(code)) is EntityVillager villager)
            {
                return Lang.Get("vsvillage:management-villager-note",
                    villager.GetBehavior<EntityBehaviorNameTag>().DisplayName,
                    Lang.Get(villager.Profession),
                    BlockPosToString(villager.Pos.AsBlockPos, capi),
                    BlockPosToString(villager.Workstation, capi),
                    BlockPosToString(villager.Bed, capi));
            }
            return Lang.Get("vsvillage:missing-in-action");
        }

        private string structureNote(Village village, string code, ICoreClientAPI capi)
        {
            var workstation = village.Workstations.Find(candidate => candidate.Pos.ToString().Equals(code));
            if (workstation != null)
            {
                return Lang.Get("vsvillage:management-structure-note",
                    Lang.Get("vsvillage:" + workstation.Profession),
                    capi.World.GetEntityById(workstation.OwnerId)?.GetBehavior<EntityBehaviorNameTag>().DisplayName ?? Lang.Get("nobody"),
                    BlockPosToString(workstation.Pos, capi));
            }
            var bed = village.Beds.Find(candidate => candidate.Pos.ToString().Equals(code));
            if (bed != null)
            {
                return Lang.Get("vsvillage:management-structure-note",
                    Lang.Get("vsvillage:bed"),
                    capi.World.GetEntityById(bed.OwnerId)?.GetBehavior<EntityBehaviorNameTag>().DisplayName ?? Lang.Get("nobody"),
                    BlockPosToString(bed.Pos, capi));
            }
            var gatherplace = village.Gatherplaces.Find(candidate => candidate.ToString().Equals(code));
            if (gatherplace != null)
            {
                return Lang.Get("vsvillage:management-structure-note",
                    Lang.Get("vsvillage:gatherplace"),
                    Lang.Get("vsvillage:everybody"),
                    BlockPosToString(gatherplace, capi));
            }
            return null;
        }

        private bool createVillage(ICoreClientAPI capi)
        {
            managementMessage.Operation = EnumVillageManagementOperation.create;
            capi.Network.GetChannel("villagemanagementnetwork").SendPacket(managementMessage);
            TryClose();
            return true;
        }

        private bool destroyVillage(ICoreClientAPI capi)
        {
            managementMessage.Operation = EnumVillageManagementOperation.destroy;
            capi.Network.GetChannel("villagemanagementnetwork").SendPacket(managementMessage);
            TryClose();
            return true;
        }

        private bool changeStatsVillage(ICoreClientAPI capi)
        {
            managementMessage.Operation = EnumVillageManagementOperation.changeStats;
            capi.Network.GetChannel("villagemanagementnetwork").SendPacket(managementMessage);
            TryClose();
            return true;
        }

        private bool removeStructure(ICoreClientAPI capi)
        {
            managementMessage.Operation = EnumVillageManagementOperation.removeStructure;
            managementMessage.StructureToRemove = BlockPosFromString(SingleComposer.GetDropDown("structures").SelectedValue);
            capi.Network.GetChannel("villagemanagementnetwork").SendPacket(managementMessage);
            TryClose();
            return true;
        }

        private bool removeVillager(ICoreClientAPI capi)
        {
            managementMessage.Operation = EnumVillageManagementOperation.removeVillager;
            managementMessage.VillagerToRemove = long.Parse(SingleComposer.GetDropDown("villagers").SelectedValue);
            capi.Network.GetChannel("villagemanagementnetwork").SendPacket(managementMessage);
            TryClose();
            return true;
        }

        public static string BlockPosToString(BlockPos pos, ICoreAPI api)
        {
            return pos != null
                ? string.Format("X={0}, Y={1}, Z={2}", pos.X - api.World.BlockAccessor.MapSizeX / 2, pos.Y, pos.Z - api.World.BlockAccessor.MapSizeZ / 2)
                : Lang.Get("not-found");
        }
        public static BlockPos BlockPosFromString(string pos)
        {
            var vector = new List<Match>(Regex.Matches(pos, "\\d+")).ConvertAll(match => int.Parse(match.Value));
            return new BlockPos(vector[0], vector[1], vector[2], vector.Count > 3 ? vector[3] : 0);
        }
    }
}