using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using Microsoft.Extensions.Logging;

namespace HudMenu.Toasts.Service;

/// <summary>
/// Provides <see cref="IToastApi"/> to every other plugin on the server.
///
/// <para>Install once; consumers reference nothing but <c>HudMenu.Toasts.Api.dll</c> and resolve the
/// capability at runtime. If this plugin is not installed they get null and carry on - a missing
/// notification system should never stop a plugin loading.</para>
/// </summary>
public sealed class ToastServicePlugin : BasePlugin
{
    public override string ModuleName        => "Toast Service";
    public override string ModuleVersion     => "1.0.0";
    public override string ModuleAuthor      => "HudMenu";
    public override string ModuleDescription => "Shared toast notifications for CounterStrikeSharp plugins.";

    private ToastManager? _manager;

    public override void Load(bool hotReload)
    {
        HudMenus.Init(this);

        _manager = new ToastManager(this, Logger);

        // Registered in Load so it is available as early as possible. Consumers still have to
        // resolve it in OnAllPluginsLoaded, because plugin load order is not defined and a consumer
        // loaded before this one would otherwise see nothing.
        Capabilities.RegisterPluginCapability(ToastApi.Capability, () => _manager);

        Logger.LogInformation(
            "[Toasts] ready - capability '{Name}', {Depth} deep per placement.",
            ToastApi.CapabilityName, _manager.StackDepth);

        if (!HudMenus.CanWritePerPlayerText)
        {
            Logger.LogWarning(
                "[Toasts] per-player text is unavailable on this build, so every viewer of a toast "
                + "sees the same text. Toasts will still appear.");
        }
    }

    public override void Unload(bool hotReload)
    {
        _manager?.Dispose();
        _manager = null;

        HudMenus.Shutdown();
    }
}
