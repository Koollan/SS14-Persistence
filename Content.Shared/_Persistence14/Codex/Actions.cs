using Content.Shared.Actions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._Persistence14.Codex;

public sealed partial class SenseAnomalyEvent : InstantActionEvent { };

public sealed partial class ArtifactRerollEvent : InstantActionEvent { };
public sealed partial class VoidVisionsEvent : InstantActionEvent { };

public sealed partial class PersistenceGuideEvent : InstantActionEvent { };

public sealed partial class MiracleReviveEvent : EntityTargetActionEvent;
