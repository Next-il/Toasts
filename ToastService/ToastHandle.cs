using CounterStrikeSharp.API.Core;

namespace HudMenu.Toasts.Service;

/// <summary>
/// The handle a caller gets back from <see cref="IToastApi.Show(ToastOptions)"/>.
///
/// <para>Deliberately safe to hold forever. A toast is transient and a caller should not have to
/// track whether it is still up, so <see cref="Clear"/> after it has expired does nothing rather
/// than throwing, and <see cref="IsVisible"/> is the honest answer to "is it still there".</para>
/// </summary>
internal sealed class ToastHandle : IToast
{
    /// <summary>Returned when a toast could not be shown at all - an invalid player, a bot. Callers
    /// get something they can safely call Clear on rather than a null they have to check.</summary>
    internal static readonly ToastHandle Dead = new();

    private readonly ToastManager? _manager;
    private readonly string        _placement;
    private readonly int           _index;

    private bool _gone;

    private ToastHandle()
    {
        _gone      = true;
        _placement = string.Empty;
    }

    internal ToastHandle(ToastManager manager, CCSPlayerController player, string placement, int index)
    {
        _manager   = manager;
        Player     = player;
        _placement = placement;
        _index     = index;
    }

    public CCSPlayerController? Player { get; }

    public bool IsVisible => !_gone && Player is { IsValid: true };

    public void Clear()
    {
        if (_gone || _manager is null || Player is not { IsValid: true } player)
            return;

        // Goes through Retire rather than straight to Release so an early dismissal still plays the
        // exit animation - a toast that vanishes mid-frame looks like a bug.
        _manager.Retire(player, _placement, _index, ToastAnimation.FromLeft);
    }

    internal void MarkGone() => _gone = true;
}
