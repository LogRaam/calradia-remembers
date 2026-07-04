// Code written by Gabriel Mailhot, 03/07/2026.
// Grudges pillar, pure foundation: the resolution path a grudge offers, per the ROADMAP's
// "cause, magnitude, resolution condition [amends/gift/deed/time], rigidity = f(regard)" model.

namespace CalradiaRemembers.Logic.Grudges
{
    /// <summary>
    ///   How a <see cref="Grudge" /> can be mended. ORDINALS ARE FROZEN: persisted alongside
    ///   <see cref="GrudgeSource" /> inside every saved <see cref="Grudge" /> record. Append-only.
    /// </summary>
    public enum GrudgeResolutionKind
    {
        /// <summary>A sincere apology or conversational making-up suffices.</summary>
        Amends = 0,

        /// <summary>A material gift or payment closes it.</summary>
        Gift = 1,

        /// <summary>Only a verified deed (a real action in the world) closes it.</summary>
        Deed = 2,

        /// <summary>Nothing the player does closes it faster; only time softens it.</summary>
        Time = 3,

        /// <summary>A vendetta: no path exists. Always paired with <see cref="Grudge.Rigidity" /> == 100.</summary>
        Unforgivable = 4
    }
}
