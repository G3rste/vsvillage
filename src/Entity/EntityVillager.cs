using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using System.IO;
using System;
using System.Collections.Generic;
using Vintagestory.API.Util;
using Vintagestory.API.Client;

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
                talkUtil?.SetModifiers(Personalities[value].TalkSpeedModifier, Personalities[value].PitchModifier, Personalities[value].VolumneModifier);
            }
        }

        public EntityVillager()
        {
            AnimManager = new TraderAnimationManager();
        }
        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            if (gearInv == null) gearInv = new InventoryVillagerGear(Code.Path, "villagerInv-" + EntityId, api);
            else gearInv.Api = api;
            gearInv.LateInitialize(gearInv.InventoryID, api);
            var slots = new ItemSlot[gearInv.Count];
            for (int i = 0; i < gearInv.Count; i++)
            {
                slots[i] = gearInv[i];
            }
            AllowDespawn = false;
            if (!WatchedAttributes.HasAttribute("personality"))
            {
                Personality = EntityTrader.Personalities.GetKeyAtIndex(World.Rand.Next(EntityTrader.Personalities.Count));
            }
            (AnimManager as TraderAnimationManager).Personality = Personality;
            if (api.Side == EnumAppSide.Server) { api.World.RegisterCallback(dt => GetBehavior<EntityBehaviorTaskAI>().TaskManager.StopTask(typeof(AiTaskVillagerSleep)), 10000); }
            else { talkUtil = new EntityTalkUtil(api as ICoreClientAPI, this); }
            this.Personality = this.Personality; // to update the talkutil
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

        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);

            if (gearInv == null) { gearInv = new InventoryVillagerGear(Code.Path, "villagerInv-" + EntityId, null); }
            gearInv.FromTreeAttributes(getInventoryTree());
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            gearInv.ToTreeAttributes(getInventoryTree());

            base.ToBytes(writer, forClient);
        }

        public override void OnHurt(DamageSource dmgSource, float damage)
        {
            base.OnHurt(dmgSource, damage);
            DrawWeapon();
        }
        public void DrawWeapon()
        {
            var availableWeapons = new List<ItemSlot>();
            System.Func<ItemSlot, string> assetStringFromSlot = slot => (slot?.Itemstack?.Item as ItemVillagerGear)?.weaponAssetLocation;

            // get all slots containing weapons
            foreach (var gear in gearInv)
            {
                string weaponAssetLocation = (gear?.Itemstack?.Item as ItemVillagerGear)?.weaponAssetLocation;
                var assetString = weaponAssetLocation;
                if (!String.IsNullOrEmpty(assetStringFromSlot.Invoke(gear)))
                {
                    availableWeapons.Add(gear);
                }
            }

            if (availableWeapons.Count < 1) { return; }

            // pick random weapon
            var chosenSlot = availableWeapons[World.Rand.Next(0, availableWeapons.Count)];
            var dummySlot = new DummySlot(new ItemStack(Api.World.GetItem(new AssetLocation(assetStringFromSlot.Invoke(chosenSlot)))));
            if (dummySlot.TryPutInto(World, RightHandItemSlot) > 0)
            {
                chosenSlot.TakeOutWhole();
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
    }
}