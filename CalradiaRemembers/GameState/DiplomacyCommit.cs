// Code written by Gabriel Mailhot, 14/11/2026.
// The one step every war, peace and change of realm CR makes goes through (VonLemberg's Bellum Civile bridge RFC,
// 14/11/2026, P0-B; Gabriel: "go pour les étapes 1 et 2"). It asks the registered authorities (CrDiplomacy), applies the
// change itself only when none takes it (DiplomacyCommitPolicy), and returns what happened, so each path tells the player
// the truth: a peace refused or awaiting a vote is never reported as signed.

#region

using System;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Diplomacy;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.GameState
{
   /// <summary>What passing the diplomacy door gave.</summary>
   internal sealed class DiplomacyOutcome
   {
      internal DiplomacyVerdict Verdict { get; set; }

      internal string AuthorityMessage { get; set; }

      /// <summary>The change happened (CR applied it, or the authority did).</summary>
      internal bool Happened => DiplomacyCommitPolicy.HasHappened(Verdict);

      /// <summary>What the player reads when it did not happen; null when it did.</summary>
      internal string NotHappenedLine(string change) => DiplomacyCommitPolicy.NotHappenedLine(Verdict, change, AuthorityMessage);
   }

   /// <summary>Asks the diplomacy authorities, then applies the change only when none took it. Game thread.</summary>
   internal static class DiplomacyCommit
   {
      /// <summary>War or peace between two factions.</summary>
      internal static DiplomacyOutcome Factions(DiplomacyChange kind, string source, IFaction a, IFaction b, Hero actor, Action apply)
      {
         CrDiplomacyRequest request = CrDiplomacy.NewRequest((CrDiplomacyKind) (int) kind, source);
         request.FactionAId = a?.StringId;
         request.FactionBId = b?.StringId;
         request.ActorHeroId = actor?.StringId;

         return Run(request, $"{a?.Name} / {b?.Name}", apply);
      }

      /// <summary>A clan joining or leaving a kingdom.</summary>
      internal static DiplomacyOutcome Realm(DiplomacyChange kind, string source, Clan clan, Kingdom kingdom, Hero actor, Action apply)
      {
         CrDiplomacyRequest request = CrDiplomacy.NewRequest((CrDiplomacyKind) (int) kind, source);
         request.ClanId = clan?.StringId;
         request.KingdomId = kingdom?.StringId;
         request.ActorHeroId = actor?.StringId;

         return Run(request, $"{clan?.Name} / {kingdom?.Name}", apply);
      }

      private static DiplomacyOutcome Run(CrDiplomacyRequest request, string label, Action apply)
      {
         (DiplomacyVerdict verdict, string message, string authority, string receipt) = CrDiplomacy.Ask(request);
         if (DiplomacyCommitPolicy.CrApplies(verdict)) apply?.Invoke();
         else ModLog.Info($"[DIPLOMACY] {request.Kind} ({request.Source}, {label}): {verdict} by '{authority}'"
                          + $"{(string.IsNullOrWhiteSpace(message) ? "" : ": " + message)}{(string.IsNullOrWhiteSpace(receipt) ? "" : " [receipt " + receipt + "]")}.");

         return new DiplomacyOutcome {Verdict = verdict, AuthorityMessage = message};
      }
   }
}
