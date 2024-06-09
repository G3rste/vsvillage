using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using System.IO;
using System;
using System.Collections.Generic;
using Vintagestory.API.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;

namespace VsVillage
{
    public class EntityVillager : EntityAgent
    {
        public static OrderedDictionary<string, TraderPersonality> Personalities = new OrderedDictionary<string, TraderPersonality>()
        {
            { "formal", new TraderPersonality(1, 1, 0.9f) },
            { "balanced", new TraderPersonality(1.2f, 0.9f, 1.1f) },
            { "lazy", new TraderPersonality(1.65f, 0.7f, 0.9f) },
            { "rowdy", new TraderPersonality(0.75f, 1f, 1.8f) }
        };
        protected InventoryVillagerGear gearInv;
        public override IInventory GearInventory => gearInv;

        public override ItemSlot LeftHandItemSlot { get => gearInv.leftHandSlot; set => gearInv.leftHandSlot = value; }
        public override ItemSlot RightHandItemSlot { get => gearInv.rightHandSlot; set => gearInv.rightHandSlot = value; }

        public EntityTalkUtil talkUtil { get; set; }
        public string Personality
        {
            get { return WatchedAttributes.GetString("personality", "formal"); }
            set
            {
                WatchedAttributes.SetString("personality", value);
                talkUtil?.SetModifiers(Personalities[value].ChorldDelayMul, Personalities[value].PitchModifier, Personalities[value].VolumneModifier);
            }
        }

