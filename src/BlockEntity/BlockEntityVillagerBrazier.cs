using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class BlockEntityVillagerBrazier : BlockEntity, IPointOfInterest
    {
        public Vec3d Position => Pos.ToVec3d();

        public string Type => "freetime";
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api is ICoreServerAPI sapi) { sapi.ModLoader.GetModSystem<POIRegistry>().AddPOI(this); }
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            if (Api is ICoreServerAPI sapi) { sapi.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this); }
        }
    }
}