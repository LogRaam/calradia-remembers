// Code written by Gabriel Mailhot, 19/11/2026.
// How a living parent reads in a character's family line. Eisendrache2 (Nexus, 19/11/2026, Gemma 3 12B): "Many lords
// claim, that their fathers have died at the battle at Pendraic. But more than often enough, their fathers are still
// alive and well!" The family line said "Father: Name (age 61)": a dead parent was marked "the late", a living one by
// nothing, so a small model could miss that he lives. Gabriel (19/11/2026) asked that the character be given whether
// each parent lives. If this is wrong, a lord buries a father who still rides.

namespace CalradiaRemembers.Logic
{
   /// <summary>The words for a living parent in a family line. Pure.</summary>
   public static class ParentLineText
   {
      /// <summary>"Name (alive, age 61)"; a dead parent keeps "the late Name" from the death describer.</summary>
      public static string Living(string name, int age) => $"{name} (alive, age {age})";
   }
}
