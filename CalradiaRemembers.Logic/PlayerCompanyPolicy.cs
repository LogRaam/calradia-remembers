// Code written by Gabriel Mailhot, 03/10/2026.
// nerethi (Nexus) asked to invite someone into a conversation. Gabriel's design (03/10/2026): "Request Private" and
// "Invite someone" always available; Invite offers a companion, a prisoner or a lord travelling with the player;
// Request Private asks the main character again to continue alone, and is granted at once when every witness is
// someone accompanying the player. The player's company decides both.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>How someone travels with the player.</summary>
   public enum CompanyRole
   {
      /// <summary>A companion or kin in the player's party.</summary>
      Companion,

      /// <summary>A lord riding with the player (a retainer in the party, or an escort).</summary>
      TravellingLord,

      /// <summary>A hero held in the player's prison roster.</summary>
      Prisoner
   }

   /// <summary>One person of the player's company.</summary>
   public sealed class CompanyMember
   {
      public string Id { get; init; } = "";
      public string Name { get; init; } = "";
      public CompanyRole Role { get; init; }
   }

   /// <summary>A witness in the room, as the privacy rule sees them.</summary>
   public sealed class PresentWitness
   {
      /// <summary>The witness's hero id; null for someone without one (a guard, a servant).</summary>
      public string HeroId { get; init; }

      /// <summary>Seated as the player's companion.</summary>
      public bool IsPlayerCompanion { get; init; }

      /// <summary>Brought in as one of the player's prisoners.</summary>
      public bool IsBroughtCaptive { get; init; }
   }

   /// <summary>Who travels with the player, for inviting and for clearing the room. Pure.</summary>
   public static class PlayerCompanyPolicy
   {
      /// <summary>
      ///   The company members who may be invited: not the one being spoken to, not anyone already present, and no
      ///   prisoner where prisoners are not welcome (a tale's scene, unless it allows them: fkasad, 07/10/2026).
      /// </summary>
      public static IReadOnlyList<CompanyMember> InviteCandidates(IEnumerable<CompanyMember> company, string mainNpcId, IEnumerable<string> presentIds,
                                                                  bool prisonersWelcome = true)
      {
         var present = new HashSet<string>(presentIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);

         return (company ?? Enumerable.Empty<CompanyMember>())
                .Where(m => m != null && !string.IsNullOrEmpty(m.Id) && m.Id != mainNpcId && !present.Contains(m.Id))
                .Where(m => prisonersWelcome || m.Role != CompanyRole.Prisoner)
                .GroupBy(m => m.Id).Select(g => g.First())
                .ToList();
      }

      /// <summary>True when every witness in the room is the player's own: seated as such, or one of the company.</summary>
      public static bool AllAreThePlayers(IEnumerable<PresentWitness> witnesses, ISet<string> companyIds)
      {
         List<PresentWitness> all = (witnesses ?? Enumerable.Empty<PresentWitness>()).Where(w => w != null).ToList();

         return all.Count > 0 && all.All(w => w.IsPlayerCompanion || w.IsBroughtCaptive
                                              || (w.HeroId != null && companyIds != null && companyIds.Contains(w.HeroId)));
      }
   }

   /// <summary>What a request for privacy does.</summary>
   public enum PrivateAudienceVerdict
   {
      /// <summary>Nothing to ask: no one else in the room, a request already waiting, or a scene that has no room to clear.</summary>
      Unavailable,

      /// <summary>The player commands the room: everyone else leaves at once.</summary>
      ClearAtOnce,

      /// <summary>The character decides, in the conversation.</summary>
      AskTheNpc
   }

   /// <summary>The state a request for privacy is judged on.</summary>
   public sealed class PrivateAudienceFacts
   {
      /// <summary>The conversation allows a request at all (not a captive scene, not a briefing).</summary>
      public bool Allowed { get; init; }

      /// <summary>Someone besides the main character is in the room.</summary>
      public bool AnyWitness { get; init; }

      /// <summary>The last request is still waiting for the character's answer.</summary>
      public bool RequestPending { get; init; }

      /// <summary>The main character is the player's prisoner.</summary>
      public bool NpcIsPlayersPrisoner { get; init; }

      /// <summary>Every witness is someone accompanying the player.</summary>
      public bool AllWitnessesAreThePlayers { get; init; }
   }

   /// <summary>Decides a request for privacy. Pure.</summary>
   public static class PrivateAudiencePolicy
   {
      /// <summary>The player's own people, or the player's prisoner, leave the decision to the player.</summary>
      public static PrivateAudienceVerdict Decide(PrivateAudienceFacts f)
      {
         if (f == null || !f.Allowed || !f.AnyWitness || f.RequestPending) return PrivateAudienceVerdict.Unavailable;

         return f.NpcIsPlayersPrisoner || f.AllWitnessesAreThePlayers ? PrivateAudienceVerdict.ClearAtOnce : PrivateAudienceVerdict.AskTheNpc;
      }
   }
}
