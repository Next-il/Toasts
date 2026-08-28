# CS2 Toasts

Shared toast notifications for CounterStrikeSharp. One service on the server, any plugin can send
through it.

```csharp
_toasts?.Show(new ToastOptions
{
    Player      = player,
    Title       = "You got muted!",
    Description = "An admin gave you mute for the reason: voice chat abuse",
    Color       = ToastColor.Danger,
});
```

Panorama cards with a blurred background, colour accent, optional icon, a progress bar and
enter/exit animations. Eight placements, each stacks four.

```
HudMenu.Toasts.Api/   the contract other plugins reference
ToastService/         the implementation
ToastExample/         demo commands
workshop/             the Panorama layout
lib/                  HudMenu.Shared.dll + gamedata
```

## Install

**Plugins** - unzip a release over `game/csgo/addons/counterstrikesharp/`:

```
shared/HudMenu.Toasts.Api/     <- must be in shared/, not plugins/
plugins/ToastService/
plugins/ToastExample/
gamedata/hudmenu.json
```

**Layout** - copy `workshop/panorama/` into your addon's content folder and build it:

```
content/csgo_addons/<your_addon>/panorama/layout/custom_game/toasts.xml
content/csgo_addons/<your_addon>/panorama/styles/custom_game/toasts.css
content/csgo_addons/<your_addon>/panorama/styles/custom_game/hud_shared.css
```

The paths have to be exactly that - `custom_game` is the search path CS2 registers for custom HUD
layouts. If the built VPK has no `toasts.vxml_c`, add the panorama folders to the addon's
`VpkDirectories`; they are not included by default.

Mount the addon, then check:

```
css_hudmenu_diag
css_toast
```

## Using it from your plugin

Reference the API DLL. Take it from a release, or from `shared/HudMenu.Toasts.Api/` on a server
running it.

```xml
<ItemGroup>
  <Reference Include="HudMenu.Toasts.Api">
    <HintPath>lib\HudMenu.Toasts.Api.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

`Private` must be `false`, and you do not ship the DLL - the server has it in `shared/`.

```csharp
using HudMenu.Toasts;

public class MyPlugin : BasePlugin
{
    private IToastApi? _toasts;

    // Must be OnAllPluginsLoaded. In Load the service may not have registered yet.
    public override void OnAllPluginsLoaded(bool hotReload)
        => _toasts = ToastApi.Capability.Get();

    private void Punish(CCSPlayerController player, string reason)
    {
        _toasts?.Show(new ToastOptions
        {
            Player      = player,
            Title       = "You got muted!",
            Description = $"An admin gave you mute for the reason: {reason}",
            Color       = ToastColor.Danger,
            Seconds     = 8f,
        });
    }
}
```

`_toasts` is null when the service is not installed. Use `?.` and carry on.

Short form:

```csharp
_toasts.Show(player, "Saved", "Your loadout is set", ToastColor.Good);
```

Keep the handle to dismiss early. `Clear()` on an expired toast does nothing.

```csharp
var toast = _toasts.Show(...);

toast.Clear();
toast.IsVisible;
```

## Options

| Field | Default | |
|---|---|---|
| `Player` | required | |
| `Title` | required | |
| `Description` | null | blank collapses the line |
| `Color` | null | `Teal` `Danger` `Warn` `Good` `Info` `Purple` |
| `Seconds` | `5` | 0 or less stays until cleared |
| `Placement` | `LeftCenter` | `TopLeft` `TopCenter` `TopRight` `LeftCenter` `RightCenter` `BottomLeft` `BottomCenter` `BottomRight` |
| `Icon` | `None` | `Alert` `Kill` `Bullet` `Crosshair` `Defuser` `C4` `Armor` `Money` |
| `Enter` / `Exit` | `FromLeft` | `FromLeft` `FromRight` `FromTop` `FromBottom` `Fade` |
| `Progress` | `true` | draining bar |

Animations are named for the side the toast sits on when hidden, so `FromLeft` slides in from the
left and back out to the left. Mix them for in-one-side-out-the-other.

```csharp
void ClearAll(CCSPlayerController player);
void ClearEveryone();
int  StackDepth { get; }
```

## Limits

- Four per placement. A fifth pushes the oldest out.
- Progress durations snap to 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 30s.
- A round restart clears everything.
- Toasts are not clickable and never take the mouse. Use a menu for anything the player answers.

## Restyling

`workshop/panorama/styles/custom_game/toasts.css` is yours - colours, spacing, fonts, animation
distances.

Do not rename the ids in `toasts.xml` (`t_<placement>_<n>`, `_title`, `_desc`, `_icon`, `_bar`,
`_track`) or the classes the service toggles (`hidden`, `show`, `color-*`, `anim-*`, `icon-*`,
`dur-*`, `run`, `has-icon`, `has-progress`, `empty`). Those are the API between the plugin and the
layout.

Adding a colour or icon is one rule in the CSS plus passing its name.

Panorama is not web CSS. No `display`, flex, grid, `calc()`, `var()`, `rgb()` or `@media`. Layout is
`flow-children` with `fit-children | fill-parent-flow(w)`. `box-shadow` takes the colour first,
`background-size` takes `contains`, `visibility: collapse` is `display: none`, and `world-blur` is
what blurs the game behind a panel. Unknown properties are dropped silently.

## Building

```bash
dotnet build Toasts.slnx -c Release
```

CI builds every push to `main` and cuts a release automatically - `v1`, `v2`, `v3`. PRs build without releasing.

`lib/HudMenu.Shared.dll` is committed so a clone builds standalone. Replace it to pick up a newer
library.

## Credits

Engine signatures in `lib/hudmenu.json` derive from
[cs2-customhud](https://gitlab.com/cs2-server-plugins/cs2-customhud).

MIT.
