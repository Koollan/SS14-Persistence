using Content.Shared.CrewManifest;
using Content.Shared.StatusIcon;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Client.CrewManifest.UI;

public sealed class CrewManifestListing : BoxContainer
{
    [Dependency] private readonly IEntitySystemManager _entitySystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    private readonly SpriteSystem _spriteSystem;

    public CrewManifestListing()
    {
        IoCManager.InjectDependencies(this);
        _spriteSystem = _entitySystem.GetEntitySystem<SpriteSystem>();
    }

    public void AddCrewManifestEntries(CrewManifestEntries entries)
    {
        var gridContainer = new GridContainer
        {
            HorizontalExpand = true,
            Columns = 2
        };

        AddChild(gridContainer);

        foreach (var entry in entries.Entries)
        {
            var name = new RichTextLabel
            {
                HorizontalExpand = true,
            };
            name.SetMessage(entry.Name);

            var titleContainer = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
            };

            var title = new RichTextLabel();
            title.SetMessage(entry.JobTitle);

            if (_prototypeManager.TryIndex<JobIconPrototype>(entry.JobIcon, out var jobIcon))
            {
                var icon = new TextureRect
                {
                    TextureScale = new Vector2(2, 2),
                    VerticalAlignment = VAlignment.Center,
                    Texture = _spriteSystem.Frame0(jobIcon.Icon),
                    Margin = new Thickness(0, 0, 4, 0),
                };

                titleContainer.AddChild(icon);
                titleContainer.AddChild(title);
            }
            else
            {
                titleContainer.AddChild(title);
            }

            gridContainer.AddChild(name);
            gridContainer.AddChild(titleContainer);
        }
    }
}
