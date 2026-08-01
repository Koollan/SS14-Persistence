using Content.Shared.Emp;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Chat;
using Content.Shared.Radio.Components;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using System.Linq;

namespace Content.Shared.Radio.EntitySystems;

public abstract class SharedHeadsetSystem : EntitySystem
{
    [Dependency] private readonly SharedStationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeadsetComponent, InventoryRelayedEvent<GetDefaultRadioChannelEvent>>(OnGetDefault);
        SubscribeLocalEvent<WearingHeadsetComponent, ResolveCustomRadioChannelEvent>(OnResolveCustomChannel);
        SubscribeLocalEvent<HeadsetComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<HeadsetComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<HeadsetComponent, EmpPulseEvent>(OnEmpPulse);
    }

    private void OnResolveCustomChannel(EntityUid uid, WearingHeadsetComponent component, ResolveCustomRadioChannelEvent args)
    {
        if (!TryComp(component.Headset, out HeadsetComponent? headset))
            return;

        var key = char.ToLowerInvariant(args.Key);
        foreach (var stationId in headset.TransmitTo.Order())
        {
            var station = _station.GetStationByID(stationId);
            if (station == null || !TryComp<StationDataComponent>(station, out var stationData))
                continue;

            foreach (var (channelId, data) in stationData.RadioData)
            {
                if (!data.IsCustom || !data.Enabled)
                    continue;

                if (char.ToLowerInvariant(data.Hotkey) != key)
                    continue;

                args.Channel = channelId;
                args.EncryptionID = stationId;
                return;
            }
        }
    }

    private void OnGetDefault(EntityUid uid, HeadsetComponent component, InventoryRelayedEvent<GetDefaultRadioChannelEvent> args)
    {
        if (!component.Enabled || !component.IsEquipped)
        {
            // don't provide default channels from pocket slots.
            return;
        }

        if (TryComp(uid, out EncryptionKeyHolderComponent? keyHolder))
            args.Args.Channel ??= keyHolder.DefaultChannel;
    }

    protected virtual void OnGotEquipped(EntityUid uid, HeadsetComponent component, GotEquippedEvent args)
    {
        component.IsEquipped = args.SlotFlags.HasFlag(component.RequiredSlot);
        Dirty(uid, component);
    }

    protected virtual void OnGotUnequipped(EntityUid uid, HeadsetComponent component, GotUnequippedEvent args)
    {
        component.IsEquipped = false;
        Dirty(uid, component);
    }

    private void OnEmpPulse(Entity<HeadsetComponent> ent, ref EmpPulseEvent args)
    {
        if (ent.Comp.Enabled)
        {
            args.Affected = true;
            args.Disabled = true;
        }
    }
}
