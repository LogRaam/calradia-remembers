// Code written by Gabriel Mailhot, 04/07/2026.
// Version-compat shim for Hero.OriginClan. A player on a Bannerlord build whose Hero type has no
// OriginClan property hit a hard MissingMethodException the instant the divorce eligibility path touched
// it (v1.21.0 crash report: get_OriginClan not found, every conversation with a divorcible NPC present).
// Binding the property by REFLECTION instead of at compile time makes its absence a caught null rather
// than a crash. Crucially, callers must NOT silently lose behaviour when it is absent: a name-based
// lookup would also return null on a BETA that merely RENAMED the property, and we will not let a future
// engine change quietly break divorce. So this exposes IsAvailable, and the divorce code falls back to a
// clan-leader heuristic (see DivorceEligibilityResolver.ResolveLeavingSpouse) to stay fully functional.

#region

using System.Reflection;
using TaleWorlds.CampaignSystem;
using CalradiaRemembers.Foundation;

#endregion

namespace CalradiaRemembers.Marriage
{
    /// <summary>Reflection-guarded access to <c>Hero.OriginClan</c>, with an availability flag so callers can degrade deliberately rather than silently.</summary>
    public static class HeroClanCompat
    {
        private static readonly PropertyInfo OriginClanProperty =
            typeof(Hero).GetProperty("OriginClan", BindingFlags.Public | BindingFlags.Instance);

        static HeroClanCompat()
        {
            if (OriginClanProperty == null)
                ModLog.Compat("the installed Bannerlord version does not support Hero.OriginClan, which this mod calls; " +
                              "divorce falls back to a clan-leader heuristic to decide the relocating spouse.");
        }

        /// <summary>True when the running game build exposes <c>Hero.OriginClan</c>. When false, callers should use their documented fallback rather than treating a null clan as fact.</summary>
        public static bool IsAvailable => OriginClanProperty != null;

        /// <summary>
        ///   <paramref name="hero" />'s OriginClan, or null when the hero is null OR the running game
        ///   version has no such property. Never throws MissingMethodException. A null here is ambiguous
        ///   (absent property vs a genuinely null clan), so gate on <see cref="IsAvailable" /> when the
        ///   distinction matters.
        /// </summary>
        public static Clan OriginClanOrNull(Hero hero)
            => hero == null || OriginClanProperty == null
                ? null
                : OriginClanProperty.GetValue(hero) as Clan;
    }
}