        public EntityVillager()
        {
            AnimManager = new TraderAnimationManager();
        }
        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            if (gearInv == null) { gearInv = new InventoryVillagerGear(Code.Path, "villagerInv-" + EntityId, api); }
            else { gearInv.Api = api; }
            gearInv.SlotModified += gearInvSlotModified;
            gearInv.LateInitialize(gearInv.InventoryID, api);
            var slots = new ItemSlot[gearInv.Count];
            for (int i = 0; i < gearInv.Count; i++)
            {
                slots[i] = gearInv[i];
            }
            if (!WatchedAttributes.HasAttribute("personality"))
            {
                Personality = Personalities.GetKeyAtIndex(World.Rand.Next(EntityTrader.Personalities.Count));
            }
            (AnimManager as TraderAnimationManager).Personality = Personality;
            if (api is ICoreClientAPI capi) { talkUtil = new EntityTalkUtil(capi, this); }
            this.Personality = this.Personality; // to update the talkutil
            if (api is ICoreServerAPI sapi)
            {
                sapi.World.RegisterGameTickListener(dt => UndrawWeaponIfOutOfCombat(), 10000, 10000);
            }
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
        {
            base.OnInteract(byEntity, slot, hitPosition, mode);
            if (Alive && byEntity is EntityPlayer player && WatchedAttributes.GetString("guardedPlayerUid") == player.PlayerUID && mode == EnumInteractMode.Interact && !player.Controls.Sneak)
            {
                bool commandSit = WatchedAttributes.GetBool("commandSit", false);
                WatchedAttributes.SetBool("commandSit", !commandSit);
                WatchedAttributes.MarkPathDirty("commandSit");
            }
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
        {
            if (WatchedAttributes.GetString("guardedPlayerUid") == player.PlayerUID && Alive)
            {
                string actionLangCode = WatchedAttributes.GetBool("commandSit", false) ? "vsvillage:command-follow" : "vsvillage:command-stay";
                return new WorldInteraction[]{
                    new WorldInteraction(){
                        ActionLangCode = actionLangCode,
                        MouseButton = EnumMouseButton.Right
                    }
                };
            }
            else
            {
                return base.GetInteractionHelp(world, es, player);
            }
        }

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();
            foreach (var gear in Enum.GetNames(typeof(VillagerGearType)))
            {
                var possibleGear = Properties.Attributes["validGear"][gear.ToLower()].AsArray<string>();
                if (possibleGear.Length > 0)
                {
                    var slot = new DummySlot(new ItemStack(Api.World.GetItem(new AssetLocation("vsvillage", String.Format("villagergear-{0}-{1}", gear.ToLower(), possibleGear[World.Rand.Next(0, possibleGear.Length)])))));
                    slot.TryPutInto(World, GearInventory.GetBestSuitedSlot(slot).slot);
                }
            }
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (Api.Side == EnumAppSide.Client && !AnimManager.IsAnimationActive("sleep"))
            {
                talkUtil?.OnGameTick(dt);
            }
        }

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging)
        {
            base.OnTesselation(ref entityShape, shapePathForLogging);
            foreach (var slot in GearInventory)
            {
                addGearToShape(slot, entityShape, shapePathForLogging);
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);
            if (packetid == 1235)
            {
                TreeAttribute tree = new TreeAttribute();
                SerializerUtil.FromBytes(data, (r) => tree.FromBytes(r));
                gearInv.FromTreeAttributes(tree);
                foreach (var slot in gearInv)
                {
                    slot.OnItemSlotModified(slot.Itemstack);
                }
            }
        }

        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);

            if (gearInv == null) { gearInv = new InventoryVillagerGear(Code.Path, "villagerInv-" + EntityId, null); }
            gearInv.FromTreeAttributes(getInventoryTree());
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            try
            {
                gearInv.ToTreeAttributes(getInventoryTree());
            }
            catch (NullReferenceException)
            {
                // ignore, better save whats left of it
            }

            base.ToBytes(writer, forClient);
        }

        public bool HasWeapon(System.Func<string, bool> matcher = null)
        {
            foreach (var gear in gearInv)
            {
                var assetString = (gear?.Itemstack?.Item as ItemVillagerGear)?.weaponAssetLocation;
                if (!String.IsNullOrEmpty(assetString) && (matcher == null || matcher.Invoke(gear?.Itemstack?.Item?.Code.Path)))
                {
                    return true;
                }
            }
            return false;
        }
        public void DrawWeapon(System.Func<string, bool> matcher = null)
        {
            var availableWeapons = new List<ItemSlot>();
            System.Func<ItemSlot, string> assetStringFromSlot = slot => (slot?.Itemstack?.Item as ItemVillagerGear)?.weaponAssetLocation;

            // get all slots containing weapons
            foreach (var gear in gearInv)
            {
                var assetString = assetStringFromSlot.Invoke(gear);
                if (!string.IsNullOrEmpty(assetString) && (matcher == null || matcher.Invoke(gear?.Itemstack?.Item?.Code.Path)))
                {
                    availableWeapons.Add(gear);
                }
            }

            if (availableWeapons.Count < 1) { return; }

            // pick random weapon
            var chosenSlot = availableWeapons[World.Rand.Next(0, availableWeapons.Count)];
            var item = Api.World.GetItem(new AssetLocation(assetStringFromSlot.Invoke(chosenSlot)));
            if (item != null)
            {
                var dummySlot = new DummySlot(new ItemStack(item));
                if (dummySlot.TryPutInto(World, RightHandItemSlot) > 0)
                {
                    var chosenCode = chosenSlot.Itemstack.Item.Code;
                    RightHandItemSlot.Itemstack.Attributes.SetString("drawnFromGearType", String.Format("{0}:{1}", chosenCode.Domain, chosenCode.Path));
                    chosenSlot.TakeOutWhole();
                }
            }
        }

        public void UndrawWeaponIfOutOfCombat()
        {
            var taskManager = GetBehavior<EntityBehaviorTaskAI>().TaskManager;
            if (taskManager.GetTask<AiTaskVillagerMeleeAttack>()?.TargetEntity?.Alive != true
                && taskManager.GetTask<AiTaskVillagerSeekEntity>()?.TargetEntity?.Alive != true
                && taskManager.GetTask<AiTaskVillagerRangedAttack>()?.TargetEntity?.Alive != true)
            {
                if (RightHandItemSlot != null && !RightHandItemSlot.Empty && RightHandItemSlot.Itemstack.Attributes.HasAttribute("drawnFromGearType"))
                {
                    var dummySlot = new DummySlot(new ItemStack(Api.World.GetItem(new AssetLocation(RightHandItemSlot.Itemstack.Attributes.GetString("drawnFromGearType")))));
                    var chosenSlot = gearInv.GetBestSuitedSlot(dummySlot, new ItemStackMoveOperation(World, EnumMouseButton.Left, EnumModifierKey.CTRL, EnumMergePriority.AutoMerge))?.slot;
                    if (dummySlot.TryPutInto(World, chosenSlot) > 0)
                    {
                        RightHandItemSlot.TakeOutWhole();
                    }
                }
            }
        }

        public void DropInventoryOnGround()
        {
            for (int i = gearInv.Count - 1; i >= 0; i--)
            {
                if (gearInv[i].Empty) { continue; }

                Api.World.SpawnItemEntity(gearInv[i].TakeOutWhole(), ServerPos.XYZ);
                gearInv.MarkSlotDirty(i);
            }
        }

        private ITreeAttribute getInventoryTree()
        {
            if (!WatchedAttributes.HasAttribute("villagerInventory"))
            {
                ITreeAttribute tree = new TreeAttribute();
                gearInv.ToTreeAttributes(tree);
                WatchedAttributes.SetAttribute("villagerInventory", tree);
            }
            return WatchedAttributes.GetTreeAttribute("villagerInventory");
        }

        private void gearInvSlotModified(int slotId)
        {
            ITreeAttribute tree = new TreeAttribute();
            gearInv.ToTreeAttributes(tree);
            WatchedAttributes.MarkPathDirty("villagerInventory");

            if (Api is ICoreServerAPI sapi)
            {
                sapi.Network.BroadcastEntityPacket(EntityId, 1235, SerializerUtil.ToBytes((w) => tree.ToBytes(w)));
            }
        }
    }
}