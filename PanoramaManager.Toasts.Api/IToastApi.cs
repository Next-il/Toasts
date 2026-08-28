using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;

namespace PanoramaManager.Toasts;

/// <summary>
/// Server-wide toast notifications, shared between plugins.
///
/// <para>Get it through the capability rather than referencing the service plugin:</para>
///
/// <code>
/// private IToastApi? _toasts;
///
/// public override void OnAllPluginsLoaded(bool hotReload)
///     =&gt; _toasts = ToastApi.Capability.Get();
///
/// _toasts?.Show(new ToastOptions
/// {
///     Player      = player,
///     Title       = "You got muted!",
///     Description = "An admin gave you mute for the reason: voice chat abuse",
///     Color       = ToastColor.Danger,
/// });
/// </code>
///
/// <para><b>Resolve it in <c>OnAllPluginsLoaded</c>, not <c>Load</c>.</b> Plugin load order is not
/// defined, so in <c>Load</c> the service may not have registered yet and you get null. Null is also
/// what you get when the service is not installed at all, which is why every call site should treat
/// it as optional rather than assuming it is there.</para>
/// </summary>
public interface IToastApi
{
    /// <summary>
    /// Shows a toast and returns a handle to it.
    ///
    /// <para>The handle stays valid after the toast disappears - <see cref="IToast.Clear"/> on an
    /// already-gone toast is a no-op, so a caller never has to track whether it is still up.</para>
    /// </summary>
    IToast Show(ToastOptions options);

    /// <summary>Convenience for the common case: a title, a description and a colour.</summary>
    IToast Show(CCSPlayerController player, string title, string? description = null,
                string? color = null, float seconds = 5f);

    /// <summary>Removes every toast this player currently has, from every plugin.</summary>
    void ClearAll(CCSPlayerController player);

    /// <summary>Removes every toast on the server. For a map change or a reset.</summary>
    void ClearEveryone();

    /// <summary>How many toasts a single placement can stack before the oldest is pushed out.
    /// Fixed by the layout, so a caller can decide whether it is worth showing another.</summary>
    int StackDepth { get; }
}

/// <summary>A toast that has been shown. Returned by <see cref="IToastApi.Show(ToastOptions)"/>.</summary>
public interface IToast
{
    /// <summary>Removes it early. Safe to call more than once, and safe after it has expired.</summary>
    void Clear();

    /// <summary>False once it has expired or been cleared.</summary>
    bool IsVisible { get; }

    /// <summary>Who it was shown to.</summary>
    CCSPlayerController? Player { get; }
}

/// <summary>Where the capability lives. Both sides use this constant so a typo cannot split them.</summary>
public static class ToastApi
{
    public const string CapabilityName = "hudmenu:toasts";

    public static PluginCapability<IToastApi> Capability { get; } = new(CapabilityName);
}
