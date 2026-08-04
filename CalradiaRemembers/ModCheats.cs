// Code written by Gabriel Mailhot, 24/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   /// <summary>
   ///   Exposes diagnostic commands to the Bannerlord cheat console.
   ///   Open the console with Alt + ~ (must be enabled in launcher config),
   ///   then run any of:
   ///   campaign.test_llm     — run a synchronous LLM diagnostic
   ///   campaign.open_chat    — open the Calradia Remembers chat window (Sprint 7.6.A)
   ///   Sprint 10 — action testing without recreating a scene (group "cr").
   ///   These exercise the REAL pipeline (RelationGate → bridge.ExecuteAction →
   ///   ApplyPlayerRelation/GiveGoldAction), not a reimplementation:
   ///   cr.relation &lt;delta&gt; [name]            — simulate a change_relation action
   ///   cr.relation_intimate &lt;delta&gt; [name]   — same, with an intimacy event (cap +3, bypasses cooldown)
   ///   cr.give_gold &lt;amount&gt; [name]          — NPC gives the player gold
   ///   cr.take_gold &lt;amount&gt; [name]          — player gives the NPC gold
   ///   cr.info [name]                          — relation, cooldown state, captive status
   ///   cr.relation_reset [name]                — clear the relation-gain cooldown stamp
   ///   cr.discover_test [name]                 — inject 3 placeholder discoveries (Sprint 10.5 encyclopedia test)
   ///   [name] is an optional name fragment; if omitted, the current conversation NPC is used.
   /// </summary>
   public static partial class ModCheats
   {
      /// <summary>cr.help — list all cr.* commands with a short description.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("help", "cr")]
      public static string Help(List<string> args) => "Calradia Remembers console commands (group: cr)\n" + "\n" + "MEMORY EDITING\n" + "  cr.memory [name]                     List all recorded events with their index\n" + "  cr.memory_add <days_ago> <text> [name]  Create a memory dated in the past (build a prior relationship)\n" + "  cr.memory_set <index> <text> [name]  Replace the text of one event (last word = name if it matches a hero)\n" + "  cr.forget <index> [name]             Delete one event by index\n" + "  cr.forget_all [name]                 Wipe all memory events for a fresh start\n" + "\n" + "RELATION\n" + "  cr.relation <delta> [name]           Simulate change_relation (event Other) - cap +1, cooldown 8h\n" + "  cr.relation_intimate <delta> [name]  Same with Intimacy event - cap +3, bypasses cooldown\n" + "  cr.relation_reset [name]             Clear the relation-gain cooldown\n" + "  cr.rep_set <value> [name]            Force-set ReputationWithPlayer (bypasses gate - for testing)\n" + "  cr.info [name]                       Show relation, captive status, cooldown remaining\n" + "\n" + "GOLD\n" + "  cr.give_gold <amount> [name]         NPC gives the player gold\n" + "  cr.take_gold <amount> [name]         Player gives the NPC gold\n" + "\n" + "LETTERS\n" + "  cr.letter [name]                     Queue a RomanticCorrespondence test letter from the NPC (immediate delivery)\n" + "  cr.compose [name]                    Open the letter compose window to write to the NPC (Sprint 12d test path)\n" + "  cr.courier [name]                    Dispatch a test letter + spawn the visible courier party (Sprint 12c)\n" + "  cr.courier_slow [name]               Spawn a SLOW engageable courier to observe bandit interception\n" + "\n" + "MULTI-NPC WITNESSES\n" + "  cr.witnesses [name]                  Diagnose: log settlement/party presence data for an NPC (Talk-vs-Visit check)\n" + "  cr.witness [witnessName]             Add a witness to the open chat session (manual seed for testing)\n" + "\n" + "CAPTIVE ENCOUNTER (Sprint 17 - Hardcore only)\n" + "  cr.crc_scene [female|male] [solo|collective] [intent] [name]\n" + "      intent: interrogation (default) | desire | domination | torture | training | reward\n" + "      Example: cr.crc_scene female collective desire Alympia\n" + "  cr.bandit_scene [intent] [female|male]   Synthetic brigand captor scene (extortion|intimidation|revenge|spoil) - no capture needed\n" + "  cr.kinks [name]                      List the NPC's kinks and valid kink names\n" + "  cr.add_kink <kink> [name]            Add a kink (apply BEFORE opening a scene)\n" + "  cr.remove_kink <kink> [name]         Remove a kink from the NPC's profile\n" + "\n" + "PLAYER STANDING\n" + "  cr.standing                          Show player clan tier and arena rank/wins\n" + "\n" + "RECRUITMENT\n" + "  cr.hire_info [name]                  Show join_party recruitability + vanilla/floor/cap hire price\n" + "  cr.bargain_items [name]              Seed a pay-in-goods bargain (DeliverItems -> join). Then use the bag icon\n" + "\n" + "MARRIAGE (Phase A.1 engine test)\n" + "  cr.marriage_approve [name]           Seed the family's blessing to marry this hero\n" + "  cr.marriage_revoke [name]            Remove a seeded blessing\n" + "  cr.marriage_status [name]            Show blessing + basic eligibility\n" + "  cr.marry_check [name]                Read-only: which ExecuteMarry guard blocks (or would block) a marriage\n" + "  cr.marry [name]                      Force a native marriage to test the family consequence\n" + "  cr.bargain_marriage [name]           Seed a marriage_consent bargain (clear bandits -> blessing). Then cr.quest_done/_complete\n" + "\n" + "DIVORCE (Phase 1, NPC case)\n" + "  cr.divorce_info [name]               Eligibility + live facts + any active estrangement process\n" + "  cr.divorce [name]                    Seed end_marriage via the REAL verb execute path\n" + "  cr.divorce_tick                      Run one day's advance for every active estrangement process\n" + "\n" + "ASSEMBLY / COUNCIL\n" + "  cr.council [companions|family|lords|war|parley <faction>]  Open a REAL council of your own house, your allies (war), or a parley with a named enemy (default companions)\n" + "  cr.council_test [n|off]              Spike: seat N synthetic members on the OPEN chat (single-call model)\n" + "  cr.roundtable [on|off]               Arm/report the per-NPC round-table lap (one call per speaker)\n" + "\n" + "GRUDGES\n" + "  cr.grudges [name]                    Dev-truth dump of every grudge this NPC nurses (source, cause, day, rigidity, resolution)\n" + "  cr.grudge_test [name]                 Plant a synthetic FailedQuest grudge (real TryPlant gate, may refuse)\n" + "  cr.grudge_backfill                   Force-rerun the save migration scan (formed/skipped); a no-op the second time\n" + "  cr.grudge_resolve [amends|gift <value>] [name]  Evaluate a gesture against the NPC's dominant resolvable grudge (no LLM)\n" + "\n" +
"COMPANION AUDIENCES\n" + "  cr.audience_topics [name]            List found-topics for one companion, or the whole party if no name (why each qualifies)\n" + "  cr.audience_fire                     Force-fire the strongest found-topic audience anywhere in the party (bypasses the cooldown)\n" + "\n" +
"COURT ACTIONS\n" + "  cr.rp_verbs [name]                    Eligibility + amounts for give_influence/lend_troops/sway_opinion/release_prisoner\n" + "  cr.give_troops [name]                 Force give_troops (reciprocal of lend_troops) via the REAL bridge path; reports facts + outcome\n" + "  cr.spend_influence [name]             Force spend_influence (reciprocal of give_influence) via the REAL bridge path; reports facts + outcome\n" + "\n" +
"APPRECIATIONS\n" + "  cr.appreciations [name]              Dev-truth dump of every kindness this NPC feels (source, cause, day, tarnished value)\n" + "  cr.appreciation_test [name]           Plant a synthetic QuestDoneForThem appreciation (real TryPlant gate, may refuse)\n" + "  cr.appreciation_backfill              Force-rerun the save migration scan (formed/skipped); a no-op the second time\n" + "\n" +
"DIAGNOSTICS\n" + "  cr.profile_check [name]              Round-trip the NPC's profile (serialize+read back); names the field if it fails\n" + "\n" + "DISCOVERY\n" + "  cr.discover_test [name]              Seed 3 PLACEHOLDER DiscoveredTraits (UI test only — real ones come from conversation)\n" + "  cr.discoveries [name]                 List real DiscoveredTraits with their index and key\n" + "  cr.discover_forget <index> [name]     Delete one DiscoveredTrait by index (see cr.discoveries)\n" + "  cr.discover_forget_all [name]         Wipe all DiscoveredTraits for this NPC\n" + "\n" + "QUESTS\n" + "  cr.quest [name]                      Seed a bandit_clear quest on the NPC (journal opens)\n" + "  cr.quest_scout [name]                Seed a scout_army quest on the NPC (uses giver's first enemy)\n" + "  cr.quest_done [name]                 Force-stamp the outstanding quest as satisfied (journal updates)\n" + "  cr.quest_complete [name]             Pay reward and close the journal entry with success\n" + "  cr.quest_abandon [name]              Force-abandon the outstanding quest (-3 regard, journal closes)\n" + "  cr.quests [name]                     List the NPC's quests with their current state\n" + "\n" +
"CAPTIVE ESCAPE GEAR\n" + "  cr.gear_stock [name]                 Report one holder's held-gear stock (or every holder, if no name)\n" + "  cr.gear_recover [name]                Force-recover a holder's whole stock into the player's inventory\n" + "  cr.gear_test [name]                  Force-transfer the player's CURRENT battle equipment to a holder (real Fled path)\n" + "\n" + "[name] is an optional name fragment (case-insensitive). If omitted, the current conversation NPC is used.";

      private static NpcProfile GetOrCreateProfile(NpcMemoryCampaignBehavior behavior, Hero hero)
      {
         NpcProfile profile = behavior.Store.Get(hero.StringId);

         if (profile != null) return profile;

         profile = HeroProfileMapper.FromHero(hero);
         behavior.Store.Set(profile);

         return profile;
      }

      /// <summary>
      ///   Resolves the target hero: a name fragment if provided (case-insensitive, diacritic-folded,
      ///   excluding the player), else the current conversation NPC. Diacritic folding
      ///   (<see cref="NameNormalization.Fold" />) lets a plain-typed query like "Heloise" find a hero
      ///   named "Héloïse". A multi-word query ("Heloise the Finger") requires EVERY folded query token
      ///   to be found (as a substring) within SOME folded name part, so a full name/epithet disambiguates
      ///   between several heroes who merely share one part (e.g. the same epithet).
      /// </summary>
      private static Hero ResolveHero(IEnumerable<string> nameTokens)
      {
         string query = nameTokens == null
            ? ""
            : string.Join(" ", nameTokens).Trim();

         if (string.IsNullOrEmpty(query)) return Hero.OneToOneConversationHero;

         string[] queryTokens = query
                                .Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries)
                                .Select(NameNormalization.Fold)
                                .ToArray();

         if (queryTokens.Length == 0) return Hero.OneToOneConversationHero;

         return Hero.AllAliveHeroes.FirstOrDefault(h =>
         {
            if (h == Hero.MainHero || h.Name == null) return false;

            string[] nameParts = h.Name
                                   .ToString()
                                   .Split(new[] {' ', '-'}, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(NameNormalization.Fold)
                                   .ToArray();

            return queryTokens.All(qt => nameParts.Any(np => np.Contains(qt)));
         });
      }

      private static bool TryContext(out NpcMemoryCampaignBehavior behavior, out string error)
      {
         behavior = NpcMemoryCampaignBehavior.Instance;
         if (behavior == null)
         {
            error = "Calradia Remembers behavior not active. Start a campaign first.";

            return false;
         }

         error = null;

         return true;
      }
   }
}
