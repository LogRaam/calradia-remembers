// Code written by Gabriel Mailhot, 05/07/2026.
// Nemesis pillar (ratified 2026-07-05): the persistent named bandit boss. This is the plain data
// carrier for a latent nemesis identity — NO map party, NO behaviour here (see NemesisRevealPolicy,
// NemesisRosterPolicy, NemesisNameGenerator for the pure decisions built on top of it). Engine glue
// (attaching this identity's Hero to a vanilla bandit party leader at encounter time) is out of scope.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   A persistent, regional, named bandit boss the player can build a recurring vendetta with.
    ///   Carries no behaviour: engine code owns turning this into a real Hero/party leader, and the
    ///   pure policies in this namespace (<see cref="NemesisRevealPolicy"/>, <see cref="NemesisRosterPolicy"/>)
    ///   decide when he shows up and which recurring identity to reuse.
    /// </summary>
    public sealed class NemesisIdentity
    {
        /// <summary>Stable identifier for this nemesis (engine-assigned; e.g. a GUID or save-scoped counter).</summary>
        public string Id { get; set; }

        /// <summary>Given name (see <see cref="NemesisNameGenerator"/>).</summary>
        public string Name { get; set; }

        /// <summary>Byname/title, e.g. "the Faceless Fox" (see <see cref="NemesisNameGenerator"/>).</summary>
        public string Epithet { get; set; }

        /// <summary>
        ///   Which vanilla bandit faction this nemesis leads (the vanilla bandit-clan StringId, e.g.
        ///   "looters", "forest_bandits", "mountain_bandits", "sea_raiders", "desert_bandits",
        ///   "steppe_bandits") — his culture/flavour matches whichever band took the player prisoner.
        /// </summary>
        public string BandCultureKey { get; set; }

        /// <summary>
        ///   The region this nemesis is anchored to (engine-defined region/area id — typically where the
        ///   player was first captured by his band). The roster is regional: the SAME band culture can
        ///   have a different living nemesis in a different region.
        /// </summary>
        public string RegionId { get; set; }

        /// <summary>False once this nemesis has been killed (or otherwise permanently resolved) — a dead nemesis never recurs.</summary>
        public bool IsAlive { get; set; }

        /// <summary>Running tally of gold/goods the player owes this nemesis (later increments: collector-party consequence).</summary>
        public int DebtOwed { get; set; }

        /// <summary>Total number of times this nemesis has personally been the one behind an encounter (hunter or captor side), across his whole life.</summary>
        public int TimesEncountered { get; set; }

        /// <summary>
        ///   Qualifying band-encounters (matching faction+region) since this identity was last REVEALED
        ///   as the party leader — the pity-timer counter <see cref="NemesisRevealPolicy"/> consumes.
        ///   Resets to 0 every time he is revealed.
        /// </summary>
        public int EncountersSinceReveal { get; set; }

        /// <summary>Campaign day this identity was first minted.</summary>
        public int MintedDay { get; set; }

        /// <summary>Campaign day this nemesis died, or null while still alive.</summary>
        public int? DeathDay { get; set; }
    }
}
