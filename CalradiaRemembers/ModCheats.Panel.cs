// Code written by Gabriel Mailhot, 11/07/2026.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic.Grudges;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
    public static partial class ModCheats
    {
        /// <summary>
        ///   cr.panel_seed [name]
        ///   Fills every section of the portrait info panel on the named lord (or the conversation NPC) so it can
        ///   be verified visually: personal regard, a simulated authored backstory (as if from
        ///   character_overrides.json), three discoveries (including a kink), an appearance description, and a
        ///   known grudge. Background comes from the vanilla encyclopedia automatically. Left-click the portrait
        ///   in chat afterwards to check the rendering.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("panel_seed", "cr")]
        public static string SeedInfoPanel(List<string> args)
        {
            if (!TryContext(out Behaviors.NpcMemoryCampaignBehavior behavior, out string error)) return error;

            Hero hero = ResolveHero(args);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            var store = behavior.Store;
            NpcProfile profile = store.Get(hero.StringId) ?? Mapping.HeroProfileMapper.FromHero(hero);
            var day = (int) CampaignTime.Now.ToDays;
            string subj = hero.IsFemale ? "She" : "He";

            // Personal regard (facts line -> "genuine regard (14)").
            profile.ReputationWithPlayer = 14;

            // Character: simulate the authored backstory the panel would otherwise read from character_overrides.json.
            profile.AuthoredBackstory =
                "A hard-bitten veteran of the border marches, dry of humour and slow to trust, who keeps an old campaign knife close at hand. (panel test seed)";

            // What you have learned: two traits + one kink.
            AddDiscovery(profile, "archetype", $"{subj} carries the wary calm of someone who has buried friends. (panel test seed)", day);
            AddDiscovery(profile, "quirk", $"{subj} taps two fingers on the table when weighing a lie. (panel test seed)", day);
            AddDiscovery(profile, "kink_dominant", $"{subj} has let slip a taste for taking charge behind closed doors. (panel test seed)", day);

            store.Set(profile);

            // Appearance: inject prose (the panel reads AppearanceProseBehavior).
            Appearance.AppearanceProseBehavior.Instance?.Set(hero.StringId,
                "Weathered and lean, a jagged scar through the left brow, grey threading dark hair; stands like a soldier at rest. (panel test seed)");

            // Grievances: plant a KNOWN grudge held by this NPC against the player.
            string grudgeNote = "grudge NOT planted (ShouldForm gate refused; check log)";
            if (Grudges.GrudgeBehavior.Instance != null
                && Grudges.GrudgeBehavior.Instance.TryPlant(hero, Hero.MainHero, GrudgeSource.FailedQuest,
                    "a promise you broke when it counted most", 40, true, day))
                grudgeNote = "grudge planted (known)";

            return $"{hero.Name}: seeded regard 14, backstory, 3 discoveries (incl. a kink), appearance prose, {grudgeNote}. " +
                   "Left-click their portrait in chat to verify. Background is drawn from the vanilla encyclopedia on its own.";
        }

        private static void AddDiscovery(NpcProfile profile, string key, string description, int day)
        {
            if (profile.DiscoveredTraits.Exists(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase))) return;
            profile.DiscoveredTraits.Add(new DiscoveredTrait {Key = key, Description = description, GameDay = day});
        }
    }
}
