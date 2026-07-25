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
        KeptOwnGear
    }

    /// <summary>The facts <see cref="GiftEquipPolicy.Decide" /> needs to judge one gifted equipment item.</summary>
    public readonly struct GiftEquipFacts
    {
        /// <summary>False for a non-wearable/wieldable gift (trade goods, etc.); such a gift is never equipped.</summary>
        public bool IsEquippable { get; }

        /// <summary>True when the target equipment slot currently holds nothing.</summary>
        public bool SlotEmpty { get; }

        /// <summary>The gifted item's value.</summary>
        public int GiftValue { get; }

        /// <summary>Value of the item currently in that slot (0 when <see cref="SlotEmpty" />).</summary>
        public int CurrentSlotValue { get; }

        /// <summary>The NPC's PERSONAL regard for the player (CR's own ledger, not vanilla relation).</summary>
        public int Relation { get; }

        /// <summary>
        ///   True for a spouse, or one of the player's OWN companions: such an NPC wears a gift fondly
        ///   regardless of <see cref="Relation" />.
        /// </summary>
        public bool IsSentimental { get; }

        /// <summary>Builds the fact set the policy judges.</summary>
        public GiftEquipFacts(bool isEquippable, bool slotEmpty, int giftValue, int currentSlotValue, int relation, bool isSentimental)
        {
            IsEquippable = isEquippable;
            SlotEmpty = slotEmpty;
            GiftValue = giftValue;
            CurrentSlotValue = currentSlotValue;
            Relation = relation;
            IsSentimental = isSentimental;
        }
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
            if (facts.SlotEmpty) return GiftEquipDecision.Equip(GiftEquipReason.EmptySlot);
            if (facts.GiftValue > facts.CurrentSlotValue) return GiftEquipDecision.Equip(GiftEquipReason.Upgrade);
            if (facts.IsSentimental || facts.Relation >= AffectionThreshold) return GiftEquipDecision.Equip(GiftEquipReason.Affection);

            return GiftEquipDecision.Keep(GiftEquipReason.KeptOwnGear);
        }
    }
}
