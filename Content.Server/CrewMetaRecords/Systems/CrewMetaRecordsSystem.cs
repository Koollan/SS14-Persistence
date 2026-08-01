using Content.Server.Access.Systems;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared.CrewMetaRecords;
using Robust.Shared.Player;

namespace Content.Server.CrewRecords.Systems;

public sealed partial class CrewMetaRecordsSystem : SharedCrewMetaRecordsSystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;

    public bool CharacterNameExists(string name)
    {
        if (_gameTicker.RunLevel != GameRunLevel.InRound) return true;
        return MetaRecords != null && MetaRecords.TryGetRecord(name, out _);
    }

    public void JoinFirstTime(ICommonSession session)
    {
        _gameTicker!.MakeJoinGamePersistent(session);
    }

    public void DevalidateID(string name)
    {
        if (MetaRecords != null && MetaRecords.TryGetRecord(name, out var record) && record != null)
        {
            record.LatestIDTime = DateTime.Now;
            _idCard.ExpireAllIds(name);
        }

    }

    public void DevalidateID(int legalID)
    {
        if (legalID <= 0)
            return;

        if (MetaRecords != null && MetaRecords.TryGetRecord(legalID, out var record) && record != null)
            record.LatestIDTime = DateTime.Now;

        _idCard.ExpireAllIds(legalID);
    }

}
