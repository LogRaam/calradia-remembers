// Code written by Gabriel Mailhot, 10/11/2026.
// How readily a character believes the player (Gabriel, 10/11/2026, reading AI Influence 7.0.0's "naivety", which decides
// how strictly a character notices lies: "vas-y avec les 3 dans l'ordre", the third). One rule served everyone: the
// behaviour guidelines tell every lord "to be guarded is to not be naive... words are cheap", and WHAT THEY SAY THEY HAVE
// DONE IS A CLAIM holds every reward to proof. So a trusting young lady and a scheming merchant weighed the player's
// word alike. Each character is now judged shrewd, ordinary or trusting from who they are: the calculating, the dishonest
// (a liar expects lies), merchants, gang leaders and rulers are harder to fool; the honourable (who judge others by
// themselves), the generous and the young are easier. A character the player has already broken faith with (a broken
// oath or pact, a manipulation exposed, a task taken and failed, still held against them) is shrewd toward them,
// whatever their nature: once burned. Nothing is saved; the ordinary say nothing, so most prompts are unchanged.
// Belief only: what a character GIVES or DOES on the player's word alone still follows the claims rule, so a trusting
// lord is moved by a sad tale and still pays no reward for a deed nobody saw.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Grudges;

#endregion

namespace CalradiaRemembers.Logic.Character
{
   /// <summary>How readily a character believes the player.</summary>
   public enum Credulity
   {
      Ordinary,
      Shrewd,
      Trusting
   }

   /// <summary>Who is judged.</summary>
   public sealed class CredulityFacts
   {
      /// <summary>The hero's id, the seed of the small personal lean.</summary>
      public string HeroId { get; init; }

      /// <summary>Their traits (each -2..2).</summary>
      public RomanticTraits Traits { get; init; } = new();

      /// <summary>Their station key (SocialStationPolicy: "ruler", "merchant"...), or null.</summary>
      public string Station { get; init; }

      /// <summary>Their age in years.</summary>
      public int Age { get; init; }

      /// <summary>The sources of the live grudges they hold against the player.</summary>
      public IReadOnlyList<GrudgeSource> GrudgesAgainstPlayer { get; init; } = Array.Empty<GrudgeSource>();
   }

   /// <summary>Judges and words how readily a character believes the player. Pure.</summary>
   public static class CredulityPolicy
   {
      /// <summary>The grudges that mean the player has broken faith with this character before.</summary>
      public static readonly GrudgeSource[] BrokenFaith = {GrudgeSource.BrokenOath, GrudgeSource.PactBroken, GrudgeSource.SwayExposed, GrudgeSource.FailedQuest};

      /// <summary>The player broke faith with them before, and they still hold it.</summary>
      public static bool Burned(CredulityFacts f) => f?.GrudgesAgainstPlayer?.Any(s => BrokenFaith.Contains(s)) == true;

      /// <summary>How readily they believe the player.</summary>
      public static Credulity Judge(CredulityFacts f)
      {
         if (f == null) return Credulity.Ordinary;
         if (Burned(f)) return Credulity.Shrewd;

         int lean = new Random(Romance.IntimateActPolicy.StableSeed((f.HeroId ?? "") + "|credulity")).Next(4) switch {
            0 => -1,
            3 => 1,
            _ => 0
         };

         return FromScore(Score(f.Traits, f.Station, f.Age) + lean);
      }

      /// <summary>
      ///   The leaning toward belief before the personal lean: each step of calculation and dishonour away from it,
      ///   honour and generosity toward it, a trading or ruling station away, youth toward.
      /// </summary>
      public static int Score(RomanticTraits t, string station, int age)
      {
         t ??= new RomanticTraits();
         int score = -t.Calculating;
         if (t.Honor > 0) score++;
         if (t.Honor < 0) score--;
         if (t.Generosity > 0) score++;
         if (station is "merchant" or "gangleader" or "ruler") score--;
         if (age < 20) score++;

         return score;
      }

      /// <summary>Two steps away from belief is shrewd, two toward it trusting.</summary>
      public static Credulity FromScore(int score) => score <= -2 ? Credulity.Shrewd : score >= 2 ? Credulity.Trusting : Credulity.Ordinary;

      /// <summary>Their nature in the second person, for the prompt; null for the ordinary, who need no word.</summary>
      public static string Render(Credulity c, bool burned)
      {
         if (burned)
            return "The player has broken faith with you before, and you have not forgotten it: you weigh every word they say against what you know, "
                   + "notice when a story shifts, and do not take a promise or an account of theirs on trust again.";

         return c switch {
            Credulity.Shrewd => "You are hard to fool. You weigh what the player tells you against what you know and have heard, notice when a story shifts "
                                + "or does not add up, and ask the one question that would expose a lie. Flattery makes you more careful, not less.",
            Credulity.Trusting => "You are trusting, perhaps too trusting. You take the player's word about themselves, their feelings and their stories "
                                  + "at face value unless it plainly contradicts what you know, and a warm word or a sad tale moves you easily.",
            _ => null
         };
      }
   }
}
