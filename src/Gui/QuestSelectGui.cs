using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace VsVillage
{
    public class QuestSelectGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private long questGiverId;
        private string selectedAvailableQuestId;
        private ActiveQuest selectedActiveQuest;

        private List<string> availableQuestIds;
        private List<ActiveQuest> activeQuests;
        private IClientPlayer player;

        private int curTab = 0;
        public QuestSelectGui(ICoreClientAPI capi, long questGiverId, List<string> availableQuestIds, List<ActiveQuest> activeQuests) : base(capi)
        {
            this.questGiverId = questGiverId;
            this.availableQuestIds = availableQuestIds;
            this.activeQuests = activeQuests;
            selectedActiveQuest = activeQuests == null ? null : activeQuests.Find(quest => true);
            player = capi.World.Player;
            recompose();
        }

        private void OnTabClicked(int tabId)
        {
            curTab = tabId;
            recompose();
        }

        private void recompose()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);

            GuiTab[] tabs = new GuiTab[] {
                new GuiTab() { Name = "Available Quests", DataInt = 0 },
                new GuiTab() { Name = "Active Quests", DataInt = 1 }
            };

            bgBounds.BothSizing = ElementSizing.FitToChildren;
            SingleComposer = capi.Gui.CreateCompo("QuestSelectDialog-", dialogBounds)
                            .AddShadedDialogBG(bgBounds)
                            .AddDialogTitleBar(Lang.Get("vsvillage:quest-select-title"), () => TryClose())
                            .AddVerticalTabs(tabs, ElementBounds.Fixed(-200, 35, 200, 200), OnTabClicked)
                            .BeginChildElements(bgBounds);
            if (curTab == 0)
            {
                if (availableQuestIds != null && availableQuestIds.Count > 0)
                {
                    selectedAvailableQuestId = availableQuestIds[0];
                    SingleComposer.AddDropDown(availableQuestIds.ToArray(), availableQuestIds.ConvertAll<string>(id => Lang.Get(id + "-title")).ToArray(), 0, onAvailableQuestSelectionChanged, ElementBounds.FixedOffseted(EnumDialogArea.RightTop, 0, 20, 400, 20))
                        .AddButton(Lang.Get("vsvillage:button-cancel"), TryClose, ElementBounds.FixedOffseted(EnumDialogArea.LeftBottom, 10, -10, 200, 20))
                        .AddButton(Lang.Get("vsvillage:button-accept"), acceptQuest, ElementBounds.FixedOffseted(EnumDialogArea.RightBottom, -10, -10, 200, 20))
                        .BeginChildElements(ElementBounds.Fixed(40, 60, 400, 500))
                            .AddRichtext(questText(availableQuestIds[0]), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 0, 400, 500), "questtext")
                        .EndChildElements();
                }
                else
                {
                    SingleComposer.AddStaticText(Lang.Get("vsvillage:no-quest-available-desc"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 60, 400, 500))
                        .AddButton(Lang.Get("vsvillage:button-cancel"), TryClose, ElementBounds.FixedOffseted(EnumDialogArea.CenterBottom, 0, -10, 200, 20));
                }
            }
            else
            {
                if (activeQuests != null && activeQuests.Count > 0)
                {
                    SingleComposer.AddDropDown(activeQuests.ConvertAll<string>(quest => quest.questId).ToArray(), activeQuests.ConvertAll<string>(quest => Lang.Get(quest.questId + "-title")).ToArray(), 0, onActiveQuestSelectionChanged, ElementBounds.FixedOffseted(EnumDialogArea.RightTop, 0, 20, 400, 20))
                        .AddButton(Lang.Get("vsvillage:button-cancel"), TryClose, ElementBounds.FixedOffseted(EnumDialogArea.LeftBottom, 10, -10, 200, 20))
                        .AddIf(selectedActiveQuest.isCompletable(player))
                            .AddButton(Lang.Get("vsvillage:button-complete"), completeQuest, ElementBounds.FixedOffseted(EnumDialogArea.RightBottom, -10, -10, 200, 20))
                        .EndIf()
                        .BeginChildElements(ElementBounds.Fixed(40, 60, 400, 500))
                            .AddRichtext(activeQuestText(selectedActiveQuest), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 0, 400, 500), "questtext")
                        .EndChildElements();
                }
                else
                {
                    SingleComposer.AddStaticText(Lang.Get("vsvillage:no-quest-active-desc"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 60, 400, 500))
                        .AddButton(Lang.Get("vsvillage:button-cancel"), TryClose, ElementBounds.FixedOffseted(EnumDialogArea.CenterBottom, 0, -10, 200, 20));
                }
            }
            SingleComposer.EndChildElements()
                    .Compose();
        }

        private void OnTabClicked(int id, GuiTab tab)
        {
            curTab = id;
            recompose();
        }

        private string questText(string questId)
        {
            return String.Format("<strong>{0}</strong><br><br>{1}", Lang.Get(questId + "-title"), Lang.Get(questId + "-desc"));
        }

        private string activeQuestText(ActiveQuest quest)
        {
            return String.Format("{0}<br><br><strong>Progress</strong><br>{1}", questText(quest.questId), Lang.Get(quest.questId + "-obj", quest.progress(player).ConvertAll<string>(x => x.ToString()).ToArray()));
        }

        private bool acceptQuest()
        {
            var message = new QuestAcceptedMessage()
            {
                questGiverId = questGiverId,
                questId = selectedAvailableQuestId
            };
            capi.Network.GetChannel("vsquest").SendPacket(message);
            TryClose();
            return true;
        }

        private bool completeQuest()
        {
            var message = new QuestCompletedMessage()
            {
                questGiverId = questGiverId,
                questId = selectedActiveQuest.questId
            };
            capi.Network.GetChannel("vsquest").SendPacket(message);
            TryClose();
            return true;
        }
        private void onAvailableQuestSelectionChanged(string questId, bool selected)
        {
            if (selected)
            {
                selectedAvailableQuestId = questId;
                SingleComposer.GetRichtext("questtext").SetNewText(questText(questId), CairoFont.WhiteSmallishText());
            }
        }

        private void onActiveQuestSelectionChanged(string questId, bool selected)
        {
            if (selected)
            {
                selectedActiveQuest = activeQuests.Find(quest => quest.questId == questId);
                SingleComposer.GetRichtext("questtext").SetNewText(questText(questId), CairoFont.WhiteSmallishText());
                recompose();
            }
        }
    }
}