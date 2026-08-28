using System;
using System.Collections.Generic;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using HudMenu.Toasts;
using Microsoft.Extensions.Logging;

namespace ToastExample;

/// <summary>
/// A worked example of consuming the toast service from another plugin.
///
/// <para>Note what this project does NOT reference: the service, HudMenu.Shared, or the layout. It
/// compiles against <c>HudMenu.Toasts.Api.dll</c> alone and finds the implementation at runtime.
/// That is the point of the capability - one server-wide notification system, and a plugin that
/// loads whether or not it is installed.</para>
/// </summary>
public sealed class ToastExamplePlugin : BasePlugin
{
    public override string ModuleName        => "Toast Example";
    public override string ModuleVersion     => "1.0.0";
    public override string ModuleAuthor      => "HudMenu";
    public override string ModuleDescription => "Demonstrates the shared toast API.";

    private IToastApi? _toasts;

    /// <summary>The last toast each player was shown, so css_toast_clear has something to dismiss.
    /// Holding a handle is safe - it goes inert once the toast expires.</summary>
    private readonly Dictionary<int, IToast> _last = new();

    /// <summary>
    /// Resolve here, not in <c>Load</c>. Plugin load order is undefined, so in <c>Load</c> the
    /// service may not have registered its capability yet and you would get null forever.
    /// </summary>
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _toasts = ToastApi.Capability.Get();

        if (_toasts is null)
        {
            Logger.LogWarning("[ToastExample] toast service not installed - the commands will say so.");

            return;
        }

        Logger.LogInformation("[ToastExample] toast service found, {Depth} deep per placement.", _toasts.StackDepth);
    }

    [ConsoleCommand("css_toast", "Demo toast. Usage: css_toast [color] [placement] [seconds] [icon]")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnToast(CCSPlayerController? player, CommandInfo command)
    {
        if (player is not { IsValid: true })
            return;

        if (_toasts is null)
        {
            command.ReplyToCommand("Toast service is not installed on this server.");

            return;
        }

        var color     = command.ArgCount > 1 ? command.GetArg(1) : ToastColor.Teal;
        var placement = command.ArgCount > 2 ? command.GetArg(2) : ToastPlacement.LeftCenter;
        var seconds   = command.ArgCount > 3 && float.TryParse(command.GetArg(3), out var s) ? s : 5f;

        // No icon unless one is asked for - the slot collapses and the text takes the width.
        var icon      = command.ArgCount > 4 ? command.GetArg(4) : ToastIcon.None;

        _last[player.Slot] = _toasts.Show(new ToastOptions
        {
            Player      = player,
            Title       = $"Toast: {color}",
            Description = $"Placement {placement}, {seconds:0.#}s. Run it again to stack another.",
            Color       = color,
            Placement   = placement,
            Seconds     = seconds,
            Icon        = icon,
        });
    }

    [ConsoleCommand("css_toast_mute", "A punishment notice, as an admin plugin would send it.")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnToastMute(CCSPlayerController? player, CommandInfo command)
    {
        if (player is not { IsValid: true } || _toasts is null)
            return;

        // Danger colour, a little longer than the default because there is something to read.
        _toasts.Show(new ToastOptions
        {
            Player      = player,
            Title       = "You got muted!",
            Description = "An admin gave you mute for the reason: voice chat abuse",
            Color       = ToastColor.Danger,
            Seconds     = 8f,
            Icon        = ToastIcon.Alert,
        });
    }

    [ConsoleCommand("css_toast_clear", "Dismiss your last toast, or all of them with 'all'.")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnToastClear(CCSPlayerController? player, CommandInfo command)
    {
        if (player is not { IsValid: true } || _toasts is null)
            return;

        if (command.ArgCount > 1 && command.GetArg(1).Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            _toasts.ClearAll(player);

            return;
        }

        // Safe on a toast that has already expired - the handle goes inert rather than dangling.
        if (_last.TryGetValue(player.Slot, out var toast))
            toast.Clear();
    }
}
