// Code written by Gabriel Mailhot, 24/08/2026.
// Council Tier 3 dev harness: seed a live standing accord on a hero so its Encyclopedia "Standing accords" panel
// can be validated by eye without playing out a whole parley/council to create one. Injects straight into each
// accord behavior's live list via its DebugSeed seam (bypassing the real guards/party moves), then the panel is
// read the ordinary way. It is a REAL record, so it behaves normally if days pass or the game is saved.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Assemblies;
using CalradiaRemembers.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
    public static partial class ModCheats
    {
        /// <summary>
        ///   cr.seed_accord &lt;tribute|stipend|pact|hostage|ward&gt; [name]
        ///   Seeds a live council accord on the target hero (the current conversation NPC when no name is given),
        ///   so the "Standing accords" section on that hero's Encyclopedia page can be checked at a glance.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("seed_accord", "cr")]
        public static string SeedAccord(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

            string type = args?.FirstOrDefault()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(type))
                return "Usage: cr.seed_accord <tribute|stipend|pact|hostage|ward> [name]. Seeds a live accord on the hero, then open that hero's Encyclopedia page to see the 'Standing accords' section.";

            Hero hero = ResolveHero(args.Skip(1));
            if (hero == null) return "No hero found (give a name fragment, or open a conversation first).";
            if (hero == Hero.MainHero) return "Pick another hero; the player's own page never shows accords.";

            Hero player = Hero.MainHero;

            switch (type)
            {
                case "tribute":
                    if (CouncilTributeBehavior.Instance == null) return "Tribute system not active (load a campaign first).";
                    CouncilTributeBehavior.Instance.DebugSeed(hero.StringId, 300, 25);
                    break;

                case "stipend":
                    if (CouncilStipendBehavior.Instance == null) return "Stipend system not active (load a campaign first).";
                    CouncilStipendBehavior.Instance.DebugSeed(hero.StringId, hero.Name?.ToString(), 200, 25);
                    break;

                case "pact":
                    if (CouncilPactBehavior.Instance == null) return "Pact system not active (load a campaign first).";
                    IFaction playerFaction = player?.MapFaction;
                    IFaction heroFaction = hero.MapFaction;
                    if (playerFaction == null || heroFaction == null) return "Both the player and the hero need a faction for a pact.";
                    if (playerFaction == heroFaction) return $"{hero.Name} is in your own faction; a pact only shows on a hero of ANOTHER faction.";
                    CouncilPactBehavior.Instance.DebugSeed(playerFaction.StringId, heroFaction.StringId, 30);
                    break;

                case "hostage":
                    if (CouncilHostageBehavior.Instance == null) return "Hostage system not active (load a campaign first).";
                    CouncilHostageBehavior.Instance.DebugSeed(hero.StringId, hero.StringId, hero.Clan?.StringId, 40);
                    break;

                case "ward":
                    if (CouncilWardBehavior.Instance == null) return "Ward system not active (load a campaign first).";
                    string mentorId = Hero.AllAliveHeroes.FirstOrDefault(h => h != hero && h != Hero.MainHero)?.StringId;
                    CouncilWardBehavior.Instance.DebugSeed(mentorId, hero.StringId);
                    break;

                default:
                    return $"Unknown accord type '{type}'. Use tribute, stipend, pact, hostage, or ward.";
            }

            return $"Seeded a {type} accord on {hero.Name}. Open their Encyclopedia page to see the 'Standing accords' section. (Dev seed: a real record, so it behaves normally if days pass or you save.)";
        }
    }
}
