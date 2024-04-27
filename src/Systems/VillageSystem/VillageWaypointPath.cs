using ProtoBuf;
using Vintagestory.API.MathTools;

namespace VsVillage
{
    [ProtoContract(ImplicitFields = ImplicitFields.None)]
    public class VillageWaypointPath
    {
        [ProtoMember(1)]
        public int Distance;
        [ProtoMember(2)]
        public BlockPos _NextWaypoint;
        public VillageWaypoint NextWaypoint;
    }
}