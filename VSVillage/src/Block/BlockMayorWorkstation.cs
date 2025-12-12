using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsVillage
{
    public class BlockMayorWorkstation : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var villageId = world.BlockAccessor.GetBlockEntity<BlockEntityVillagerWorkstation>(blockSel.Position).VillageId;
            bool isConnectedToVillage = !string.IsNullOrEmpty(villageId);
            if (!isConnectedToVillage && world.Api is ICoreClientAPI capi)
            {
                new ManagementGui(capi, blockSel.Position).TryOpen();
                return false;
            }
            if (isConnectedToVillage && world.Api is ICoreServerAPI sapi && byPlayer is IServerPlayer serverPlayer)
            {
                sapi.Network.GetChannel("villagemanagementnetwork").SendPacket(sapi.ModLoader.GetModSystem<VillageManager>().GetVillage(villageId), new IServerPlayer[] { serverPlayer });
            }
            return true;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return [
                new WorldInteraction(){
                    ActionLangCode = "vsvillage:manage-village",
                    MouseButton = EnumMouseButton.Right
                }
            ];
        }
    }
}