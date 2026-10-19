// Code written by Gabriel Mailhot, 29/08/2026.
// Equipment-awareness pillar: plain carrier for ONE equipped item's facts, fed to EquipmentNotabilityPolicy.
// Deliberately engine-agnostic. The mod reads TaleWorlds' EquipmentElement (item tier, its ItemModifier
// name, value, item type) and maps them onto these plain fields; the policy then decides, purely and
// testably, whether the piece is worth voicing to the model and how. Player report (a modder on TOR): NPCs
// react to the PLAYER's fine/shabby attire only by the model's own guesswork, and never to a companion's
// mastercraft blade or a gifted legendary weapon, because CR feeds no equipment to the prompt at all. This
// is the ground fact the awareness layer stands on.

#region

#endregion

namespace CalradiaRemembers.Logic.Items
{
    /// <summary>The broad kind of an equipped piece, enough to phrase it ("clad in", "bears", "rides").</summary>
    public enum GearKind
    {
        Weapon,
        Armor,
        Mount
    }

    /// <summary>
    ///   The quality band of a piece, derived by the host from the item's modifier (Masterwork/Legendary vs
    ///   Rusty/Tattered/Cracked...) and, for an unmarked piece, its raw value. Only bands other than
    ///   <see cref="Common" /> are voiced: a standout in either direction speaks up, the ordinary stays silent,
    ///   the same discipline as the appearance composer's "average is unvoiced".
    /// </summary>
    public enum GearQuality
    {
        /// <summary>Conspicuously bad: rusty, tattered, cracked, battered. The "girl in rags" case.</summary>
        Poor,

        /// <summary>Unremarkable. Never voiced.</summary>
        Common,

        /// <summary>Notably good (a fine piece, or an unmarked one whose sheer value stands out).</summary>
        Fine,

        /// <summary>A master's work.</summary>
        Masterwork,

        /// <summary>Legendary, the rarest tier of make.</summary>
        Legendary
    }

    /// <summary>
    ///   One equipped item, reduced to the facts <see cref="EquipmentNotabilityPolicy" /> needs. Immutable.
    /// </summary>
    public sealed class EquipmentPieceFacts
    {
        public EquipmentPieceFacts(GearKind kind, string kindNoun, GearQuality quality, int tier, int value)
        {
            Kind = kind;
            KindNoun = kindNoun;
            Quality = quality;
            Tier = tier;
            Value = value;
        }

        /// <summary>Broad kind, chooses the verb the piece is voiced with.</summary>
        public GearKind Kind { get; }

        /// <summary>
        ///   The short noun the piece is named by in prose: a weapon's class ("sword", "bow", "axe", or the
        ///   generic "weapon"); "armor" for any worn armor piece (folded to one mass noun so a full harness
        ///   does not spell out five slots); a mount word ("warhorse", "charger", "horse").
        /// </summary>
        public string KindNoun { get; }

        /// <summary>Quality band (see <see cref="GearQuality" />).</summary>
        public GearQuality Quality { get; }

        /// <summary>Engine tier, 0 when unknown. Used only as a rank tie-break, never alone to make a piece notable.</summary>
        public int Tier { get; }

        /// <summary>Raw item value. Promotes an unmarked-but-costly piece to notable (catches modded uniques with no vanilla modifier).</summary>
        public int Value { get; }
    }
}
