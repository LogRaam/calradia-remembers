// Code written by Gabriel Mailhot, 25/07/2026.
// Pure decision for the "gift equipment in chat" feature: when the player gifts an equipment item to an NPC,
// should the NPC actually EQUIP it (a robe gifted to the wife gets worn; a sword a companion likes gets
// equipped), or merely accept it as a gesture while keeping their own gear (a rival lord keeps his trusted
// blade)? The engine half (reading the item's type, mapping it to a Bannerlord equipment slot, and calling
// the real equip API) is built separately; this stays pure so the rule (empty slot always takes it, a real
// upgrade is worn on merit, a fond NPC wears even a lateral gift, a cold NPC does not) is unit-tested on its
// own and never silently downgrades someone's gear from a random trinket.

namespace CalradiaRemembers.Logic
{
    /// <summary>Why an NPC did, or did not, equip a gift the player just handed them.</summary>
    public enum GiftEquipReason
    {
        /// <summary>The gift is not a wearable/wieldable item (trade goods, etc.); it is never equipped.</summary>
        NotEquippable,

        /// <summary>Nothing was in the target slot, so they put the gift on.</summary>
        EmptySlot,

        /// <summary>The gift is worth more than what they had there.</summary>
        Upgrade,

        /// <summary>Not an upgrade, but a bonded/fond NPC wears the player's gift anyway.</summary>
        Affection,

        /// <summary>Not an upgrade, and not fond enough to wear it over their own gear.</summary>
        KeptOwnGear,

        /// <summary>
        ///   The recipient is the player's held PRISONER and the gift is a garment: their captor is dressing
        ///   them, so it is put on regardless of fondness or value (the prisoner does not get a say). Only a
        ///   wearable garment reaches this, never a weapon, so a captive is never armed by a "gift".
        /// </summary>
        Imposed
    }

    /// <summary>The facts <see cref="GiftEquipPolicy.Decide" /> needs to judge one gifted equipment item.</summary>
    public readonly struct GiftEquipFacts
    {
        /// <summary>Every fact defaults to the refusing answer, so a caller names only what it actually knows.</summary>
        public GiftEquipFacts() { }

        /// <summary>False for a non-wearable/wieldable gift (trade goods, etc.); such a gift is never equipped.</summary>
        public bool IsEquippable { get; init; } = false;
        /// <summary>True when the target equipment slot currently holds nothing.</summary>
        public bool SlotEmpty { get; init; } = false;
        /// <summary>The gifted item's value.</summary>
        public int GiftValue { get; init; } = 0;
        /// <summary>Value of the item currently in that slot (0 when <see cref="SlotEmpty" />).</summary>
        public int CurrentSlotValue { get; init; } = 0;
        /// <summary>The NPC's PERSONAL regard for the player (CR's own ledger, not vanilla relation).</summary>
        public int Relation { get; init; } = 0;
        /// <summary>
        ///   True for a spouse, or one of the player's OWN companions: such an NPC wears a gift fondly
        ///   regardless of <see cref="Relation" />.
        /// </summary>
        public bool IsSentimental { get; init; } = false;
        /// <summary>
        ///   True when the recipient is the player's held PRISONER and the gift is a wearable GARMENT (never a
        ///   weapon): the captor is dressing them, so it is imposed regardless of fondness or value. The engine
        ///   half sets this only for garment slots, so a captive can never be handed a weapon this way.
        /// </summary>
        public bool IsCaptiveDressing { get; init; } = false;

    }

    /// <summary>The policy's verdict: whether to equip the gift, and why.</summary>
    public readonly struct GiftEquipDecision
    {
        /// <summary>True when the engine should actually equip the gifted item.</summary>
        public bool ShouldEquip { get; }

        /// <summary>The reason behind <see cref="ShouldEquip" />, for narration.</summary>
        public GiftEquipReason Reason { get; }

        private GiftEquipDecision(bool shouldEquip, GiftEquipReason reason)
        {
            ShouldEquip = shouldEquip;
            Reason = reason;
        }

        internal static GiftEquipDecision Equip(GiftEquipReason reason) => new GiftEquipDecision(true, reason);

        internal static GiftEquipDecision Keep(GiftEquipReason reason) => new GiftEquipDecision(false, reason);
    }

    /// <summary>
    ///   Pure policy for whether an NPC equips an equipment item the player just gifted them: an empty slot
    ///   always takes it, a real upgrade is worn on merit, a sentimental or fond-enough NPC wears even a
    ///   lateral gift, and a cold NPC keeps their own trusted gear. A non-equippable gift never touches
    ///   equipment slots at all.
    /// </summary>
    public static class GiftEquipPolicy
    {
        /// <summary>
        ///   Relation at or above which an NPC will wear the player's gift out of fondness even when it is not
        ///   a strict upgrade. Tunable (Gabriel adjusts by feel). A spouse or the player's own companion clears
        ///   this via <see cref="GiftEquipFacts.IsSentimental" /> regardless of <see cref="GiftEquipFacts.Relation" />.
        /// </summary>
        public const int AffectionThreshold = 30;

        /// <summary>Decides whether the NPC equips the gifted item, and why.</summary>
        public static GiftEquipDecision Decide(GiftEquipFacts facts)
        {
            if (!facts.IsEquippable) return GiftEquipDecision.Keep(GiftEquipReason.NotEquippable);
            // A captor dressing a held prisoner overrides the fondness/value math: the prisoner does not choose.
            if (facts.IsCaptiveDressing) return GiftEquipDecision.Equip(GiftEquipReason.Imposed);
            if (facts.SlotEmpty) return GiftEquipDecision.Equip(GiftEquipReason.EmptySlot);
            if (facts.GiftValue > facts.CurrentSlotValue) return GiftEquipDecision.Equip(GiftEquipReason.Upgrade);
            if (facts.IsSentimental || facts.Relation >= AffectionThreshold) return GiftEquipDecision.Equip(GiftEquipReason.Affection);

            return GiftEquipDecision.Keep(GiftEquipReason.KeptOwnGear);
        }

        /// <summary>
        ///   Which equipment set the engine must write a gift into so it is actually SEEN. A held prisoner is
        ///   rendered in their BATTLE set (the captor scene is a world-map conversation, which shows battle gear,
        ///   not the town/civilian outfit), so anything a captor puts on them must go on the battle set or it is
        ///   invisible. Otherwise the normal rule holds: a civilian garment on the town outfit, war gear on the
        ///   battle set. Returns true for "use the battle set".
        /// </summary>
        public static bool WearsOnBattleSet(bool isHeldCaptive, bool isCivilianGarment)
            => isHeldCaptive || !isCivilianGarment;
    }
}
