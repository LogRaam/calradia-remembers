// Code written by Gabriel Mailhot, 10/11/2026.
// A voice of their own for every character (Gabriel, 10/11/2026, reading AI Influence 7.0.0's archetypes and speech
// habits: "vas-y avec les 3 dans l'ordre", the first being a voice per character). Nothing said HOW a lord speaks: the
// persona says what they are like, the station how they treat the player, and the MCM style how the whole is written, so
// two lords of one temperament sounded alike, and kurushimi00 (Nexus, 01/11/2026) heard characters "recite" the same way.
// Each character is given, from their id, one habit of speech, how freely they curse and how their piety shows. Nothing is
// saved: the same hero always draws the same habit (the habit is drawn from their station and age only, so a trait moving
// never changes it), while cursing and piety follow the traits and so may shift as a character grows. No faith is named:
// Calradia has none of its own, so a believer speaks by "what their people hold holy" and the world supplies the rest.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Character
{
   /// <summary>Who a voice is drawn for.</summary>
   public sealed class CharacterVoiceFacts
   {
      /// <summary>The hero's id, the seed of the draw.</summary>
      public string HeroId { get; init; }

      /// <summary>Their traits (each -2..2).</summary>
      public RomanticTraits Traits { get; init; } = new();

      /// <summary>Their station key (SocialStationPolicy: "ruler", "lord", "preacher"...), or null.</summary>
      public string Station { get; init; }

      /// <summary>Their age in years.</summary>
      public int Age { get; init; }
   }

   /// <summary>How freely a character curses.</summary>
   public enum Coarseness
   {
      Never,
      Rarely,
      Often
   }

   /// <summary>How a character's piety shows.</summary>
   public enum Piety
   {
      Ordinary,
      Devout,
      ForShow
   }

   /// <summary>A character's voice, as drawn.</summary>
   public sealed class CharacterVoice
   {
      /// <summary>Their habit of speech, one sentence addressed to them.</summary>
      public string Habit { get; init; }

      /// <summary>How freely they curse.</summary>
      public Coarseness Coarseness { get; init; }

      /// <summary>How their piety shows.</summary>
      public Piety Piety { get; init; }
   }

   /// <summary>Draws and words a character's voice. Pure.</summary>
   public static class CharacterVoicePolicy
   {
      private const int Everyone = 0, Young = 1, Elder = 2;

      private sealed class HabitEntry
      {
         public string Text;
         public string[] Stations;
         public int Age;
      }

      private static readonly string[] Nobles = {SocialStation.Ruler, SocialStation.Lord, SocialStation.Noble};
      private static readonly string[] Swords = {SocialStation.Ruler, SocialStation.Lord, SocialStation.Noble, SocialStation.Companion, SocialStation.Wanderer};
      private static readonly string[] Traders = {"merchant", "artisan"};
      private static readonly string[] LandFolk = {"headman", "ruralnotable"};

      private static readonly HabitEntry[] Habits = {
         H("You often answer a question with a question of your own before you give your answer."),
         H("You lean on the old sayings of your people, one now and then, and trust them more than argument."),
         H("You speak in short, clipped sentences and let silence say the rest."),
         H("You come at a point sideways, through a story or a comparison, before you reach it."),
         H("You count things off as you say them: first this, second that, even feelings."),
         H("You are given to dry understatement: a disaster is \"unfortunate\", a slaughter \"untidy\"."),
         H("You leave a hard sentence unfinished and let the other finish it in their own head."),
         H("You repeat the other's last few words back to them while you weigh them."),
         H("You correct yourself mid-sentence, hunting for the exact word."),
         H("You name things precisely: the weight, the price, the number of days."),
         H("You laugh at your own misfortunes before anyone else can."),
         H("You speak of the weather, the roads or the harvest before you come to anything that matters."),
         H("You use the other's name often, as if to hold them by it."),
         H("You put things as wagers: \"I would wager\", \"odds are\"."),
         H("You grumble, mildly and constantly, about small things: your knees, the cold, the price of salt."),
         H("You speak slowly and plainly, in small words, and distrust fine speech."),
         H("You love a well-turned phrase and are visibly pleased when you find one."),
         H("You call yourself a simple soul just before you say something shrewd."),
         H("You end your points with a short question that wants agreement: \"is it not?\", \"yes?\"."),
         H("You tell the other what they are feeling before they say it, rightly or not."),
         H("You have one pet word or phrase you lean on (choose it once and keep it), and catch yourself overusing it."),
         H("You exaggerate by habit: everything is the finest, the worst, the biggest you have ever seen."),
         H("You keep promising to be brief, and are not."),
         H("You turn every compliment aside with a joke."),
         H("You ask leave before anything blunt: \"may I speak plainly?\", and then do."),
         H("You frame things as debts: who owes whom, and what, and for how long."),
         H("You lower your voice for anything that matters, as if the walls had ears."),
         H("You speak with a soldier's bluntness: orders rather than requests, even in friendship.", Swords),
         H("You reach for the hunt and the horse to describe everything.", Swords),
         H("You speak of your house and your forebears as if they stood beside you, listening.", Nobles),
         H("You say \"we\" for your house's interests, and \"I\" only for your own small wants.", Nobles),
         H("You talk of yourself by name, as another would, when you are proud or angry.", Nobles),
         H("You haggle by habit, even over things that are not for sale.", Traders),
         H("You see everything in terms of the land: seasons, seed, rain and rot.", LandFolk),
         H("You give everyone a nickname and name nothing outright that could hang you.", new[] {SocialStation.GangLeader}),
         H("You answer with teachings you know by heart, and the occasional pointed parable.", new[] {"preacher"}),
         H("You speak fast and leap between thoughts, and apologize for it.", null, Young),
         H("You try to sound older and harder than you are, and sometimes slip.", null, Young),
         H("You measure everything against your youth: things were harder, cheaper or braver then.", null, Elder),
         H("You give advice nobody asked for, kindly meant and at length.", null, Elder),
         H("You call those younger than you \"child\" or \"lad\" or \"girl\", whatever their rank.", null, Elder)
      };

      /// <summary>The habits a character of this station and age may draw, in catalog order.</summary>
      public static IReadOnlyList<string> HabitsFor(string station, int age)
      {
         int bracket = age < 25 ? Young : age >= 50 ? Elder : Everyone;
         return Habits.Where(h => (h.Stations == null || h.Stations.Contains(station ?? "")) && (h.Age == Everyone || h.Age == bracket))
                      .Select(h => h.Text)
                      .ToList();
      }

      /// <summary>The voice of a character, the same on every draw.</summary>
      public static CharacterVoice Draw(CharacterVoiceFacts f)
      {
         string id = f?.HeroId ?? "";
         string station = f?.Station ?? "";
         RomanticTraits t = f?.Traits ?? new RomanticTraits();
         var moodRoll = new Random(Romance.IntimateActPolicy.StableSeed(id + "|voice-tongue"));

         return new CharacterVoice {
            Habit = HabitOf(id, station, f?.Age ?? 30),
            Coarseness = CoarsenessOf(station, t, f?.Age ?? 30, moodRoll.Next(2)),
            Piety = PietyOf(station, t, moodRoll.Next(10))
         };
      }

      /// <summary>
      ///   The habit for life, and the habit of an age. The lifelong habit is drawn among those of the station that belong
      ///   to no age, so it is the same at 24, 25, 49 and 50 (VonLemberg, source audit, 15/11/2026: the draw was taken in
      ///   a pool that changed size at 25 and at 50, so most characters changed habit on those birthdays). A second,
      ///   separate draw decides whether the young or the old character shows a habit of their age instead, with the
      ///   same odds as before: that one comes with the age and goes with it, which is what an age habit is.
      /// </summary>
      private static string HabitOf(string id, string station, int age)
      {
         IReadOnlyList<string> lifelong = HabitsFor(station, 30);
         string own = lifelong[new Random(Romance.IntimateActPolicy.StableSeed(id + "|voice")).Next(lifelong.Count)];

         List<string> ofTheAge = HabitsFor(station, age).Where(h => !lifelong.Contains(h)).ToList();
         if (ofTheAge.Count == 0) return own;

         var ageRoll = new Random(Romance.IntimateActPolicy.StableSeed(id + "|voice-age"));

         return ageRoll.Next(lifelong.Count + ofTheAge.Count) < ofTheAge.Count ? ofTheAge[ageRoll.Next(ofTheAge.Count)] : own;
      }

      /// <summary>
      ///   How freely they curse: hirelings and gang leaders more, nobles less, a preacher or a child never; the
      ///   impulsive and the merciless more, the honourable less.
      /// </summary>
      public static Coarseness CoarsenessOf(string station, RomanticTraits t, int age, int jitter)
      {
         if (station == "preacher" || age < 16) return Coarseness.Never;
         t ??= new RomanticTraits();
         int score = StationCoarseness(station) + jitter;
         if (t.Calculating < 0) score++;
         if (t.Mercy < 0) score++;
         if (t.Honor > 0) score--;

         return score <= 0 ? Coarseness.Never : score >= 3 ? Coarseness.Often : Coarseness.Rarely;
      }

      /// <summary>
      ///   How their piety shows, from a roll of 0..9: a preacher is devout; honour and mercy widen the devout share,
      ///   dishonour and calculation the share who believe for show.
      /// </summary>
      public static Piety PietyOf(string station, RomanticTraits t, int roll)
      {
         if (station == "preacher") return Piety.Devout;
         t ??= new RomanticTraits();
         int devout = 3 + (t.Honor > 0 ? 1 : 0) + (t.Mercy > 0 ? 1 : 0);
         int forShowFrom = 8 - (t.Honor < 0 ? 1 : 0) - (t.Calculating > 0 ? 1 : 0);
         if (roll < devout) return Piety.Devout;

         return roll >= forShowFrom ? Piety.ForShow : Piety.Ordinary;
      }

      /// <summary>The voice in the second person, for the prompt: the habit, then cursing, then piety.</summary>
      public static string Render(CharacterVoice v)
      {
         if (v == null || string.IsNullOrWhiteSpace(v.Habit)) return null;

         string tongue = v.Coarseness switch {
            Coarseness.Often => "You curse freely and coarsely among equals, in anger, complaint and jest, though not in every sentence; you hold it back before your betters, priests and children.",
            Coarseness.Rarely => "You curse rarely: only in real anger or pain, and never before your betters.",
            _ => "You never curse; coarse talk is beneath you."
         };
         string faith = v.Piety switch {
            Piety.Devout => "You are devout: you greet, bless, swear and curse by what your people hold holy, often, and never make light of it.",
            Piety.ForShow => "Your piety is for show: you say the proper holy words where they are expected and do not believe them, and in private it shows.",
            _ => "You believe as your people do: a blessing or an oath by what they hold holy slips out now and then, no more."
         };

         return $"{v.Habit} Let it show now and then, never in every line. {tongue} {faith}";
      }

      private static int StationCoarseness(string station) => station switch {
         SocialStation.GangLeader => 2,
         SocialStation.Wanderer or SocialStation.Companion => 1,
         "headman" or "ruralnotable" or "artisan" => 1,
         _ => 0
      };

      private static HabitEntry H(string text, string[] stations = null, int age = Everyone) => new() {Text = text, Stations = stations, Age = age};

      private static class SocialStation
      {
         public const string Ruler = Social.SocialStationPolicy.Ruler, Lord = Social.SocialStationPolicy.Lord, Noble = Social.SocialStationPolicy.Noble,
            Companion = Social.SocialStationPolicy.Companion, Wanderer = Social.SocialStationPolicy.Wanderer, GangLeader = Social.SocialStationPolicy.GangLeader;
      }
   }
}
