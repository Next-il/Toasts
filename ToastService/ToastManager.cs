using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;

namespace PanoramaManager.Toasts.Service;

/// <summary>
/// The implementation behind <see cref="IToastApi"/>.
///
/// <para>Slots, not a queue. The layout has a fixed pool per placement, so showing a toast means
/// finding a free slot and filling it. An unused slot carries "hidden", which collapses it out of
/// layout, so when one in the middle expires the ones below close the gap on their own - no
/// re-render, no shuffling text between panels.</para>
/// </summary>
internal sealed class ToastManager : IToastApi, IDisposable
{
    private const string Layout = "panorama/layout/custom_game/toasts.vxml_c";
    private const int    Depth  = 4;

    /// <summary>Must match the placements the layout declares.</summary>
    private static readonly string[] Placements =
        [ "tl", "tc", "tr", "ml", "mr", "bl", "bc", "br" ];

    private static readonly string[] Colours =
        [ "teal", "danger", "warn", "good", "info", "purple" ];

    private static readonly string[] Anims =
        [ "from-left", "from-right", "from-top", "from-bottom", "fade" ];

    private static readonly string[] Icons =
        [ "alert", "kill", "bullet", "crosshair", "defuser", "c4", "armor", "money" ];

    /// <summary>Progress-bar durations the stylesheet defines. A request snaps to the nearest, so a
    /// 7-second toast drains over 8 - close enough, and far cheaper than a per-tick driver.</summary>
    private static readonly float[] BarDurations = [2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 30];

    /// <summary>How long the exit transition takes in the stylesheet. The slot is only freed after
    /// this, or the next toast would pop into a panel still animating out.</summary>
    private const float ExitSeconds = 0.25f;

    /// <summary>
    /// Gap between un-collapsing a toast and starting its entry animation.
    ///
    /// <para>A transition needs the client to have seen the panel at its resting offset before it is
    /// told to move. This was Server.NextFrame, which is one server frame - not necessarily a
    /// separate network update. When both changes reach the client in one snapshot it applies them
    /// in a single style pass, has nothing to transition from, and snaps to the end state: the toast
    /// appears without sliding.</para>
    ///
    /// <para>A few ticks of wall time is the difference. 60ms is ~4 ticks at 64 and ~8 at 128, and
    /// is not perceptible on a notification.</para>
    /// </summary>
    private const float EntrySeconds = 0.06f;

    private sealed class Slot
    {
        public ToastHandle? Toast;
        public Timer?       Entry;
        public Timer?       Expiry;
        public Timer?       Release;
    }

    private readonly BasePlugin _plugin;
    private readonly ILogger    _logger;
    private readonly PanelHandle _menu;

    /// <summary>Slot state per player, keyed "placement:index".</summary>
    private readonly Dictionary<int, Dictionary<string, Slot>> _slots = new();

    public ToastManager(BasePlugin plugin, ILogger logger)
    {
        _plugin = plugin;
        _logger = logger;

        _menu = Panorama.Spawn(Layout, new LayoutContract
        {
            RootPanelId  = "ToastRoot",

            // The two that matter for a notification: it must not take the mouse, and it must not
            // touch the HUD. A toast that pulls up a cursor or hides the crosshair is worse than no
            // toast at all.
            CaptureInput = false,
            HideHud      = HideHudFlags.None,

            RowCount     = 0,   // no rowN pool; every panel is addressed directly
        });

        _menu.OnEvent += OnMenuEvent;
    }

    public int StackDepth => Depth;

    public IToast Show(CCSPlayerController player, string title, string? description = null,
                       string? color = null, float seconds = 5f)
        => Show(new ToastOptions
        {
            Player      = player,
            Title       = title,
            Description = description,
            Color       = color,
            Seconds     = seconds,
        });

