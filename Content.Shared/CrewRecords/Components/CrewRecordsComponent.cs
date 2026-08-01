using Robust.Shared.GameStates;

namespace Content.Shared.CrewRecords.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class CrewRecordsComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public Dictionary<string, CrewRecord> CrewRecords { get; set; } = new();

    public bool TryGetRecord(int legalID, out CrewRecord? record)
    {
        foreach (var currRecord in CrewRecords.Values)
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

    public bool TryGetRecord(string name, out CrewRecord? record)
    {
        foreach (var currRecord in CrewRecords.Values)
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

    public bool TryEnsureRecord(string name, out CrewRecord? record, EntityManager? entityManager = null)
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
