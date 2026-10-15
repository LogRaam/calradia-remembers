// Code written by Gabriel Mailhot, 14/10/2026.
// The door an aim passes through (tashmetu, 14/10/2026; built on the Feed.Record and MissionBoard pattern). Text is
// bounded (the save-blob guard), identity is required, a lord holds ONE standing aim at a time (a lord who wants five
// things at once wants nothing in particular, and the prompt would carry all five), and one source may hold only so
// many standing aims (tashmetu's own design: "most get nothing"; the cap keeps a source honest to it and bounds the
// blob).

namespace CalradiaRemembers.Logic.Aims
{
   /// <summary>What became of a registration.</summary>
   public enum AimAdmission
   {
      /// <summary>A new aim now stands.</summary>
      Accepted,

      /// <summary>An aim with the same id stood and has been replaced; whether it was raised is kept.</summary>
      Updated,

      /// <summary>The id, the lord or the source tag was missing.</summary>
      MissingIdentity,

      /// <summary>The want was empty: an aim that says nothing is refused.</summary>
      EmptyWant,

      /// <summary>The lord already holds another aim (any source). The registering mod retracts it first, or waits.</summary>
      LordAlreadyHasAnAim,

      /// <summary>This source already holds the most standing aims it may.</summary>
      SourceAtCapacity,

      /// <summary>The aim's last day is already past.</summary>
      AlreadyExpired
   }

   /// <summary>Normalisation and admission rules for an aim registered by another mod.</summary>
   public static class AimRules
   {
      /// <summary>The longest a want may be: room for tashmetu's Lakonon example, not for an essay. Tuning.</summary>
      public const int MaxWantChars = 400;

      /// <summary>The longest the player's part may be: one line. Tuning.</summary>
      public const int MaxPlayerPartChars = 200;

      /// <summary>The most standing aims one source may hold at once ("most get nothing"). Tuning.</summary>
      public const int MaxStandingAimsPerSource = 40;

      /// <summary>Trims and bounds a want; null or whitespace returns null (the registration is refused).</summary>
      public static string BoundWant(string want) => Bound(want, MaxWantChars);

      /// <summary>Trims and bounds the player's part; null or whitespace returns null (the aim simply has none).</summary>
      public static string BoundPlayerPart(string part) => Bound(part, MaxPlayerPartChars);

      /// <summary>True when the aim names its own id, its lord and its source.</summary>
      public static bool HasRequiredIdentity(StandingAim aim)
         => aim != null
            && !string.IsNullOrWhiteSpace(aim.Id)
            && !string.IsNullOrWhiteSpace(aim.NpcId)
            && !string.IsNullOrWhiteSpace(aim.SourceMod);

      private static string Bound(string text, int max)
      {
         if (string.IsNullOrWhiteSpace(text)) return null;

         string trimmed = text.Trim();

         return trimmed.Length <= max
            ? trimmed
            : trimmed.Substring(0, max);
      }
   }
}
