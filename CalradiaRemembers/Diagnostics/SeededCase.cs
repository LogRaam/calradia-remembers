// Code written by Gabriel Mailhot, 12/09/2026.
// What a seeded character was given, in a form the automated run can turn into the right question.
//
// It exists because Gabriel asked for the obvious next thing: "est-ce que tu peux automatiser les tests avec
// cr.seed_knowledge? Plutot que j'aie a avoir une conversation en jeu avec eux." Seeding a situation and then
// asking a human to go and talk to six lords is half a tool.

#region

using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Diagnostics
{
   /// <summary>
   ///   The situations the seeder can create. Each one exists because it makes a DIFFERENT pack, or a
   ///   different half of one, observable - a secret and an open bond are the two ends of the discretion axis
   ///   and neither shows anything on its own.
   /// </summary>
   public enum SeededKind
   {
      /// <summary>A love he keeps hidden. The pack must refuse to render it however he is asked.</summary>
      SecretLove,

      /// <summary>A love he does not hide. Surfaces in private, to somebody he thinks well of.</summary>
      OpenLove,

      /// <summary>A grievance against a third party. Ordinary talk, and his to raise with anyone.</summary>
      OpenGrievance,

      /// <summary>Barely on his feet. The case with a consequence: he must refuse to ride out.</summary>
      GrievousWound,

      /// <summary>Knocked about. He should mention it and still be able.</summary>
      LightWound
   }

   /// <summary>One seeded character and what was done to them, so the run knows what to ask about.</summary>
   public sealed class SeededCase
   {
      /// <summary>Who was seeded.</summary>
      public Hero Hero { get; set; }

      /// <summary>What they were given.</summary>
      public SeededKind Kind { get; set; }

      /// <summary>
      ///   The other person in it - the lover, or the one resented - so a probe can check the reply against a
      ///   NAME rather than against a feeling. Null for the wounds, which are about nobody.
      /// </summary>
      public string CounterpartName { get; set; }

      /// <summary>Their hit points before the seeder touched them, so `clear` puts back what was there.</summary>
      public int HitPointsBefore { get; set; }
   }
}
