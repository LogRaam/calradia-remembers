// Code written by Gabriel Mailhot, 14/11/2026.
// Extension Surface: the diplomacy authority door. VonLemberg's team (Bellum Civile bridge RFC, 14/11/2026, P0-B): "We would
// like an optional pre-execution delegation/interception mechanism for selected diplomacy-related actions", with "PassThrough
// / Rejected / Pending / Applied", "optional and non-breaking", "one clean interception point instead of requiring multiple
// Harmony patches". Gabriel: "go pour les étapes 1 et 2". Every war, peace and change of realm CR makes itself asks the
// registered authorities first (DiplomacyCommit); with none registered, nothing changes. CR names no mod. Since 2.8.10.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Diplomacy;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>A diplomatic change CR is about to make. Since 2.8.10.</summary>
   public enum CrDiplomacyKind
   {
      /// <summary>Faction A declares war on faction B.</summary>
      DeclareWar = 0,

      /// <summary>Factions A and B make peace.</summary>
      MakePeace = 1,

      /// <summary>A clan (ClanId) joins a kingdom (KingdomId) as a vassal.</summary>
      ClanJoinsKingdom = 2,

      /// <summary>A clan joins a kingdom as a mercenary.</summary>
      ClanJoinsAsMercenary = 3,

      /// <summary>A clan leaves its kingdom.</summary>
      ClanLeavesKingdom = 4,

      /// <summary>A clan ends its mercenary service.</summary>
      ClanLeavesMercenaryService = 5
   }

   /// <summary>What an authority answers. Since 2.8.10.</summary>
   public enum CrDiplomacyVerdict
   {
      /// <summary>No opinion: CR makes the change itself, as it always did.</summary>
      PassThrough = 0,

      /// <summary>Not allowed: CR makes nothing and tells the player why.</summary>
      Rejected = 1,

      /// <summary>Put to your own process (a treaty, a vote): CR makes nothing and says it has not happened yet.</summary>
      Pending = 2,

      /// <summary>You made the change yourself: CR does not make it again.</summary>
      Applied = 3
   }

   /// <summary>The change CR asks about. Ids are the game's StringIds. Since 2.8.10.</summary>
   public sealed class CrDiplomacyRequest
   {
      /// <summary>A new id for this request.</summary>
      public string RequestId { get; set; }

      /// <summary>What change.</summary>
      public CrDiplomacyKind Kind { get; set; }

      /// <summary>Which CR path asks: "mediate_peace", "council", "scenario", "arrest", "join_clan", "join_as_vassal", "join_as_mercenary", "leave_mercenary", "union_rivals".</summary>
      public string Source { get; set; }

      /// <summary>For war and peace: the first faction (the one declaring, for a war).</summary>
      public string FactionAId { get; set; }

      /// <summary>For war and peace: the second faction.</summary>
      public string FactionBId { get; set; }

      /// <summary>For a change of realm: the clan.</summary>
      public string ClanId { get; set; }

      /// <summary>For a change of realm: the kingdom joined or left.</summary>
      public string KingdomId { get; set; }

      /// <summary>The character behind it (the lord who brokered, the ruler asked), when there is one.</summary>
      public string ActorHeroId { get; set; }

      /// <summary>The game day.</summary>
      public int GameDay { get; set; }

      /// <summary>The campaign's unique id, so an answer is never applied to another save.</summary>
      public string SaveId { get; set; }
   }

   /// <summary>Your answer. Since 2.8.10.</summary>
   public sealed class CrDiplomacyAnswer
   {
      /// <summary>The verdict.</summary>
      public CrDiplomacyVerdict Verdict { get; set; }

      /// <summary>Why, in a few words the player reads ("the council has not voted it"), for Rejected and Pending. Optional.</summary>
      public string Message { get; set; }

      /// <summary>Your own receipt for the decision (logged by CR), optional.</summary>
      public string Receipt { get; set; }
   }

   /// <summary>The door through which another mod becomes the authority over the wars, peaces and changes of realm CR makes. Since 2.8.10.</summary>
   public static class CrDiplomacy
   {
      private static readonly SortedDictionary<string, Func<CrDiplomacyRequest, CrDiplomacyAnswer>> Authorities = new(StringComparer.Ordinal);
      private static readonly HashSet<string> Faulted = new(StringComparer.Ordinal);

      /// <summary>
      ///   Registers your authority. Before CR declares a war, makes a peace or moves a clan between realms (a lord's mediation,
      ///   the council, a scenario, an arrest, the player joining or leaving a realm through a character), it calls every
      ///   registered authority in the order of their names; the first answer that is not PassThrough decides. Rejected and
      ///   Pending: CR makes nothing and tells the player the change did not happen, or has not yet. Applied: you made it, CR
      ///   does not. A null answer or one that throws counts as PassThrough (logged once). Called on the game thread; answer
      ///   at once (Pending, then your own process, when you need time). Registering the same <paramref name="sourceMod" />
      ///   again replaces it. Changes the game makes on its own (its kingdom votes, AI lords) never pass here.
      /// </summary>
      public static void RegisterAuthority(string sourceMod, Func<CrDiplomacyRequest, CrDiplomacyAnswer> authority)
      {
         if (string.IsNullOrWhiteSpace(sourceMod) || authority == null) return;

         lock (Authorities)
         {
            Authorities[sourceMod.Trim()] = authority;
            Faulted.Remove(sourceMod.Trim());
         }

         ModLog.Info($"[DIPLOMACY API] authority registered by '{sourceMod.Trim()}'");
      }

      /// <summary>Withdraws your authority. Returns whether one was there.</summary>
      public static bool UnregisterAuthority(string sourceMod)
      {
         if (sourceMod == null) return false;

         lock (Authorities) return Authorities.Remove(sourceMod.Trim());
      }

      /// <summary>Asks every authority, in name order, until one has an opinion. Internal: DiplomacyCommit is the caller.</summary>
      internal static (DiplomacyVerdict Verdict, string Message, string Authority, string Receipt) Ask(CrDiplomacyRequest request)
      {
         List<KeyValuePair<string, Func<CrDiplomacyRequest, CrDiplomacyAnswer>>> authorities;
         lock (Authorities)
         {
            if (Authorities.Count == 0) return (DiplomacyVerdict.PassThrough, null, null, null);

            authorities = Authorities.ToList();
         }

         foreach (KeyValuePair<string, Func<CrDiplomacyRequest, CrDiplomacyAnswer>> authority in authorities)
         {
            CrDiplomacyAnswer answer;
            try { answer = authority.Value(request); }
            catch (Exception ex)
            {
               bool first;
               lock (Authorities) first = Faulted.Add(authority.Key);
               if (first) ModLog.Error($"[DIPLOMACY API] the authority of '{authority.Key}' threw and is read as PassThrough (logged once): {ex.GetType().Name}: {ex.Message}");
               continue;
            }

            var verdict = (DiplomacyVerdict) (int) (answer?.Verdict ?? CrDiplomacyVerdict.PassThrough);
            if (verdict != DiplomacyVerdict.PassThrough) return (verdict, answer?.Message, authority.Key, answer?.Receipt);
         }

         return (DiplomacyVerdict.PassThrough, null, null, null);
      }

      /// <summary>A request with its id, day and save filled in.</summary>
      internal static CrDiplomacyRequest NewRequest(CrDiplomacyKind kind, string source)
         => new() {
            RequestId = Guid.NewGuid().ToString("N"), Kind = kind, Source = source,
            GameDay = Campaign.Current == null ? 0 : (int) CampaignTime.Now.ToDays, SaveId = Campaign.Current?.UniqueGameId
         };
   }
}
