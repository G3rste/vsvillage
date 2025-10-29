using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Essentials;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class VillagerWaypointsTraverser : WaypointsTraverser
    {
        public VillagerWaypointsTraverser(EntityAgent entity, EnumAICreatureType creatureType = EnumAICreatureType.Default) : base(entity, creatureType)
        {
        }
    }
}