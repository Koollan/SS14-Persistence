using Content.Shared.CrewAssignments.Prototypes;
using Content.Shared.CrewAssignments.Systems;
using Content.Shared.MessageBoard.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CrewMetaRecords;

[RegisterComponent]
public sealed partial class CrewMetaRecordsComponent : Component
{
    [DataField]
    public string SectorStatus = "";
    [DataField]
    public int SectorChaos = 0;
    [DataField]
    public int NextObjectiveID = 1;
    [DataField]
    public int NextCodexID = 1;
    [DataField]
    public int NextMessageBoardEntryID = 1;
    [DataField]
    public List<WorldObjectivesEntry> CurrentObjectives { get; set; } = new();
    [DataField]
    public List<WorldObjectivesEntry> CompletedObjectives { get; set; } = new();
    [DataField]
    public List<CodexEntry> CodexEntries { get; set; } = new();

    [DataField]
    public List<MessageBoardEntry> MessageBoardEntries { get; set; } = new();
    [DataField]
    public Dictionary<string, CrewMetaRecord> CrewMetaRecords { get; set; } = new();
    [DataField]
    public Dictionary<int, EntityUid> Stations { get; set; } = new();
    public bool TryGetRecord(int legalID, out CrewMetaRecord? record)
    {
        foreach (var currRecord in CrewMetaRecords.Values)
        {
            if (currRecord.LegalID == legalID)
            {
                record = currRecord;
                return true;
            }
        }

        record = null;
        return false;
    }

    public bool TryGetRecord(string name, out CrewMetaRecord? record)
    {
        foreach (var currRecord in CrewMetaRecords.Values)
        {
            if (currRecord.Name == name || currRecord.RealName == name || currRecord.CustomName == name)
            {
                record = currRecord;
                return true;
            }
        }

        record = null;
        return false;
    }

    public bool CreateRecord(int legalID, string realName, out CrewMetaRecord? record)
    {
        if (TryGetRecord(legalID, out record))
            return false;

        record = new CrewMetaRecord(legalID, realName);

        var key = legalID.ToString();
        if (CrewMetaRecords.ContainsKey(key))
            key = $"{key}:{realName}";

        CrewMetaRecords[key] = record;
        return true;
    }

    public bool TryEnsureRecord(int legalID, string realName, out CrewMetaRecord? record, EntityManager? entityManager = null)
    {
        if (TryGetRecord(legalID, out record))
            return true;

        if (TryGetRecord(realName, out record) && record != null)
        {
            var changed = false;
            if (record.LegalID <= 0)
            {
                record.LegalID = legalID;
                changed = true;
            }

            if (record.RealName != realName)
            {
                record.RealName = realName;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(record.CustomName))
            {
                record.CustomName = realName;
                changed = true;
            }

            if (changed && entityManager != null)
                entityManager.Dirty(Owner, this);

            return true;
        }

        CreateRecord(legalID, realName, out record);
        if (entityManager != null) entityManager.Dirty(Owner, this);
        return true;
    }

    public bool TryEnsureRecord(string name, out CrewMetaRecord? record, EntityManager? entityManager = null)
    {
        if (TryGetRecord(name, out record))
            return true;

        record = null;
        return false;
    }
}


[DataDefinition]
[Serializable]
[Virtual]
public partial class CrewMetaRecord
{
    [DataField("_name")]
    public string Name = "Unnamed Crew Meta Record";
    [DataField]
    public int LegalID;
    [DataField]
    public string RealName = "Unnamed Crew Meta Record";
    [DataField]
    public string CustomName = "Unnamed Crew Meta Record";
    [DataField]
    public DateTime LatestIDTime;
    [DataField]
    public ProtoId<NetworkLevelPrototype> Level = "NetworkLevel1";

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextMessageBoardEntry = TimeSpan.Zero;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextMessageBoardComment = TimeSpan.Zero;

    public CrewMetaRecord(int legalID, string realName)
    {
        LegalID = legalID;
        Name = realName;
        RealName = realName;
        CustomName = realName;
    }

    public CrewMetaRecord(string name)
    {
        Name = name;
        RealName = name;
        CustomName = name;
    }
}
