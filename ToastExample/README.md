# ToastExample

A plugin that only shows toasts, so you can see the API in game before wiring it into anything.
Copy it, rename it, delete the commands.

## Commands

Client-side, no permissions.

| | |
|---|---|
| `css_toast` | teal, left-centre, 5s |
| `css_toast [color] [placement] [seconds] [icon]` | every argument optional |
| `css_toast_mute` | a punishment notice |
| `css_toast_clear` | dismiss your last one |
| `css_toast_clear all` | dismiss everything |

```
css_toast danger tr 8 alert    red, top-right, 8s, alert icon
css_toast good bl 20           long enough to watch the bar drain
css_toast info mr 0            no timeout, stays until cleared
```

Run `css_toast` five times. The stack holds four; the fifth pushes the oldest out.

## In the code

- `_toasts` is resolved in `OnAllPluginsLoaded`, not `Load`.
- It is nullable. Null means the service is not installed.
- The handle is kept so `css_toast_clear` can dismiss a toast that may already be gone.

This project uses a `ProjectReference` because it sits inside the API's repo. Your plugin references
the built DLL instead - see the main README.

## If it says the service is not installed

`HudMenu.Toasts.Api.dll` is not in `counterstrikesharp/shared/HudMenu.Toasts.Api/`, or ToastService
is not loaded. `css_hudmenu_diag` will say which.
