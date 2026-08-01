using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.StatusIcon;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System;
using System.Numerics;

namespace Content.Client.Access;

public sealed class IdCardSystem : SharedIdCardSystem
{
	[Dependency] private readonly SpriteSystem _sprite = default!;
	[Dependency] private readonly IPrototypeManager _prototype = default!;

	private static readonly ResPath JobIconRsiPath = new("Interface/Misc/job_icons.rsi");
	private static readonly Vector2 JobIconOffset = new(-0.09375f, 0.0625f);

	protected override void OnIdCardUpdated(Entity<IdCardComponent> ent)
	{
		SyncJobIconLayer(ent.Owner, ent.Comp);
	}

	private void SyncJobIconLayer(EntityUid uid, IdCardComponent id)
	{
		if (!TryComp<SpriteComponent>(uid, out var sprite))
			return;

		var iconState = ResolveIconState(id.JobIcon);
		if (string.IsNullOrEmpty(iconState))
			return;

		var layerIndex = FindJobIconLayer((uid, sprite));
		if (layerIndex == null)
		{
			layerIndex = _sprite.AddRsiLayer((uid, sprite), new RSI.StateId(iconState), JobIconRsiPath);
			if (layerIndex < 0)
				return;

			_sprite.LayerSetOffset((uid, sprite), layerIndex.Value, JobIconOffset);
		}
		else
		{
			_sprite.LayerSetRsi((uid, sprite), layerIndex.Value, JobIconRsiPath, new RSI.StateId(iconState));
		}
	}

	private int? FindJobIconLayer(Entity<SpriteComponent> sprite)
	{
		var idx = 0;
		foreach (var layer in sprite.Comp.AllLayers)
		{
			var path = layer.ActualRsi?.Path.CanonPath;
			if (path != null && path.EndsWith("/Textures/Interface/Misc/job_icons.rsi", StringComparison.OrdinalIgnoreCase))
				return idx;

			idx++;
		}

		return null;
	}

	private string? ResolveIconState(ProtoId<JobIconPrototype> icon)
	{
		if (!_prototype.TryIndex(icon, out var iconProto))
			return null;

		if (iconProto.Icon is not SpriteSpecifier.Rsi rsiIcon)
			return null;

		return rsiIcon.RsiState;
	}
}