    public IToast Show(ToastOptions options)
    {
        var player = options.Player;

        if (player is not { IsValid: true, IsBot: false })
            return ToastHandle.Dead;

        var placement = Placements.Contains(options.Placement) ? options.Placement : ToastPlacement.LeftCenter;

        if (!_slots.TryGetValue(player.Slot, out var slots))
        {
            slots = new Dictionary<string, Slot>();
            _slots[player.Slot] = slots;

            // Opening un-hides the root for this viewer. Closed again when their last toast goes, so
            // a player with nothing to see has nothing drawn.
            _menu.Open(player);
        }

        var index = FreeIndex(slots, placement);
        var key   = $"{placement}:{index}";

        if (slots.TryGetValue(key, out var occupied) && occupied.Toast is { IsVisible: true })
        {
            // Every slot in this stack is taken, so the oldest gives way. Dropping the newest
            // instead would mean an important message silently never appearing.
            Release(player, placement, index, closeWhenEmpty: false);
        }

        var slot  = slots.TryGetValue(key, out var s) ? s : slots[key] = new Slot();
        var id    = $"t_{placement}_{index}";
        var toast = new ToastHandle(this, player, placement, index);

        slot.Toast = toast;

        Paint(player, id, options);

        // The classes that start the animations go in a later network update - see EntrySeconds.
        // Applied in the same update as the ones that set the resting state, the client has nothing
        // to transition FROM and both the entry and the progress bar snap to their end state.
        slot.Entry?.Kill();
        slot.Entry = _plugin.AddTimer(EntrySeconds, () =>
        {
            slot.Entry = null;

            if (player is not { IsValid: true } || slot.Toast != toast)
                return;

            _menu.SetClassFor(player, id, "show", true);

            if (options.Progress && options.Seconds > 0f)
                _menu.SetClassFor(player, $"{id}_bar", "run", true);
        }, TimerFlags.STOP_ON_MAPCHANGE);

        if (options.Seconds > 0f)
        {
            slot.Expiry = _plugin.AddTimer(options.Seconds,
                                           () => Retire(player, placement, index, options.Exit),
                                           TimerFlags.STOP_ON_MAPCHANGE);
        }

        return toast;
    }

    private void Paint(CCSPlayerController player, string id, ToastOptions options)
    {
        var description = options.Description ?? string.Empty;

        _menu.SetVariableFor(player, $"{id}_title", options.Title);
        _menu.SetVariableFor(player, $"{id}_desc", description);
        _menu.SetClassFor(player, $"{id}_desc", "empty", description.Length == 0);

        // Reset anything a previous occupant left behind. A slot is reused, and a stale colour or
        // icon on it is the kind of bug that only shows up once the server is busy.
        foreach (var colour in Colours)
            _menu.SetClassFor(player, id, $"color-{colour}", colour == options.Color);

        foreach (var anim in Anims)
            _menu.SetClassFor(player, id, $"anim-{anim}", anim == options.Enter);

        foreach (var icon in Icons)
            _menu.SetClassFor(player, $"{id}_icon", $"icon-{icon}", icon == options.Icon);

        _menu.SetClassFor(player, $"{id}_icon", "has-icon", options.Icon is { Length: > 0 });

        var progress = options.Progress && options.Seconds > 0f;

        _menu.SetClassFor(player, $"{id}_track", "has-progress", progress);

        // ORDER MATTERS. Dropping "run" while a dur-* class is still on the bar makes the width
        // transition BACKWARDS to 100% over that same duration - so a reused slot shows a bar
        // slowly refilling instead of a fresh one draining. Clear the duration first and the reset
        // is instant, because the base rule transitions over 0s.
        foreach (var duration in BarDurations)
            _menu.SetClassFor(player, $"{id}_bar", $"dur-{(int) duration}", false);

        _menu.SetClassFor(player, $"{id}_bar", "run", false);

        if (progress)
            _menu.SetClassFor(player, $"{id}_bar", $"dur-{(int) NearestDuration(options.Seconds)}", true);

        _menu.SetClassFor(player, id, "hidden", false);
    }

    /// <summary>Lowest free slot, or 0 when the stack is full so the oldest is reused.</summary>
    private static int FreeIndex(Dictionary<string, Slot> slots, string placement)
    {
        for (var i = 0; i < Depth; i++)
        {
            if (!slots.TryGetValue($"{placement}:{i}", out var slot) || slot.Toast is not { IsVisible: true })
                return i;
        }

        return 0;
    }

