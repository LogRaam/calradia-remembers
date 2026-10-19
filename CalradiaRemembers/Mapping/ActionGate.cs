// Code written by Gabriel Mailhot, 30/09/2026.
// Reads the facts NpcActionGate (pure, tested) needs from the live game: every path where a character acts or is
// placed before the player asks here, naming its kind, so docs/maps/actions.md can list each one and the suite
// can tell when a kind is declared but never asked (Gabriel, 30/09/2026, after a baby son asked to discuss warfare).

#region

using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>The live-game side of the one initiative gate.</summary>
   public static class ActionGate
   {
      /// <summary>
      ///   True when <paramref name="hero" /> may take this initiative toward the player. With <paramref name="log" />, a
      ///   refused one-off initiative says why in the log; scans over many heroes leave it off.
      /// </summary>
      public static bool Allows(Hero hero, ActionKind kind, bool log = false)
      {
         ActionRefusal refusal = Check(hero, kind);
         if (refusal == ActionRefusal.None) return true;

         if (log) ModLog.Info($"[ACTION GATE] {kind} by {hero?.Name?.ToString() ?? "no one"} does not happen: {refusal}.");

         return false;
      }

      /// <summary>Why this hero may not take this initiative, or None. The kind is named so the initiatives map lists the site.</summary>
      public static ActionRefusal Check(Hero hero, ActionKind kind) => NpcActionGate.Check(Facts(hero), kind);

      /// <summary>The engine facts the gate reads. Age is Hero.IsChild, never inferred from rank (IsLord is an occupation).</summary>
      internal static ActorFacts Facts(Hero hero)
         => hero == null
            ? new ActorFacts {Exists = false}
            : new ActorFacts {
               IsAlive = hero.IsAlive, IsChild = hero.IsChild, IsPrisoner = hero.IsPrisoner, IsPlayer = hero == Hero.MainHero,
               IsDisabled = !HeroPresence.IsInTheWorld(hero) && hero.IsAlive, Age = (int) hero.Age,
               TutorialRestrictsInteraction = StoryModeTutorial.RestrictsInteraction()
            };
   }
}
