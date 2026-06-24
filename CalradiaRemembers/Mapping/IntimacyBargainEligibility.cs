// Code written by Gabriel Mailhot, 23/06/2026.
// Negotiation Phase 3 — the game-side eligibility read for "intimacy as currency", shared by the prompt
// context builder and the intimacy-event hook (so the leverage flag is set on exactly the same archetype the
// dynamic is taught to). The DECISION is the pure CalradiaRemembers.Logic.IntimacyCurrencyPolicy; this only
// gathers the facts (gender, kinship, traits, adult tier).

#region

using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Mapping
{
    /// <summary>Resolves the adult-gated, archetype-filtered eligibility for the intimacy-as-currency dynamic.</summary>
    public static class IntimacyBargainEligibility
    {
        public static bool IsEligible(Hero npc)
        {
            if (npc == null || Hero.MainHero == null) return false;

            return IntimacyCurrencyPolicy.IsEligible(
                playerIsFemale:        Hero.MainHero.IsFemale,
                npcIsMale:             !npc.IsFemale,
                npcIsPlayerKin:        npc.Clan != null && npc.Clan == Hero.MainHero.Clan,
                npcHonor:              npc.GetTraitLevel(DefaultTraits.Honor),
                npcCalculating:        npc.GetTraitLevel(DefaultTraits.Calculating),
                npcMercy:              npc.GetTraitLevel(DefaultTraits.Mercy),
                adultExplicitOrHigher: ModSettings.AdultContentLevel >= AdultContentLevel.Explicit);
        }
    }
}
