using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using System.IO;

namespace VsVillage
{
    public class EntityVillager : EntityAgent
    {
        protected InventoryBase gearInv;
        public override IInventory GearInventory => gearInv;

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