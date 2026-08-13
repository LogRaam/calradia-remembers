// Code written by Gabriel Mailhot, 13/08/2026.

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   Maps a Bannerlord trait level (the small signed integer <c>hero.GetTraitLevel</c> returns, roughly
   ///   -2..+2) to the plain band word ("low"/"moderate"/"high") the extractor's context digest uses to describe
   ///   a character's disposition (for example "Honour high, Mercy low, Calculating moderate"). Pure so the
   ///   console digest and any test read the same word for the same level.
   /// </summary>
   public static class TraitBand
   {
      /// <summary>
      ///   "low" for a negative level (the trait's opposite lean dominates), "high" for a positive level, and
      ///   "moderate" for zero (no marked lean either way).
      /// </summary>
      public static string Describe(int level)
      {
         if (level < 0) return "low";
         if (level > 0) return "high";

         return "moderate";
      }
   }
}