    private static float NearestDuration(float seconds)
        => BarDurations.OrderBy(d => Math.Abs(d - seconds)).First();

    /// <summary>Starts the exit animation, then frees the slot once it has played.</summary>
    internal void Retire(CCSPlayerController player, string placement, int index, string exit)
    {
        if (!_slots.TryGetValue(player.Slot, out var slots)
            || !slots.TryGetValue($"{placement}:{index}", out var slot)
            || slot.Toast is not { IsVisible: true } toast)
            return;

        toast.MarkGone();

        // A toast dismissed inside the entry window must not have "show" added after it has already
        // been told to leave - that would animate it back in and strand it.
        slot.Entry?.Kill();
        slot.Entry = null;
        slot.Expiry?.Kill();
        slot.Expiry = null;

        var id = $"t_{placement}_{index}";

        if (player is { IsValid: true })
        {
            // Swap the direction before removing "show" so it can leave differently than it arrived.
            foreach (var anim in Anims)
                _menu.SetClassFor(player, id, $"anim-{anim}", anim == exit);

            _menu.SetClassFor(player, id, "show", false);
        }

        slot.Release = _plugin.AddTimer(ExitSeconds,
                                        () => Release(player, placement, index, closeWhenEmpty: true),
                                        TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void Release(CCSPlayerController player, string placement, int index, bool closeWhenEmpty)
    {
        if (!_slots.TryGetValue(player.Slot, out var slots)
            || !slots.TryGetValue($"{placement}:{index}", out var slot))
            return;

        slot.Toast?.MarkGone();
        slot.Toast = null;
        slot.Entry?.Kill();
        slot.Expiry?.Kill();
        slot.Release?.Kill();
        slot.Entry = slot.Expiry = slot.Release = null;

        if (player is { IsValid: true })
        {
            var id = $"t_{placement}_{index}";

            _menu.SetClassFor(player, id, "show", false);
            _menu.SetClassFor(player, id, "hidden", true);

            // Same ordering rule as Paint: clear the duration before "run", or the bar animates
            // back to full instead of resetting.
            foreach (var duration in BarDurations)
                _menu.SetClassFor(player, $"{id}_bar", $"dur-{(int) duration}", false);

            _menu.SetClassFor(player, $"{id}_bar", "run", false);
        }

        // Nothing left to show: close the menu so the container stops being drawn at all.
        if (closeWhenEmpty && player is { IsValid: true }
            && slots.Values.All(v => v.Toast is not { IsVisible: true }))
        {
            _slots.Remove(player.Slot);
            _menu.Close(player);
        }
    }

    public void ClearAll(CCSPlayerController player)
    {
        if (player is not { IsValid: true } || !_slots.TryGetValue(player.Slot, out var slots))
            return;

        foreach (var key in slots.Keys.ToList())
        {
            var parts = key.Split(':');
            Release(player, parts[0], int.Parse(parts[1]), closeWhenEmpty: false);
        }

        _slots.Remove(player.Slot);
        _menu.Close(player);
    }

    public void ClearEveryone()
    {
        foreach (var slot in _slots.Keys.ToList())
        {
            if (Utilities.GetPlayerFromSlot(slot) is { IsValid: true } player)
                ClearAll(player);
            else
                _slots.Remove(slot);
        }
    }

    /// <summary>The library closes menus on a round restart and on disconnect. When that happens the
    /// panels are gone, so the bookkeeping has to go with them or the next toast picks a slot it
    /// thinks is occupied.</summary>
    private void OnMenuEvent(PanelEvent e)
    {
        if (e.Action != PanelAction.Close)
            return;

        if (_slots.Remove(e.Player.Slot, out var slots))
            Forget(slots);
    }

    private static void Forget(Dictionary<string, Slot> slots)
    {
        foreach (var slot in slots.Values)
        {
            slot.Toast?.MarkGone();
            slot.Expiry?.Kill();
            slot.Release?.Kill();
        }
    }

    public void Dispose()
    {
        foreach (var slots in _slots.Values)
            Forget(slots);

        _slots.Clear();
        _menu.Dispose();
    }
}
