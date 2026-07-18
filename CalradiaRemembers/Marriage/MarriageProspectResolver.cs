// Code written by Gabriel Mailhot, 20/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Marriage
{
   /// <summary>
   ///   Produces the "MARRIAGE — IF THE PLAYER SEEKS A MATCH" block injected into the prompt:
   ///   the unwed, suitable kin of the conversation NPC's clan (and the NPC themselves when
   ///   eligible), so the NPC can only ever name real, marriageable people. Returns null when
   ///   a marriage request does not apply — the NPC is not their clan's head, this is the
   ///   player's own clan, the player is already wed, or no kin qualify.
   /// </summary>
   public static class MarriageProspectResolver
   {
      /// <summary>Builds the prompt block, or null when no marriage request applies to this NPC.</summary>
      public static string Build(Hero npc)
      {
         try
         {
            if (npc == null) return null;
            var player = Hero.MainHero;

            if (player is not {Spouse: null}) return null; // player already wed

            // The clan's HEAD may bless any eligible kin of an external house.
            bool isLeader = npc.Clan != null && npc.Clan.Leader == npc && npc.Clan != Clan.PlayerClan;

            var candidates = new List<Hero>();
            if (isLeader)
               foreach (Hero member in npc.Clan.Heroes.ToList())
                  if (member is {IsAlive: true} && IsMarriageable(player, member))
                     candidates.Add(member);

            // A PARENT may bless their OWN child even when they do not head the clan — the
            // natural person a player approaches for a daughter's or son's hand.
            if (npc.Children != null)
               foreach (Hero child in npc.Children)
                  if (child is {IsAlive: true} && child.Clan != Clan.PlayerClan && IsMarriageable(player, child) && !candidates.Contains(child))
                     candidates.Add(child);

            if (candidates.Count == 0) return null;

            var sb = new StringBuilder();
            sb.AppendLine(isLeader
               ? $"You are the head of {npc.Clan.Name}. These unwed kin of your house could be matched to a worthy suitor:"
               : "As their parent, the match of your own unwed children is yours to bless:");
            foreach (Hero c in candidates)
               sb.AppendLine($"  - {c.Name} ({DescribeKin(npc, c)}, age {(int) c.Age})");

            int familyTier = npc.Clan?.Tier ?? 0;
            int playerTier = player.Clan?.Tier ?? 0;
            var playerRenown = (int) (player.Clan?.Renown ?? 0f);
            sb.Append($"Your house stands at clan tier {familyTier}. The player's clan stands at tier {playerTier} " +
                      $"(renown {playerRenown}). Weigh that gap, the player's proven deeds, and your own regard: a great " +
                      "house demands a suitor of real standing, while a lesser house may welcome a rising one.");

            return sb.ToString();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MARRIAGE] prospect resolver failed: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Whether the NPC may bless a marriage to <paramref name="candidate" />: they head the
      ///   candidate's clan, or they are the candidate's parent.
      /// </summary>
      public static bool HasAuthority(Hero npc, Hero candidate)
      {
         if (npc == null || candidate == null) return false;
         bool isLeader = npc.Clan != null && candidate.Clan == npc.Clan && npc.Clan.Leader == npc;
         bool isParent = npc.Children != null && npc.Children.Contains(candidate);

         return isLeader || isParent;
      }

      /// <summary>
      ///   Whether <paramref name="candidate" /> could marry the player: unwed, adult, of the
      ///   opposite sex (Bannerlord marries opposite-sex only), and within a believable age gap.
      ///   Shared by <see cref="Build" /> (listing) and the bridge (validating a grant).
      /// </summary>
      public static bool IsMarriageable(Hero player, Hero candidate)
      {
         if (player == null || candidate == null) return false;

         return MarriageEligibility.IsMarriageable(
            candidate.IsAlive, candidate.Spouse != null, candidate.IsFemale,
            player.IsFemale, candidate.Age, player.Age);
      }

      /// <summary>
      ///   Finds the candidate named <paramref name="name" /> among the NPC's clan members and
      ///   their own children (the NPC's house, broadly) — exact name match preferred. Null when
      ///   none match. Shared by the grant_blessing action and the marriage_consent bargain.
      /// </summary>
      public static Hero ResolveCandidate(Hero npc, string name)
      {
         if (npc == null || string.IsNullOrWhiteSpace(name)) return null;
         var pool = new List<Hero>();
         if (npc.Clan != null) pool.AddRange(npc.Clan.Heroes.Where(h => h is {IsAlive: true}));
         if (npc.Children != null)
            pool.AddRange(npc.Children.Where(h => h is {IsAlive: true} && !pool.Contains(h)));

         Hero exact = pool.FirstOrDefault(h => string.Equals(h.Name?.ToString(), name, StringComparison.OrdinalIgnoreCase));
         if (exact != null) return exact;

         // Minor (romance audit): a substring fallback ("Ana" -> "Anastasia") is useful, but when SEVERAL of the
         // house match it the old code silently blessed the first, i.e. the wrong child. An ambiguous partial now
         // resolves to no one rather than a guess, so a blessing never lands on a sibling by accident.
         List<Hero> partial = pool.Where(h => h.Name != null && h.Name.ToString().IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

         return partial.Count == 1
            ? partial[0]
            : null;
      }

      #region private

      /// <summary>Describes a candidate's place in the clan head's family, for prompt flavor.</summary>
      private static string DescribeKin(Hero leader, Hero c)
      {
         if (c == leader) return "yourself, unwed";
         if (leader.Children != null && leader.Children.Contains(c))
            return c.IsFemale
               ? "your daughter"
               : "your son";
         if (leader.Siblings != null && leader.Siblings.Contains(c))
            return c.IsFemale
               ? "your sister"
               : "your brother";
         if (leader.Father == c || leader.Mother == c)
            return c.IsFemale
               ? "your mother"
               : "your father";

         return c.IsFemale
            ? "your kinswoman"
            : "your kinsman";
      }

      #endregion
   }
}