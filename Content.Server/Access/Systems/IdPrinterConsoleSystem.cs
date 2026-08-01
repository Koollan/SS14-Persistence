using Content.Server.Chat.Systems;
using Content.Server.CrewRecords.Systems;
using Content.Server.Hands.Systems;
using Content.Shared.Mind;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Coordinates;
using Content.Shared.StatusIcon;
using Content.Shared.Throwing;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using static Content.Shared.Access.Components.IdPrinterConsoleComponent;

namespace Content.Server.Access.Systems;

[UsedImplicitly]
public sealed class IdPrinterConsoleSystem : SharedIdPrinterConsoleSystem
{
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly CrewMetaRecordsSystem _crewMeta = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IdPrinterConsoleComponent, ComponentStartup>(UpdateUserInterface);
        SubscribeLocalEvent<IdPrinterConsoleComponent, PrintID>(Print);
    }

    private void Print(EntityUid uid, IdPrinterConsoleComponent component, PrintID args)
    {
        if (args.Actor is not { Valid: true } player)
            return;
        var name = Name(player);
        var hasLegalId = _mind.TryGetLegalID(player, out var legalID);
        var hasSourceId = _idCard.TryFindIdCard(player, out var sourceIdCard);

        if (_crewMeta.MetaRecords != null)
        {
            if (hasLegalId)
                _crewMeta.DevalidateID(legalID);
            else if (_crewMeta.MetaRecords.TryGetRecord(name, out _))
                _crewMeta.DevalidateID(name);
        }

        var iD = _entityManager.SpawnAtPosition("PassengerIDCard", player.ToCoordinates());
        if (hasLegalId)
        {
            _idCard.BuildID(iD, legalID, name);
        }
        else
        {
            _idCard.BuildID(iD, name);
        }

        if (hasSourceId)
        {
            _idCard.TryChangeJobTitle(iD, sourceIdCard.Comp.LocalizedJobTitle);
            if (_protoManager.Resolve(sourceIdCard.Comp.JobIcon, out JobIconPrototype? jobIcon))
                _idCard.TryChangeJobIcon(iD, jobIcon);
            _idCard.TryChangeJobDepartment(iD, sourceIdCard.Comp.JobDepartments);
        }

        if (!_hands.TryPickupAnyHand(player, iD))
            _transform.SetLocalRotation(iD, Angle.Zero); // Orient these to grid north instead of map north

    }
    private void UpdateUserInterface(EntityUid uid, IdPrinterConsoleComponent component, EntityEventArgs args)
    {
        IdPrinterConsoleBoundUserInterfaceState newState = new();
        _userInterface.SetUiState(uid, IdPrinterConsoleUiKey.Key, newState);
    }

}
