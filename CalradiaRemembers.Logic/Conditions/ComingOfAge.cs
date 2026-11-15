// Code written by Gabriel Mailhot, 14/11/2026.
// The age a hero comes of age is the game's own (AgeModel.HeroComesOfAge); the mod read it in five places, each writing
// the engine's default, 18, as its own fallback for when the model cannot be read. The glue-decisions map (14/11/2026)
// showed one of them as a bare comparison. One home for the fallback, so the five cannot drift apart.

namespace CalradiaRemembers.Logic.Conditions
{
   /// <summary>The coming of age, when the game cannot be asked.</summary>
   public static class ComingOfAge
   {
      /// <summary>The engine's default AgeModel.HeroComesOfAge, used only when the model cannot be read.</summary>
      public const int EngineDefault = 18;

      /// <summary>The game's coming of age, or <see cref="EngineDefault" /> when it gave none.</summary>
      public static int YearsOr(int? fromTheGame) => fromTheGame ?? EngineDefault;
   }
}
