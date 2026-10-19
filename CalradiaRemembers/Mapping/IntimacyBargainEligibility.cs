// Code written by Gabriel Mailhot, 23/06/2026.
// Negotiation Phase 3 — the game-side eligibility read for "intimacy as currency", shared by the prompt
// context builder and the intimacy-event hook (so the leverage flag is set on exactly the same archetype the
// dynamic is taught to). The DECISION is the pure CalradiaRemembers.Logic.IntimacyCurrencyPolicy; this only
// gathers the facts (gender, kinship, traits, adult tier).

#region

using CalradiaRemembers.Foundation;
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

            return IntimacyCurrencyPolicy.IsEligible(new IntimacyCurrencyPolicy.LeverageArchetype {PlayerIsFemale = Hero.MainHero.IsFemale, NpcIsMale = !npc.IsFemale, NpcIsPlayerKin = npc.Clan != null && npc.Clan == Hero.MainHero.Clan, NpcHonor = npc.GetTraitLevel(DefaultTraits.Honor), NpcCalculating = npc.GetTraitLevel(DefaultTraits.Calculating), NpcMercy = npc.GetTraitLevel(DefaultTraits.Mercy), AdultExplicitOrHigher = ModSettings.AdultContentLevel >= AdultContentLevel.Explicit});
        }
    }
}
