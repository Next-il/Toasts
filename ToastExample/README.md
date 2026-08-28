# ToastExample

A plugin that does nothing except show toasts, so you can see what the API feels like before wiring
it into something real.

Copy this project, rename it, delete the commands - it is meant as a starting point.

## Commands

All client-side, no permissions needed.

| | |
|---|---|
| `css_toast` | teal, left-centre, 5s |
| `css_toast [color] [placement] [seconds] [icon]` | every argument optional, left to right |
| `css_toast_mute` | a punishment notice - the shape a real admin plugin would send |
| `css_toast_clear` | dismiss your most recent one |
| `css_toast_clear all` | dismiss everything |

Worth actually trying:

```
css_toast                      the default
css_toast danger tr 8 alert    red, top-right, 8 seconds, alert icon
css_toast good bl 20           bottom-left, long enough to watch the bar drain
css_toast info mr 0            no timeout - stays until you clear it
```

Run `css_toast` five times quickly. The stack holds four; the fifth pushes the oldest out.

## What to look at in the code

**Where the API is resolved.** `OnAllPluginsLoaded`, not `Load`:

```csharp
public override void OnAllPluginsLoaded(bool hotReload)
    => _toasts = ToastApi.Capability.Get();
```

Plugin load order is undefined. In `Load` the service may not have registered its capability yet, and
you would get null permanently with nothing in the log to explain it.

**That `_toasts` is nullable everywhere.** Null means the service is not installed. That should never
stop your plugin loading - show the toast if you can, carry on if you cannot:

```csharp
_toasts?.Show(...);
```

**That the handle is kept.** `css_toast_clear` dismisses a toast that may already have expired:

```csharp
_last[player.Slot] = _toasts.Show(...);
...
toast.Clear();      // no-op if it is already gone
```

You never have to track whether a toast is still up.

**What this project references.** One thing, and not the service, not `HudMenu.Shared`, not the
layout. Note that it uses a `ProjectReference` because it lives *inside* this repo and the API source
is right there. **Your plugin will not** - it references the built DLL instead:

```xml
<Reference Include="HudMenu.Toasts.Api">
  <HintPath>lib\HudMenu.Toasts.Api.dll</HintPath>
  <Private>false</Private>
</Reference>
```

Either way `Private` is false, which is the part that matters: compile against it, do not copy it
next to your plugin. The runtime type has to come from `counterstrikesharp/shared/` or the capability
returns null. See the main README.

## If the commands say the service is not installed

`HudMenu.Toasts.Api.dll` is not in `counterstrikesharp/shared/HudMenu.Toasts.Api/`, or the service
plugin is not loaded. `css_hudmenu_diag` will tell you which.
