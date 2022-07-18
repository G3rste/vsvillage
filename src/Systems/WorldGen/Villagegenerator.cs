using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace VsVillage
{
    public class VillageGenerator : ModSystem
    {

        public override double ExecuteOrder() => 0.6;
        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.ChunkColumnGeneration(handler, EnumWorldGenPass.TerrainFeatures, "standard");
            api.Event.MapRegionGeneration(mapHandler, "standard");
        }

        private void mapHandler(IMapRegion mapRegion, int regionX, int regionZ)
        {
            throw new NotImplementedException();
        }

        private void handler(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams)
        {
            throw new NotImplementedException();
        }
    }
}