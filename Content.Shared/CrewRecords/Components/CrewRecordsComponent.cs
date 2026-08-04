using Robust.Shared.GameStates;

namespace Content.Shared.CrewRecords.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class CrewRecordsComponent : Component
{
    private const string LegacyPlaceholderName = "Unnamed Crew Record";

    [DataField]
    [AutoNetworkedField]
    public Dictionary<string, CrewRecord> CrewRecords { get; set; } = new();

    public void NormalizeLegacyRecords(EntityManager? entityManager = null)
    {
        var changed = false;
        foreach (var (key, record) in CrewRecords)
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

    public bool TryGetRecord(int legalID, out CrewRecord? record)
    {
        foreach (var (key, currRecord) in CrewRecords)
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

    public bool TryGetRecord(string name, out CrewRecord? record)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            record = null;
            return false;
        }

        foreach (var (key, currRecord) in CrewRecords)
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

    public bool CreateRecord(int legalID, string realName, out CrewRecord? record)
    {
        if (TryGetRecord(legalID, out record))
            return false;

        record = new CrewRecord(legalID, realName);
        record.LastPaid = DateTime.Now;

        var key = legalID.ToString();
        if (CrewRecords.ContainsKey(key))
            key = $"{key}:{realName}";

        CrewRecords[key] = record;
        return true;
    }

    public bool TryEnsureRecord(int legalID, string realName, out CrewRecord? record, EntityManager? entityManager = null)
    {
        NormalizeLegacyRecords(entityManager);

        if (TryGetRecord(legalID, out record))
            return true;

        if (TryGetRecord(realName, out record) && record != null)
        {
            // If this legacy-name match already belongs to another legal identity,
            // create a dedicated record for the requested legal ID instead of hijacking it.
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

    public bool TryEnsureRecord(string name, out CrewRecord? record, EntityManager? entityManager = null)
    {
        NormalizeLegacyRecords(entityManager);

        if (TryGetRecord(name, out record))
            return true;

        if (string.IsNullOrWhiteSpace(name))
        {
            record = null;
            return false;
        }

        // Legacy compatibility: old flows ensure by display name only.
        // Create a provisional record that can be upgraded with LegalID later.
        var key = name;
        if (CrewRecords.ContainsKey(key))
            key = $"legacy:{name}:{Guid.NewGuid():N}";

        record = new CrewRecord(name)
        {
            LastPaid = DateTime.Now,
        };

        CrewRecords[key] = record;
        if (entityManager != null)
            entityManager.Dirty(Owner, this);

        return true;
    }

    private bool BackfillLegacyRecord(string key, CrewRecord record)
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

        if (record.LastPaid == DateTime.MinValue)
        {
            record.LastPaid = DateTime.Now;
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
public partial class CrewRecord
{

    [DataField("_name")]
    public string Name = "Unnamed Crew Record";
    [DataField]
    public int LegalID;
    [DataField]
    public string RealName = "Unnamed Crew Record";
    [DataField]
    public string CustomName = "Unnamed Crew Record";
    [DataField("_assignmentid")]
    public int AssignmentID = 0;
    [DataField("_spent")]
    public int Spent = 0;
    [DataField("_generalRecord")]
    public string GeneralRecord = "";
    [DataField("_criminalRecord")]
    public string CriminalRecord = "";
    [DataField("_medicalRecord")]
    public string MedicalRecord = "";
    [DataField]
    public DateTime LastPaid = DateTime.MinValue;
    public CrewRecord(int legalID, string realName)
    {
        LegalID = legalID;
        Name = realName;
        RealName = realName;
        CustomName = realName;
    }

    public CrewRecord(string name)
    {
        Name = name;
        RealName = name;
        CustomName = name;
    }
}
