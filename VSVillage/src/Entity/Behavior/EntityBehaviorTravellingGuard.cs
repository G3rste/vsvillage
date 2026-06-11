using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsVillage;

public class EntityBehaviorTravellingGuard : EntityBehavior
{
	private long _tickListenerId;

	public long TraderEntityId
	{
		get
		{
			return entity.WatchedAttributes.GetLong("tgTraderEntityId", 0L);
		}
		set
		{
			entity.WatchedAttributes.SetLong("tgTraderEntityId", value);
			entity.WatchedAttributes.MarkPathDirty("tgTraderEntityId");
		}
	}

	public BlockPos MarketStallPos
	{
		get
		{
			return entity.WatchedAttributes.GetBlockPos("tgMarketStallPos");
		}
		set
		{
			if (value != null)
			{
				entity.WatchedAttributes.SetBlockPos("tgMarketStallPos", value);
			}
			entity.WatchedAttributes.MarkPathDirty("tgMarketStallPos");
		}
	}

	public Entity TraderEntity => entity.World.GetEntityById(TraderEntityId);

	public bool IsTraderAtStall
	{
		get
		{
			Entity traderEntity = TraderEntity;
			return traderEntity != null && traderEntity.GetBehavior<EntityBehaviorTravellingTrader>()?.IsAtStall == true;
		}
	}

	public EntityBehaviorTravellingGuard(Entity entity)
		: base(entity)
	{
	}

	public override void Initialize(EntityProperties properties, JsonObject attributes)
	{
		base.Initialize(properties, attributes);
		if (entity.Api.Side == EnumAppSide.Server)
		{
			_tickListenerId = entity.World.RegisterGameTickListener(CheckDespawn, 10000);
		}
	}

	public override void OnEntityDespawn(EntityDespawnData despawn)
	{
		if (_tickListenerId != 0)
		{
			entity.World.UnregisterGameTickListener(_tickListenerId);
			_tickListenerId = 0;
		}
	}

	public override string PropertyName()
	{
		return "TravellingGuard";
	}

	private void CheckDespawn(float dt)
	{
		if (!entity.Alive || entity.Api.Side != EnumAppSide.Server)
			return;

		Entity trader = TraderEntity;
		if (trader == null || !trader.Alive)
		{
			Log("Trader gone - despawning guard.");
			DespawnSelf();
			return;
		}

		string villageId = trader.GetBehavior<EntityBehaviorTravellingTrader>()?.VillageId;
		if (!string.IsNullOrEmpty(villageId))
		{
			Village village = entity.Api.ModLoader.GetModSystem<VillageManager>()?.GetVillage(villageId);
			if (village != null)
			{
				double dist = entity.Pos.XYZ.DistanceTo(village.EffectiveCenter().ToVec3d());
				if (dist > village.Radius + 10.0)
				{
					Log($"Outside village area ({dist:F0}) - despawning guard.");
					DespawnSelf();
				}
			}
		}
	}

	private void DespawnSelf()
	{
		(entity.Api as ICoreServerAPI)?.World.DespawnEntity(entity, new EntityDespawnData
		{
			Reason = EnumDespawnReason.Removed
		});
	}

	private void Log(string msg)
	{
		entity.World.Logger.Debug($"[TravellingGuard:{entity.EntityId}] {msg}");
	}

	public override void GetInfoText(StringBuilder infotext)
	{
		base.GetInfoText(infotext);
		if (entity.Api is ICoreClientAPI capi && capi.Settings.Bool["showEntityDebugInfo"])
		{
			StringBuilder stringBuilder = infotext;
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(19, 1, stringBuilder);
			handler.AppendLiteral("[TG] Trader      : ");
			handler.AppendFormatted(TraderEntityId);
			stringBuilder2.AppendLine(ref handler);
			stringBuilder = infotext;
			StringBuilder stringBuilder3 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(19, 1, stringBuilder);
			handler.AppendLiteral("[TG] Stall       : ");
			handler.AppendFormatted(MarketStallPos?.ToString() ?? "unset");
			stringBuilder3.AppendLine(ref handler);
			stringBuilder = infotext;
			StringBuilder stringBuilder4 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder);
			handler.AppendLiteral("[TG] TraderAtStall: ");
			handler.AppendFormatted(IsTraderAtStall);
			stringBuilder4.AppendLine(ref handler);
		}
	}
}
