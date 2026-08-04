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
    private const string LegacyPlaceholderName = "Unnamed Crew Meta Record";

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

    public void NormalizeLegacyRecords(EntityManager? entityManager = null)
    {
        var changed = false;
        foreach (var (key, record) in CrewMetaRecords)
        {
            if (BackfillLegacyRecord(key, record))
                changed = true;
        }

        if (!changed)
            return;

        if (entityManager != null)
            entityManager.Dirty(Owner, this);
        else
            Dirty();
    }

    public bool TryGetRecord(int legalID, out CrewMetaRecord? record)
    {
        foreach (var (key, currRecord) in CrewMetaRecords)
        {
            BackfillLegacyRecord(key, currRecord);

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
        if (string.IsNullOrWhiteSpace(name))
        {
            record = null;
            return false;
        }

        foreach (var (key, currRecord) in CrewMetaRecords)
        {
            BackfillLegacyRecord(key, currRecord);

            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                record = currRecord;
                return true;
            }

            if (string.Equals(currRecord.Name, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(currRecord.RealName, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(currRecord.CustomName, name, StringComparison.OrdinalIgnoreCase))
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
        NormalizeLegacyRecords(entityManager);

        if (TryGetRecord(legalID, out record))
            return true;

        if (TryGetRecord(realName, out record) && record != null)
        {
            // Prevent accidentally reusing a legacy name record that already belongs
            // to a different legal identity.
            if (record.LegalID > 0 && record.LegalID != legalID)
            {
                CreateRecord(legalID, realName, out record);
                if (entityManager != null)
                    entityManager.Dirty(Owner, this);
                return true;
            }

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
        NormalizeLegacyRecords(entityManager);

        if (TryGetRecord(name, out record))
            return true;

        if (string.IsNullOrWhiteSpace(name))
        {
            record = null;
            return false;
        }

        // Legacy compatibility: old systems still ensure by name.
        var key = name;
        if (CrewMetaRecords.ContainsKey(key))
            key = $"legacy:{name}:{Guid.NewGuid():N}";

        record = new CrewMetaRecord(name);
        CrewMetaRecords[key] = record;

        if (entityManager != null)
            entityManager.Dirty(Owner, this);

        return true;
    }

    private bool BackfillLegacyRecord(string key, CrewMetaRecord record)
    {
        var changed = false;

        var inferredName = InferNameFromKey(key);
        if (!string.IsNullOrWhiteSpace(inferredName))
        {
            if (string.IsNullOrWhiteSpace(record.Name) || record.Name == LegacyPlaceholderName)
            {
                record.Name = inferredName;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(record.RealName) || record.RealName == LegacyPlaceholderName)
            {
                record.RealName = inferredName;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(record.CustomName) || record.CustomName == LegacyPlaceholderName)
            {
                record.CustomName = inferredName;
                changed = true;
            }
        }

        if (record.LegalID <= 0 && TryInferLegalIdFromKey(key, out var inferredLegalId))
        {
            record.LegalID = inferredLegalId;
            changed = true;
        }

        return changed;
    }

    private static bool TryInferLegalIdFromKey(string key, out int legalId)
    {
        legalId = 0;

        var keyPart = key;
        var separator = key.IndexOf(':');
        if (separator > 0)
            keyPart = key[..separator];

        return int.TryParse(keyPart, out legalId) && legalId > 0;
    }

    private static string? InferNameFromKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        if (key.StartsWith("legacy:", StringComparison.OrdinalIgnoreCase))
        {
            var legacy = key["legacy:".Length..];
            var tailSeparator = legacy.LastIndexOf(':');
            if (tailSeparator > 0)
            {
                var tail = legacy[(tailSeparator + 1)..];
                if (Guid.TryParseExact(tail, "N", out _))
                    return legacy[..tailSeparator];
            }

            return legacy;
        }

        var separator = key.IndexOf(':');
        if (separator > 0 && int.TryParse(key[..separator], out _))
            return key[(separator + 1)..];

        if (int.TryParse(key, out _))
            return null;

        return key;
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
