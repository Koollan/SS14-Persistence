using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Events;

[Serializable, NetSerializable]
public sealed class StationModificationChangeAssignmentJobIcon : BoundUserInterfaceMessage
{
    public int AccessID;
    public ProtoId<JobIconPrototype> JobIcon;

    public StationModificationChangeAssignmentJobIcon(int id, ProtoId<JobIconPrototype> jobIcon)
    {
        AccessID = id;
        JobIcon = jobIcon;
    }
}