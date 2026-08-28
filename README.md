# CS2 Toasts

Shared toast notifications for CounterStrikeSharp. One service on the server, any number of plugins
sending notifications through it.

```csharp
_toasts?.Show(new ToastOptions
{
    Player      = player,
    Title       = "You got muted!",
    Description = "An admin gave you mute for the reason: voice chat abuse",
    Color       = ToastColor.Danger,
});
```

Real Panorama cards, not centre-text or chat spam: blurred background, colour accent, optional icon,
a progress bar that drains, and enter/exit animations. Eight placements, each an independent stack.

```
HudMenu.Toasts.Api/     the contract other plugins compile against
ToastService/           the implementation
ToastExample/           a worked example with /toast commands
workshop/               the Panorama layout
lib/                    HudMenu.Shared.dll + gamedata
```



## Install

Two halves: the plugins, and the Panorama layout they drive.

### Plugins

```
game/csgo/addons/counterstrikesharp/
  shared/HudMenu.Toasts.Api/HudMenu.Toasts.Api.dll     the contract   <- MUST be here
  plugins/ToastService/                                 the service
  plugins/ToastExample/                                 optional demo
  gamedata/hudmenu.json                                 engine signatures
```

`shared/` is not a convention you can ignore. CounterStrikeSharp loads assemblies from there into the
**default** load context, so every plugin resolves the same `IToastApi` type. Anywhere else and each
plugin gets its own copy, the types stop matching, and the capability returns null with nothing
useful in the log.

### Layout

Drop `workshop/panorama/` into your addon's content folder, keeping the paths, and build the addon as
usual:

```
content/csgo_addons/<your_addon>/panorama/layout/custom_game/toasts.xml
content/csgo_addons/<your_addon>/panorama/styles/custom_game/toasts.css
content/csgo_addons/<your_addon>/panorama/styles/custom_game/hud_shared.css
```

`custom_game` is the search path CS2 registers for custom HUD layouts, so the files have to live
exactly there. You want `toasts.vxml_c` and `toasts.vcss_c` in the built output - if the panorama
folders are missing from the VPK, add them to the addon's `VpkDirectories`, since they are not
included by default.

Players need the addon mounted, however you normally do that.

### Check it

```
css_hudmenu_diag                gamedata, natives, click channel, live menus
css_toast                       teal, left-centre, 5s
css_toast danger tr 8 alert     red, top-right, 8s, alert icon
css_toast_mute                  a punishment notice
css_toast_clear all
```



## Using it from your plugin

Reference **only** the API assembly. Not the service, not the layout.

Grab `HudMenu.Toasts.Api.dll` from a release (or from `counterstrikesharp/shared/HudMenu.Toasts.Api/`
on a server that already runs it), drop it somewhere in your plugin repo, and reference the DLL:

```xml
<ItemGroup>
  <Reference Include="HudMenu.Toasts.Api">
    <HintPath>lib\HudMenu.Toasts.Api.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

**`<Private>false</Private>` is the whole trick**, and it is the same arrangement VIPCore uses. It
means "compile against this, do not copy it next to my plugin". At runtime the type resolves from
`counterstrikesharp/shared/`, which CounterStrikeSharp loads into the **default** load context - so
your `IToastApi` and the service's are the same type and the capability cast works.

Leave it out and MSBuild copies the DLL into your plugin folder. CounterStrikeSharp then loads that
copy into your plugin's own context, your `IToastApi` becomes a *different* type from the service's,
and `Capability.Get()` returns null. Nothing is logged, and it looks exactly like the service is not
installed.

You do not ship that DLL with your plugin. The server already has it in `shared/`.

```csharp
using HudMenu.Toasts;

public class MyPlugin : BasePlugin
{
    private IToastApi? _toasts;

    // OnAllPluginsLoaded, NOT Load. Plugin load order is undefined; in Load the service may not
    // have registered yet and you would get null permanently.
    public override void OnAllPluginsLoaded(bool hotReload)
        => _toasts = ToastApi.Capability.Get();

