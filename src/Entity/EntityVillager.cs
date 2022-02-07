using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using System.IO;
using System;

namespace VsVillage
{
    public class EntityVillager : EntityAgent
    {
        protected InventoryVillagerGear gearInv;
        public override IInventory GearInventory => gearInv;

        public override ItemSlot LeftHandItemSlot { get => gearInv.leftHandSlot; set => gearInv.leftHandSlot = value; }
        public override ItemSlot RightHandItemSlot { get => gearInv.rightHandSlot; set => gearInv.rightHandSlot = value; }
        public string Personality
        {
            get { return WatchedAttributes.GetString("personality", "formal"); }
            set { WatchedAttributes.SetString("personality", value); }
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
            Draw();
        }
        public void Draw()
        {
            foreach (var gear in gearInv)
            {
                var assetString = (gear?.Itemstack?.Item as ItemVillagerGear)?.toolAssetLocation;
                if (!String.IsNullOrEmpty(assetString))
                {
                    var slot = new DummySlot(new ItemStack(Api.World.GetItem(new AssetLocation(assetString))));
                    if (slot.TryPutInto(World, RightHandItemSlot) > 0)
                    {
                        gear.TakeOutWhole();
                        break;
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
    }
}