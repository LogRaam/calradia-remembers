// Code written by Gabriel Mailhot, 16/10/2026.
// Extension Surface: the match door. tashmetu (Nexus, 16/10/2026): a head of house agreed in the chat to give his
// kinswoman to a lord of the player's house, as another mod's matchmaker had proposed, and nothing happened: arrange_match
// is taught only for a match a character asked of the player by letter. Gabriel approved a door the same day: another mod
// registers a pending match (suitor, bride, the one who decides), the decider is taught arrange_match for it, and CR's
// own law settles it (CourtActionResolver.TryArrangeThirdPartyMatch: the game's legality, no captivity or war between
// the houses, her heart, her family's consent). The door refuses at once what the law would refuse outright, so the
// decider is never taught a match the game then refutes. In memory only: register again when a game is loaded.

#region

using System;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Marriage;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>Lets another mod hand the player a match to broker; CR teaches it to the one who decides and settles it.</summary>
   public static class CrMatches
   {
      internal static readonly PendingMatchBook Book = new();

      /// <summary>
      ///   Fired when the decider's consent was asked in the chat for a registered match: wed, or refused with the reason
      ///   (her heart, her family, a war between the houses...). A refused match stays registered and may be asked again;
      ///   a wed one is dropped.
      /// </summary>
      public static event Action<CrMatchOutcome> Settled;

      /// <summary>
      ///   Registers a match for the player to broker: <paramref name="deciderId" /> (the bride herself, or the head of her
      ///   house or a parent) will be taught arrange_match for it in the chat. Refused at once, keeping nothing, when an id
      ///   names no one, the two are one person, the game would not wed them, or the decider has no say over her hand.
      ///   Registering the same couple again replaces the earlier entry. Not saved: register again when a game is loaded.
      ///   The ids are named on <see cref="CrMatchRequest" />, so a suitor and a bride can never be passed the wrong way round.
      /// </summary>
      public static MatchRegistrationResult Register(CrMatchRequest request)
      {
         try
         {
            string suitorId = request?.SuitorId, brideId = request?.BrideId, deciderId = request?.DeciderId, sourceMod = request?.SourceMod;
            Hero suitor = Find(suitorId), bride = Find(brideId), decider = Find(deciderId);
            bool known = suitor != null && bride != null && decider != null;
            MatchRegistrationResult result = PendingMatchBook.Judge(new MatchRegistrationFacts {
               AllKnown = known,
               SamePerson = known && suitor == bride,
               PlayerIsOneOfThem = known && (suitor == Hero.MainHero || bride == Hero.MainHero),
               LawfulCouple = known && Campaign.Current?.Models?.MarriageModel?.IsCoupleSuitableForMarriage(suitor, bride) == true,
               DeciderHasSay = known && (decider == bride || MarriageProspectResolver.HasAuthority(decider, bride))
            });

            ModLog.Info($"[MATCH API] {sourceMod ?? "a mod"} registered {suitor?.Name?.ToString() ?? suitorId} for {bride?.Name?.ToString() ?? brideId}, "
                        + $"decided by {decider?.Name?.ToString() ?? deciderId}: {result}.");
            if (result == MatchRegistrationResult.Registered)
               Book.Add(new PendingMatchEntry {SuitorId = suitorId, BrideId = brideId, DeciderId = deciderId, Source = sourceMod});

            return result;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MATCH API] Register failed: {ex.GetType().Name}: {ex.Message}");

            return MatchRegistrationResult.RejectedUnknown;
         }
      }

      /// <summary>Withdraws a registered match; false when there was none.</summary>
      public static bool Withdraw(string suitorId, string brideId) => Book.Remove(suitorId, brideId);

      /// <summary>
      ///   The registered match this decider has the say over, still standing; matches where one of the two died or was
      ///   wed meanwhile, or whose decider no longer has the say over her (a new head of house), are dropped first, so a
      ///   decider is never taught a match the law would then refuse him. Null when none.
      /// </summary>
      internal static PendingMatchEntry ForDecider(Hero decider)
      {
         if (decider == null) return null;
         Book.Prune(e => Find(e.SuitorId) is {IsDead: false, Spouse: null} && Find(e.BrideId) is {IsDead: false, Spouse: null} bride
                         && Find(e.DeciderId) is {IsDead: false} who && (who == bride || MarriageProspectResolver.HasAuthority(who, bride)));

         return Book.ForDecider(decider.StringId);
      }

      internal static void RaiseSettled(CrMatchOutcome outcome)
      {
         if (Settled == null || outcome == null) return;

         foreach (Delegate handler in Settled.GetInvocationList())
            try { ((Action<CrMatchOutcome>) handler)(outcome); }
            catch (Exception ex)
            {
               ModLog.Error($"[MATCH API] a subscriber threw and was isolated: {ex.GetType().Name}: {ex.Message}");
            }
      }

      private static Hero Find(string id) => string.IsNullOrWhiteSpace(id) ? null : Hero.FindFirst(h => h.StringId == id);
   }

   /// <summary>A match offered to <see cref="CrMatches.Register" />, its people named by hero StringId.</summary>
   public sealed class CrMatchRequest
   {
      /// <summary>The suitor, who would wed the bride.</summary>
      public string SuitorId { get; set; }

      /// <summary>The bride.</summary>
      public string BrideId { get; set; }

      /// <summary>The one who decides: the bride herself, or the head of her house or a parent.</summary>
      public string DeciderId { get; set; }

      /// <summary>Your mod's name, for the log and <see cref="CrMatchOutcome.Source" />.</summary>
      public string SourceMod { get; set; }
   }

   /// <summary>What came of a registered match when its decider was asked in the chat.</summary>
   public sealed class CrMatchOutcome
   {
      internal CrMatchOutcome(PendingMatchEntry entry, bool wed, string reason)
      {
         SuitorId = entry?.SuitorId;
         BrideId = entry?.BrideId;
         DeciderId = entry?.DeciderId;
         Source = entry?.Source;
         Wed = wed;
         Reason = reason ?? "";
      }

      /// <summary>The suitor's hero id.</summary>
      public string SuitorId { get; }

      /// <summary>The bride's hero id.</summary>
      public string BrideId { get; }

      /// <summary>The decider's hero id.</summary>
      public string DeciderId { get; }

      /// <summary>The mod that registered the match.</summary>
      public string Source { get; }

      /// <summary>True when the game wed them.</summary>
      public bool Wed { get; }

      /// <summary>Why not, in the words the player read; empty when wed.</summary>
      public string Reason { get; }
   }
}
