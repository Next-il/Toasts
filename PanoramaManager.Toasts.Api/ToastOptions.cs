using CounterStrikeSharp.API.Core;

namespace PanoramaManager.Toasts;

/// <summary>
/// Everything a toast can be. Every field has a default, so the minimum is a player and a title.
///
/// <para>Values are plain strings rather than enums on purpose: the colours, placements, animations
/// and icons all resolve to CSS classes in the layout, and a server that ships its own layout can
/// add to that palette without this assembly changing. The constants below are the ones the bundled
/// layout defines - an unknown value is ignored by the client, which is the usual reason a toast
/// renders but looks plain.</para>
/// </summary>
public sealed record ToastOptions
{
    public required CCSPlayerController Player { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    /// <summary>Accent colour, or null for the neutral card. See <see cref="ToastColor"/>.</summary>
    public string? Color { get; init; }

    /// <summary>Seconds on screen before it leaves on its own. Zero or less means it stays until
    /// something clears it - use that sparingly, since the player cannot dismiss it.</summary>
    public float Seconds { get; init; } = 5f;

    /// <summary>See <see cref="ToastPlacement"/>. Defaults to the left edge, away from the
    /// crosshair, the radar and the killfeed.</summary>
    public string Placement { get; init; } = ToastPlacement.LeftCenter;

    /// <summary>Icon class, or none. See <see cref="ToastIcon"/>.</summary>
    public string? Icon { get; init; } = ToastIcon.None;

    /// <summary>How it arrives. See <see cref="ToastAnimation"/>.</summary>
    public string Enter { get; init; } = ToastAnimation.FromLeft;

    /// <summary>How it leaves.</summary>
    public string Exit { get; init; } = ToastAnimation.FromLeft;

    /// <summary>Draw a bar underneath that drains over <see cref="Seconds"/>, so the player can see
    /// how long is left. Ignored when the toast has no timeout.</summary>
    public bool Progress { get; init; } = true;
}

/// <summary>Colours the bundled layout defines.</summary>
public static class ToastColor
{
    public const string Teal   = "teal";
    public const string Danger = "danger";
    public const string Red    = "danger";
    public const string Warn   = "warn";
    public const string Good   = "good";
    public const string Info   = "info";
    public const string Purple = "purple";
}

/// <summary>
/// Placements the bundled layout defines. Each is an independent stack.
///
/// <para>The default is deliberately on the left: the right side carries the radar and the killfeed,
/// the centre carries the crosshair and the bomb timer, and a notification that covers any of those
/// is worse than no notification.</para>
/// </summary>
public static class ToastPlacement
{
    public const string TopLeft      = "tl";
    public const string TopCenter    = "tc";
    public const string TopRight     = "tr";
    public const string LeftCenter   = "ml";
    public const string RightCenter  = "mr";
    public const string BottomLeft   = "bl";
    public const string BottomCenter = "bc";
    public const string BottomRight  = "br";
}

/// <summary>
/// Animations the bundled layout defines.
///
/// <para>Named for the side the toast rests on when hidden, not for a direction of travel - that is
/// what makes one name work for both ends. <see cref="FromLeft"/> enters by sliding rightward from
/// off-left, and exits by sliding back leftward the way it came. Mixing them is fine:
/// <c>Enter = FromLeft, Exit = FromRight</c> slides in from the left and continues out to the
/// right.</para>
/// </summary>
public static class ToastAnimation
{
    public const string FromLeft   = "from-left";
    public const string FromRight  = "from-right";
    public const string FromTop    = "from-top";
    public const string FromBottom = "from-bottom";

    /// <summary>No movement, opacity only.</summary>
    public const string Fade       = "fade";
}

/// <summary>
/// Icons the bundled layout defines, drawn from the game's own artwork. Typed the same way as
/// <see cref="ToastColor"/>, so a server shipping its own layout can add to the set without this
/// assembly changing.
/// </summary>
public static class ToastIcon
{
    /// <summary>No artwork. The default - the icon slot collapses and the text takes the width.</summary>
    public const string? None = null;

    public const string Alert     = "alert";
    public const string Kill      = "kill";
    public const string Bullet    = "bullet";
    public const string Crosshair = "crosshair";
    public const string Defuser   = "defuser";
    public const string C4        = "c4";
    public const string Armor     = "armor";
    public const string Money     = "money";
}
