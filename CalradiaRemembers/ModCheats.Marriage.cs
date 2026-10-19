// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic.Marriage;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Quests;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.marry [name]
      ///   Forces a native marriage between the player and the named hero so the
      ///   consequence resolver (warm/hostile family) can be tested end to end. Pair with
      ///   cr.marriage_approve / cr.marriage_revoke to test both outcomes.
      ///   Also the player's REPAIR PATH for the marriage-memory bug (Nexus, v1.31.4): a chat that
      ///   narrated and recorded a wedding ExecuteMarry actually refused can be sealed for real here,
      ///   deliberately bypassing the guards cr.marry_check reports on (run that first to see WHY the
      ///   chat version never happened).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("marry", "cr")]
      public static string ForceMarry(List<string> args)
      {
         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";
         if (Hero.MainHero.Spouse != null) return $"You are already married to {Hero.MainHero.Spouse.Name}.";
         if (hero.Spouse != null) return $"{hero.Name} is already married to {hero.Spouse.Name}.";

         try
         {
            MarriageAction.Apply(Hero.MainHero, hero);
         }
         catch (Exception ex)
         {
            return $"Marriage to {hero.Name} failed: {ex.GetType().Name}: {ex.Message}";
         }

         if (hero.Spouse != Hero.MainHero)
            return $"MarriageAction.Apply ran, but {hero.Name} is still not recorded as your spouse: nothing was sealed.";

         PinRomanticStatusToCommitted(hero);

         return $"Married {hero.Name}. Check the quick-notice for the family's reaction (and cr.info on a kin).";
      }

      /// <summary>
      ///   cr.marry_check [name] (read-only diagnostic). Runs every guard
      ///   <see cref="BannerlordGameStateBridge" />'s ExecuteMarry checks, in the SAME order, and reports a
      ///   pass/fail line for each with the specific blocker named. Mutates nothing.
      ///   Built after a player report (Nexus, v1.31.4): the chat and letters narrated a complete wedding
      ///   and recorded it as an [EVENT] ("Mesui and Huan Yi married under Khuzait law..."), but the engine
      ///   never married anyone (cr.marriage_status showed the spouse still free). Either the marry action was
      ///   never emitted at all, or ExecuteMarry refused it and the model narrated straight over the refusal.
      ///   This command names exactly WHICH guard blocks a marriage the fiction promised. (The age gap that
      ///   was the prime suspect in that report is no longer a guard: it was removed 2026-07-21, since a
      ///   marriage is a contract between two adults.)
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("marry_check", "cr")]
      public static string MarryCheck(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero npc = ResolveHero(args);
         if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         Hero player = Hero.MainHero;
         var sb = new StringBuilder();
         sb.AppendLine($"{npc.Name}: cr.marry_check (read-only, same guards as ExecuteMarry, in the same order):");

         // Guard 1: the player is not already wed.
         bool playerAlreadyWed = player.Spouse != null;
         sb.AppendLine(playerAlreadyWed
            ? $"  [FAIL] player already wed: you are married to {player.Spouse.Name}."
            : "  [PASS] player is not already wed.");

         // Guard 2: the NPC does not belong to the player's own clan.
         bool sameClan = npc.Clan != null && npc.Clan == Clan.PlayerClan;
         sb.AppendLine(sameClan
            ? $"  [FAIL] same clan: {npc.Name} belongs to your own clan (custom forbids it)."
            : "  [PASS] not a member of your own clan.");

         // Guard 3: MarriageProspectResolver.IsMarriageable, broken down so the exact reason (age, sex,
         // an existing spouse, deceased) surfaces instead of one opaque bool.
         bool candidateAlive = npc.IsAlive;
         bool candidateHasSpouse = npc.Spouse != null;
         bool sameSex = npc.IsFemale == player.IsFemale;
         float candidateAge = npc.Age;
         float playerAge = player.Age;
         bool tooYoung = candidateAge < MarriageEligibility.MinMarriageAge;
         bool marriageable = MarriageProspectResolver.IsMarriageable(player, npc);

         sb.AppendLine(marriageable
            ? $"  [PASS] marriageable: age {(int) candidateAge} vs your {(int) playerAge} (a gap is no bar to a marriage)."
            : "  [FAIL] not marriageable: " + (!candidateAlive
                 ? "deceased"
                 : candidateHasSpouse
                    ? $"already wed to {npc.Spouse.Name}"
                    : sameSex
                       ? "same sex as you (the engine marries opposite-sex only)"
                       : tooYoung
                          ? $"under the minimum marriage age (age {(int) candidateAge}, needs >= {(int) MarriageEligibility.MinMarriageAge})"
                          : "ineligible") + ".");

         // Not a guard on the marriage itself, reported because it explains a DIFFERENT puzzle a player may
         // bring: why a clan head never OFFERED this kin unprompted. A house aims its own proposals at the
         // young (MarriageProspectPolicy); the player asking for an older hand is not blocked by it.
         if (marriageable && candidateAge > MarriageProspectPolicy.MaxProspectAge)
            sb.AppendLine($"  [note] past {MarriageProspectPolicy.MaxProspectAge:0}, so their house would not "
                        + "offer them to you unprompted, but you may still ask for them.");

         // Guard 4: MarriageGuardPolicy.MarriageAllowed (M-B5): neither party a prisoner, no active war.
         bool atWar = IsAtWarWithPlayerForCheck(npc);
         bool guardAllowed = MarriageGuardPolicy.MarriageAllowed(new MarriageGuardPolicy.MarriageStateFacts {CandidatePrisoner = npc.IsPrisoner, PlayerPrisoner = player.IsPrisoner, AtWarWithCandidate = atWar});
         sb.AppendLine(guardAllowed
            ? "  [PASS] not a prisoner (either side), and not at war."
            : "  [FAIL] blocked: " + (npc.IsPrisoner
                 ? $"{npc.Name} is a prisoner"
                 : player.IsPrisoner
                    ? "you are a prisoner"
                    : $"at war with {npc.MapFaction?.Name?.ToString() ?? "their faction"}") + ".");

         // Guard 5: the regard threshold, met by EITHER ledger (personal ReputationWithPlayer or vanilla
         // Hero.GetRelation). Reads the profile without creating one, so this stays read-only.
         NpcProfile profile = behavior.Store.Get(npc.StringId);
         int reputation = profile?.ReputationWithPlayer ?? 0;
         int vanillaRelation = npc.GetRelation(player);
         int marriageBar = ModSettings.MarriageRegardThreshold;
         bool regardEnough = reputation >= marriageBar || vanillaRelation >= marriageBar;
         sb.AppendLine(regardEnough
            ? $"  [PASS] regard: personal {reputation}, vanilla relation {vanillaRelation} (needs >= {marriageBar} on either)."
            : $"  [FAIL] regard too low: personal {reputation}, vanilla relation {vanillaRelation} (needs >= {marriageBar} on either).");

         bool eligible = !playerAlreadyWed && !sameClan && marriageable && guardAllowed && regardEnough;
         sb.AppendLine(eligible
            ? "VERDICT: ExecuteMarry would ACCEPT this marriage right now."
            : "VERDICT: ExecuteMarry would REFUSE this marriage right now (see the FAIL line(s) above). " +
              $"cr.marry {npc.Name} seals it anyway for testing, bypassing these guards.");

         return sb.ToString();
      }

      /// <summary>
      ///   Does for cr.marry what ExecuteMarry's own C3 block does for a chat marriage: pins the profile to
      ///   Committed and clears any clandestine flag left over from the courtship. Without it a marriage
      ///   sealed here keeps its pre-wedding status, and the next intimacy [EVENT] flips a REAL spouse to
      ///   SecretLover, freezes them there for life, and makes jealousy read the marriage as an affair. That
      ///   matters most on this path, which is the repair route for a wedding the chat only pretended to hold.
      ///   Silent when the player has never spoken to the hero (no profile exists to pin).
      /// </summary>
      private static void PinRomanticStatusToCommitted(Hero hero)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string _)) return;

         NpcProfile profile = behavior.Store.Get(hero.StringId);

         if (profile?.Romantic == null) return;
         profile.Romantic.Status = RomanticStatus.Committed;
         profile.Romantic.IsSecretLover = false;
         behavior.Store.Set(profile);
      }

      /// <summary>
      ///   Mirrors <c>BannerlordGameStateBridge.IsAtWarWithPlayer</c> exactly (that one is private to the
      ///   bridge): true when the NPC's faction is on the opposite side of an active war from the player's.
      /// </summary>
      private static bool IsAtWarWithPlayerForCheck(Hero npc)
         => npc?.MapFaction != null && Hero.MainHero?.MapFaction != null
            && FactionManager.IsAtWarAgainstFaction(npc.MapFaction, Hero.MainHero.MapFaction);

      /// <summary>
      ///   cr.marriage_approve [name]
      ///   Seeds the family's blessing for the player to marry the named hero (approver =
      ///   their clan leader). Then marry them (cr.marry or in-game) to see the family warm
      ///   to you instead of turning hostile.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("marriage_approve", "cr")]
      public static string MarriageApprove(List<string> args)
      {
         if (MarriageApprovalBehavior.Instance == null)
            return "Marriage behavior not active. Start a campaign first.";

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         MarriageApprovalBehavior.Instance.Grant(
            hero, hero.Clan?.Leader, (int) CampaignTime.Now.ToDays);

         return $"{hero.Name}: family blessing seeded (by {hero.Clan?.Leader?.Name?.ToString() ?? "?"}). " + "Marry them now and the family will embrace the union instead of resenting it.";
      }

      /// <summary>cr.marriage_revoke [name] — removes a seeded blessing for the named hero.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("marriage_revoke", "cr")]
      public static string MarriageRevoke(List<string> args)
      {
         if (MarriageApprovalBehavior.Instance == null)
            return "Marriage behavior not active. Start a campaign first.";

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         bool removed = MarriageApprovalBehavior.Instance.Revoke(hero.StringId);

         return removed
            ? $"{hero.Name}: blessing revoked. Marrying them now would affront the family."
            : $"{hero.Name}: no blessing on record.";
      }

      /// <summary>
      ///   cr.marriage_status [name]
      ///   Reports whether the family has blessed a match with the named hero, plus the
      ///   basic eligibility the marriage path will check (spouse single, player single).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("marriage_status", "cr")]
      public static string MarriageStatus(List<string> args)
      {
         if (MarriageApprovalBehavior.Instance == null)
            return "Marriage behavior not active. Start a campaign first.";

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         bool approved = MarriageApprovalBehavior.Instance.IsApproved(hero.StringId);
         string clan = hero.Clan?.Name?.ToString() ?? "(no clan)";

         return $"{hero.Name} — family: {clan}; blessing: {(approved ? "GRANTED" : "none")}; " + $"spouse free: {hero.Spouse == null}; player free: {Hero.MainHero.Spouse == null}.";
      }

      /// <summary>
      ///   cr.bargain_marriage [name]
      ///   Seeds a marriage_consent bargain on a clan head (or a parent): a deed (clear bandits
      ///   near a village) conditions the blessing for the player to marry one of their eligible
      ///   kin. Tests the full A.2b path without the LLM: then cr.quest_done + cr.quest_complete
      ///   → the blessing is recorded; verify with cr.marriage_status [spouse].
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bargain_marriage", "cr")]
      public static string SeedMarriageBargain(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         Hero spouse = FindEligibleCandidate(hero);

         if (spouse == null)
            return $"{hero.Name} has no unwed, eligible kin to offer (or you are already wed / no authority).";

         var village = Settlement.All.FirstOrDefault(s => s.IsVillage)?.Name?.ToString();

         if (village == null) return "No village found to anchor the deed.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (profile.ActiveQuests.Any(q => q.IsOutstanding))
            return $"{hero.Name} already has an outstanding task — settle or abandon it first.";

         int day = behavior.GameStateBridge.GetCurrentGameDay();
         var proposal = new QuestProposal {
            Type = QuestType.BanditClear,
            Description = $"Prove yourself worthy of {spouse.Name}'s hand: clear the bandits near {village}.",
            TargetSettlement = village,
            Reward = RewardGrant.MarriageConsent,
            MarriageSpouse = spouse.Name?.ToString()
         };

         InformalQuest quest = QuestFactory.Create(proposal, hero, day);

         if (quest == null) return $"Could not ground the marriage bargain for {hero.Name} → {spouse.Name}.";

         profile.ActiveQuests.Add(quest);
         behavior.Store.Set(profile);
         QuestJournalBridge.OpenMirror(quest, hero);

         return $"{hero.Name}: marriage bargain seeded — clear bandits near {village} to earn the blessing for " + $"marrying {spouse.Name}. Then: cr.quest_done, cr.quest_complete, then cr.marriage_status {spouse.Name}.";
      }

      /// <summary>
      ///   cr.arrange_marriage &lt;yourKin&gt; &lt;theirKin&gt; [npc]
      ///   The MATCHMAKER test path: weds one of YOUR clan's unwed kin to a noble of ANOTHER clan (you are
      ///   neither spouse), with an authority NPC (the target's clan head or parent, or the target themselves)
      ///   consenting. Drives the real arrange_marriage action through the bridge, so it runs the SAME guards
      ///   the LLM path uses: engine legality (MarriageModel), the NPC's authority, and the consent policy.
      ///   npc defaults to the open conversation, else the target kin's own clan leader. Note: it weds two real
      ///   NPCs, so use it on a test save.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("arrange_marriage", "cr")]
      public static string ArrangeMarriage(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
         if (args == null || args.Count < 2)
            return "Usage: cr.arrange_marriage <yourKin> <theirKin> [npc]  (yourKin = a member of YOUR clan; theirKin = a noble of another clan).";

         string playerKin = args[0];
         string targetKin = args[1];

         Hero npc = args.Count > 2
            ? ResolveHero(args.Skip(2).ToList())
            : Hero.OneToOneConversationHero;

         // Fall back to the target kin's own clan leader as the consenting authority.
         if (npc == null)
         {
            Hero target = Hero.AllAliveHeroes.FirstOrDefault(
               h => string.Equals(h.Name?.ToString(), targetKin, StringComparison.OrdinalIgnoreCase));
            npc = target?.Clan?.Leader ?? target;
         }

         if (npc == null) return "No authority NPC found. Pass one as the 3rd arg, or open a conversation first.";

         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {
               Type = "arrange_marriage",
               Parameters = new Dictionary<string, string> {["player_kin"] = playerKin, ["target_kin"] = targetKin}
            }, npc, null);

         return outcome?.Message
             ?? $"arrange_marriage did nothing (see CalradiaRemembers.log). npc={npc.Name}, {playerKin} + {targetKin}.";
      }

      /// <summary>First unwed, eligible kin of the NPC's house (clan + own children) the player could marry.</summary>
      private static Hero FindEligibleCandidate(Hero npc)
      {
         if (npc == null || Hero.MainHero == null) return null;
         var pool = new List<Hero>();
         if (npc.Clan != null) pool.AddRange(npc.Clan.Heroes);
         if (npc.Children != null) pool.AddRange(npc.Children);

         return pool.FirstOrDefault(h => h is {IsAlive: true} && MarriageProspectResolver.IsMarriageable(Hero.MainHero, h) && MarriageProspectResolver.HasAuthority(npc, h));
      }
   }
}
