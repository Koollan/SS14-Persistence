using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.CrewRecords.Systems;
using Content.Server.Kitchen.Components;
using Content.Server.Kitchen.EntitySystems;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewRecords.Components;
using Content.Shared.Database;
using Content.Shared.Kitchen;
using Content.Shared.PDA;
using Content.Shared.Station.Components;
using Content.Shared.Popups;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server.Access.Systems;

public sealed class IdCardSystem : SharedIdCardSystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly MicrowaveSystem _microwave = default!;
    [Dependency] private readonly CrewMetaRecordsSystem _crewMeta = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedJobStatusSystem _jobStatus = default!;

    private static readonly ProtoId<JobIconPrototype> OffDutyIcon = "JobIconNoId";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IdCardComponent, BeingMicrowavedEvent>(OnMicrowaved);
        SubscribeLocalEvent<IdCardComponent, ComponentStartup>(OnCompInit);
    }

    private void OnCompInit(EntityUid uid, IdCardComponent id, ComponentStartup args)
    {
        if (id.LegalID <= 0 && _crewMeta.MetaRecords != null && !string.IsNullOrWhiteSpace(id.FullName))
        {
            if (_crewMeta.MetaRecords.TryGetRecord(id.FullName, out var legalRecord) && legalRecord != null && legalRecord.LegalID > 0)
            {
                id.LegalID = legalRecord.LegalID;
            }
        }

        if (id.CreatedTime == null)
        {
            id.CreatedTime = DateTime.Now;
        }
        else
        {
            if (_crewMeta.MetaRecords != null && id.FullName != null)
            {
                if (_crewMeta.MetaRecords.TryGetRecord(id.FullName, out var record))
                {
                    if (record != null && id.CreatedTime < record.LatestIDTime)
                    {
                        QueueDel(uid);
                    }
                }
            }
        }
        if (id.FullName != "*Expired*" && id.FullName != null && id.FullName != "")
        {
            RebuildJob(uid, id);
            UpdateEntityName(uid, id);
        }

    }

    private void OnMicrowaved(EntityUid uid, IdCardComponent component, BeingMicrowavedEvent args)
    {
        if (!component.CanMicrowave || !TryComp<MicrowaveComponent>(args.Microwave, out var micro) || micro.Broken)
            return;

        if (TryComp<AccessComponent>(uid, out var access))
        {
            float randomPick = _random.NextFloat();

            // if really unlucky, burn card
            if (randomPick <= 0.15f)
            {
                TryComp(uid, out TransformComponent? transformComponent);
                if (transformComponent != null)
                {
                    _popupSystem.PopupCoordinates(Loc.GetString("id-card-component-microwave-burnt", ("id", uid)),
                     transformComponent.Coordinates, PopupType.Medium);
                    Spawn("FoodBadRecipe",
                        transformComponent.Coordinates);
                }
                _adminLogger.Add(LogType.Action, LogImpact.Medium,
                    $"{ToPrettyString(args.Microwave)} burnt {ToPrettyString(uid):entity}");
                QueueDel(uid);
                return;
            }

            //Explode if the microwave can't handle it
            if (!micro.CanMicrowaveIdsSafely)
            {
                _microwave.Explode((args.Microwave, micro));
                return;
            }

            // If they're unlucky, brick their ID
            if (randomPick <= 0.25f)
            {
                _popupSystem.PopupEntity(Loc.GetString("id-card-component-microwave-bricked", ("id", uid)), uid);

                access.Tags.Clear();
                Dirty(uid, access);

                _adminLogger.Add(LogType.Action, LogImpact.Medium,
                    $"{ToPrettyString(args.Microwave)} cleared access on {ToPrettyString(uid):entity}");
            }
            else
            {
                _popupSystem.PopupEntity(Loc.GetString("id-card-component-microwave-safe", ("id", uid)), uid, PopupType.Medium);
            }

            // Give them a wonderful new access to compensate for everything
            var ids = _prototypeManager.EnumeratePrototypes<AccessLevelPrototype>().Where(x => x.CanAddToIdCard).ToArray();

            if (ids.Length == 0)
                return;

            var random = _random.Pick(ids);

            access.Tags.Add(random.ID);
            Dirty(uid, access);

            _adminLogger.Add(LogType.Action, LogImpact.High,
                    $"{ToPrettyString(args.Microwave)} added {random.ID} access to {ToPrettyString(uid):entity}");

        }
    }

    public void ExpireAllIds(string name)
    {
        var query = EntityQueryEnumerator<IdCardComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.FullName == name)
            {
                if (comp.CreatedTime < DateTime.Now)
                {
                    QueueDel(uid);
                }
            }

        }
    }

    public void ExpireAllIds(int legalID)
    {
        if (legalID <= 0)
            return;

        var query = EntityQueryEnumerator<IdCardComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.LegalID != legalID)
                continue;

            if (comp.CreatedTime < DateTime.Now)
                QueueDel(uid);
        }
    }

    public void UpdateIDAssignment(string name, int station)
    {
        var query = EntityQueryEnumerator<IdCardComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.FullName == name)
            {
                comp.stationID = station;
                RebuildJob(uid, comp);
                UpdateEntityName(uid, comp);
            }

        }
    }

    public void UpdateIDAssignment(int legalID, int station)
    public void RefreshStationIds(int stationId)
    {
        var query = EntityQueryEnumerator<IdCardComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.LegalID != legalID)
                continue;

            comp.LegalID = legalID;
            comp.stationID = station;
            RebuildJob(uid, comp);
            UpdateEntityName(uid, comp);
            if (comp.stationID != stationId)
                continue;

            RebuildJob(uid, comp);
            UpdateEntityName(uid, comp);
            Dirty(uid, comp);
        }
    }

    public override void ExpireId(Entity<ExpireIdCardComponent> ent)
    {
        if (ent.Comp.Expired)
            return;

        base.ExpireId(ent);

        if (ent.Comp.ExpireMessage != null)
        {
            _chat.TrySendInGameICMessage(
                ent,
                Loc.GetString(ent.Comp.ExpireMessage),
                InGameICChatType.Speak,
                ChatTransmitRange.Normal,
                true);
        }
    }

    public void BuildID(EntityUid card, string name)
    {
        if (TryComp<IdCardComponent>(card, out var comp))
        {
            comp.FullName = name;
            RebuildJob(card, comp);
            UpdateEntityName(card, comp);
        }
    }

    public void BuildID(EntityUid card, int legalID, string name)
    {
        if (TryComp<IdCardComponent>(card, out var comp))
        {
            comp.LegalID = legalID;
            comp.FullName = name;
            RebuildJob(card, comp);
            UpdateEntityName(card, comp);
        }
    }

    public void RebuildJob(EntityUid card, IdCardComponent comp)
    {
        if (comp.FullName == null || comp.stationID == null)
        {
            comp.LocalizedJobTitle = "Off Duty";
            comp.JobIcon = OffDutyIcon;
            Dirty(card, comp);
            UpdateHolderJobStatus(card);
            return;
        }

        var station = _station.GetStationByID(comp.stationID.Value);
        if (station == null)
        {
            comp.LocalizedJobTitle = "Off Duty";
            comp.JobIcon = OffDutyIcon;
            Dirty(card, comp);
            UpdateHolderJobStatus(card);
            return;
        }

        bool found = false;

        if (TryComp<CrewRecordsComponent>(station, out var crewRecords))
        {
            if ((comp.LegalID > 0 && crewRecords.TryGetRecord(comp.LegalID, out var crewRecord) && crewRecord != null)
                || crewRecords.TryGetRecord(comp.FullName, out crewRecord) && crewRecord != null)
            {
                if (TryComp<CrewAssignmentsComponent>(station, out var crewAssignments))
                {
                    if (crewAssignments.TryGetAssignment(crewRecord.AssignmentID, out var crewAssignment) && crewAssignment != null)
                    {
                        comp.LocalizedJobTitle = crewAssignment.Name;
                        comp.JobIcon = crewAssignment.JobIcon;
                        // IDs show assignment as "[TAG] Job" so players can quickly identify
                        // which faction the card holder currently works for.
                        var factionTag = string.Empty;
                        if (TryComp<StationDataComponent>(station, out var stationData))
                            factionTag = stationData.GetResolvedFactionTag(MetaData(station.Value).EntityName);

                        comp.LocalizedJobTitle = string.IsNullOrEmpty(factionTag)
                            ? crewAssignment.Name
                            : $"[{factionTag}] {crewAssignment.Name}";
                        found = true;
                    }
                }
            }
        }
        if (!found)
        {
            comp.LocalizedJobTitle = "Off Duty";
            comp.JobIcon = OffDutyIcon;
        }

        Dirty(card, comp);
        UpdateHolderJobStatus(card);
    }

    public void RebuildAssignmentIds(EntityUid stationUid, int assignmentId)
    {
        var stationId = _station.GetStationID(stationUid);
        if (stationId == 0)
            return;

        var stationDataId = 0;
        if (TryComp<StationDataComponent>(stationUid, out var stationData))
            stationDataId = stationData.UID;

        if (!TryComp<CrewRecordsComponent>(stationUid, out var crewRecords))
            return;

        var rebuiltAny = false;
        var query = EntityQueryEnumerator<IdCardComponent>();
        while (query.MoveNext(out var uid, out var card))
        {
            // Cards can store either StationSystem station IDs or StationData UIDs depending on source.
            if (card.stationID is > 0
                && card.stationID != stationId
                && (stationDataId == 0 || card.stationID != stationDataId))
                continue;

            CrewRecord? record = null;
            if (card.LegalID > 0)
                crewRecords.TryGetRecord(card.LegalID, out record);

            if (record == null && !string.IsNullOrWhiteSpace(card.FullName))
                crewRecords.TryGetRecord(card.FullName, out record);

            if (record == null || record.AssignmentID != assignmentId)
                continue;

            if (card.stationID is null or <= 0)
                card.stationID = stationId;

            RebuildJob(uid, card);
            UpdateEntityName(uid, card);
            rebuiltAny = true;
        }

        if (rebuiltAny)
            RefreshAllJobStatuses();
    }

    private void RefreshAllJobStatuses()
    {
        var query = EntityQueryEnumerator<JobStatusComponent>();
        while (query.MoveNext(out var uid, out var status))
        {
            _jobStatus.UpdateStatus((uid, status));
        }
    }

    private void UpdateHolderJobStatus(EntityUid card)
    {
        var parent = Transform(card).ParentUid;
        if (parent == EntityUid.Invalid)
            return;

        if (HasComp<PdaComponent>(parent))
        {
            var holder = Transform(parent).ParentUid;
            if (holder != EntityUid.Invalid)
                _jobStatus.UpdateStatus((holder, null));
            return;
        }

        _jobStatus.UpdateStatus((parent, null));
    }
}