    private void Punish(CCSPlayerController player, string reason)
    {
        // Null when the service is not installed. Never a reason to fail your own plugin.
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

The handle is safe to keep:

```csharp
var toast = _toasts.Show(...);

toast.Clear();       // no-op if it already expired
toast.IsVisible;     // false once gone
```

Short form for the common case:

```csharp
_toasts.Show(player, "Saved", "Your loadout is set", ToastColor.Good);
```



## Options


| Field            | Default      |                                                                                                       |
| ---------------- | ------------ | ----------------------------------------------------------------------------------------------------- |
| `Player`         | required     | who sees it                                                                                           |
| `Title`          | required     | headline                                                                                              |
| `Description`    | null         | body; blank collapses the line                                                                        |
| `Color`          | null         | `Teal` `Danger` `Warn` `Good` `Info` `Purple`                                                         |
| `Seconds`        | `5`          | 0 or less stays until cleared                                                                         |
| `Placement`      | `LeftCenter` | `TopLeft` `TopCenter` `TopRight` `LeftCenter` `RightCenter` `BottomLeft` `BottomCenter` `BottomRight` |
| `Icon`           | `None`       | `Alert` `Kill` `Bullet` `Crosshair` `Defuser` `C4` `Armor` `Money`                                    |
| `Enter` / `Exit` | `FromLeft`   | `FromLeft` `FromRight` `FromTop` `FromBottom` `Fade`                                                  |
| `Progress`       | `true`       | draining bar under the card                                                                           |


Animations are named for the side the toast **rests on**, not a direction of travel - that is what
lets one name work for both ends. `FromLeft` enters by sliding rightward from off-left and exits by
sliding back the way it came. Mixing is fine: `Enter = FromLeft, Exit = FromRight` slides in from the
left and continues out to the right.

The default placement is the left edge on purpose. The right side carries the radar and killfeed, the
centre carries the crosshair and bomb timer, and a notification over any of those is worse than none.

```csharp
void ClearAll(CCSPlayerController player);   // this player's toasts, from every plugin
void ClearEveryone();                        // map change, reset
int  StackDepth { get; }                     // 4
```



## What it will not do

**It never takes the mouse.** No cursor, no input capture, nothing clickable. A notification that
stops the player aiming is worse than no notification - which is also why toasts have no buttons. For
anything the player must answer, use a menu.

**It never touches the HUD.** No hidden crosshair, no hidden radar.

## Limits

**Four per placement.** The pool is fixed by the layout. A fifth pushes the oldest out - dropping the
newest would mean an important message silently never appearing.

**Progress durations snap to a set**: 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 30s. The bar is a CSS
transition - the server picks a duration class and toggles one more to start it, and the client
animates the drain. The alternative is a 10Hz driver rewriting a class per toast per player per tick.

**A round restart clears them**, because the layout entity is destroyed along with every other
non-player entity.

## Restyling

`workshop/panorama/styles/custom_game/toasts.css` is yours - colours, spacing, fonts, animation
distances, all of it, without touching the plugin.

What you cannot rename without also changing the service: the ids in `toasts.xml`
(`t_<placement>_<n>`, `_title`, `_desc`, `_icon`, `_bar`, `_track`) and the classes it toggles
(`hidden`, `show`, `color-*`, `anim-*`, `icon-*`, `dur-*`, `run`, `has-icon`, `has-progress`,
`empty`). Those are the API between the two halves.

Adding a colour is one rule in the CSS plus passing its name. Same for an icon.

If you are editing that CSS, the Panorama traps worth knowing up front:

- No `display`, no flex, no grid, no `calc()`, no `var()`, no `rgb()`, no `@media`
- Layout is `flow-children` plus `width`/`height` taking `fit-children | fill-parent-flow(w)`
- `box-shadow` takes the colour **first**
- `background-size: contains`, not the web's `contain`
- `visibility: collapse` is the equivalent of `display: none`
- `world-blur` blurs the game behind a panel. `background-blur` does not - it only blurs UI behind UI
- An unrecognised property is dropped silently, with nothing logged



## How it works, briefly

A CS2 client only renders a layout it already has. The server cannot create panels - it can write a
string into a dialog variable and toggle a CSS class. Everything here follows from that:

- **Stacking is a fixed pool.** Four slots per placement; a free one carries `hidden`, which
collapses it out of layout. When a toast in the middle expires the ones below close the gap on
their own, with no re-render.
- **The progress bar is a CSS transition.** The server cannot animate a width, so the bar starts
full, a `dur-*` class sets how long, and one more class starts the drain.
- **Colours and icons are classes.** A colour cannot travel over the wire, so the palette lives in
the stylesheet and the plugin names one.



## Building

```bash
dotnet build Toasts.slnx -c Release
```

CI does this on every push and publishes the result as an artifact, laid out exactly as it drops
into a server. Tagging `v*` attaches a zip to a GitHub release.

The workflow also fails the build if `HudMenu.Toasts.Api.dll` ends up next to a plugin instead of
only in `shared/` - two copies means two types, the capability cast fails at runtime, and nothing
useful is logged. Easiest bug in this project to ship by accident, so it is checked rather than
trusted.

```
.build/shared/HudMenu.Toasts.Api/   -> counterstrikesharp/shared/
.build/plugins/ToastService/        -> counterstrikesharp/plugins/
.build/plugins/ToastExample/        -> counterstrikesharp/plugins/
```

`lib/HudMenu.Shared.dll` is committed so a clone builds with nothing else present. Replace it to pick
up a newer library.

## Credits

Built on the `custom_hud_layout` entity. The engine signatures in `lib/hudmenu.json` derive from  
[cs2-customhud](https://gitlab.com/cs2-server-plugins/cs2-customhud), whose reverse engineering made  
this possible.