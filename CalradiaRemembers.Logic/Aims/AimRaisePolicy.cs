// Code written by Gabriel Mailhot, 14/10/2026.
// Whether a lord may bring up his aim himself (tashmetu, 14/10/2026). His worry, in his words: "The real risk is a lord
// who brings up Lakonon in every conversation." Decided by Claude on Gabriel's delegation (14/10/2026, "je te laisse
// decider", to review): the lord may raise it once; if it still stands after a long while he may raise it ONE more time,
// then never again of his own accord (he still wants it, and answers if the player brings it up). Never toward a player
// he holds as an enemy (tashmetu's own line: "never if he is your enemy"), and not inside a captivity, whose scenes have
// their own rules.

namespace CalradiaRemembers.Logic.Aims
{
   /// <summary>What the lord may do with his aim right now.</summary>
   public enum AimRaiseVerdict
   {
      /// <summary>He may bring it up himself, when it fits.</summary>
      MayRaise,

      /// <summary>He raised it once and it is too soon to raise it again.</summary>
      TooSoon,

      /// <summary>He has raised it as often as he ever will; it stays his want, voiced only if the player asks.</summary>
      Spent,

      /// <summary>He holds the player as an enemy.</summary>
      Hostile,

      /// <summary>One of them is the other's captive.</summary>
      Captivity,

      /// <summary>The aim's last day is past.</summary>
      Expired,

      /// <summary>The registering mod closed this channel for the aim.</summary>
      ChannelClosed
   }

   /// <summary>The facts the raise decision reads.</summary>
   public sealed class AimRaiseFacts
   {
      /// <summary>How often he has raised it, by any channel.</summary>
      public int TimesRaised { get; init; }

      /// <summary>Days since he last raised it; ignored when never raised.</summary>
      public int DaysSinceLastRaised { get; init; }

      /// <summary>True when the lord holds the player as an enemy (personal regard in the enemy band, or their realms at war).</summary>
      public bool IsEnemyOfPlayer { get; init; }

      /// <summary>True when either one is the other's captive.</summary>
      public bool IsCaptivity { get; init; }

      /// <summary>True when the aim's last day is past.</summary>
      public bool IsExpired { get; init; }

      /// <summary>True when the registering mod allows the channel being asked about.</summary>
      public bool ChannelOpen { get; init; } = true;
   }

   /// <summary>Pure rule: may the lord raise his aim, unbidden, through a given channel, today.</summary>
   public static class AimRaisePolicy
   {
      /// <summary>
      ///   The most times a lord raises one aim of his own accord: once, then once more if it still stands after
      ///   <see cref="ReRaiseAfterDays" />. Tuning (Claude's call, to review).
      /// </summary>
      public const int MaxRaises = 2;

      /// <summary>How long after raising it a lord waits before raising the same aim one last time. Tuning (to review).</summary>
      public const int ReRaiseAfterDays = 60;

      /// <summary>Decides what the lord may do with his aim right now.</summary>
      public static AimRaiseVerdict Decide(AimRaiseFacts facts)
      {
         if (facts == null) return AimRaiseVerdict.Spent;
         if (facts.IsExpired) return AimRaiseVerdict.Expired;
         if (!facts.ChannelOpen) return AimRaiseVerdict.ChannelClosed;
         if (facts.IsCaptivity) return AimRaiseVerdict.Captivity;
         if (facts.IsEnemyOfPlayer) return AimRaiseVerdict.Hostile;
         if (facts.TimesRaised >= MaxRaises) return AimRaiseVerdict.Spent;
         if (facts.TimesRaised > 0 && facts.DaysSinceLastRaised < ReRaiseAfterDays) return AimRaiseVerdict.TooSoon;

         return AimRaiseVerdict.MayRaise;
      }
   }
}
