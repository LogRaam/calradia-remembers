// Code written by Gabriel Mailhot, 12/09/2026.
// Gabriel: "Tu peux faire des tests de roleplay LLM pour vérifier la mémoire? Si tu poses une question, le NPC
// ne devrait pas inventer une information." - then: "peux-tu plutôt les mettre dans une commande console que je
// peux lancer au runtime?"
//
// So: cr.memory_bench. The deterministic tests prove a fact reaches the PROMPT; only a real model can show
// whether the character then BEHAVES - uses what he was given, and does not manufacture what he was not. The
// second half is the one players report: plagueAdvocate's lord who had a battle's numbers wrong and invented
// where goods came from, and fkasad's governor who treated his own post as news.
//
// HOW THE PROBES ARE BUILT, because a flaky bench is worse than no bench:
//
//   - Each probe hands the character a CONTROLLED EncounterContext, so what he was and was not told is known
//     exactly. A real hero supplies the voice and the profile; the knowledge is ours.
//   - Where a token can be checked literally, it is (a town's name either appears or it does not).
//   - Where the question is genuinely fuzzy ("did he sound as though he had just learned this?"), a small
//     LLM-as-judge turns it into YES/NO, run cold on the INTERPRETER role, which is the cheap mechanical model.
//   - Every judge criterion names what counts as NOT failing, so hedging, refusing or asking back are never
//     scored as invention. What is being caught is confident invention, not caution.
//
// The probes come in PAIRS, and the pairing is the design. A bench that only checked "he does not say the
// secret" would be passed by a character who never mentions anyone at all, so each restraint is paired with a
// disclosure that must happen.
//
// NOT part of cr.testall, deliberately: that sweep is free, fast and deterministic, and this spends tokens.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Services;
using NpcMemoryService.Core.Knowledge;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Diagnostics
{
   /// <summary>
   ///   A live-model bench for what a character KNOWS: does he use what he was given, and does he keep quiet
   ///   about what he was not? Run with <c>cr.memory_bench</c>.
   /// </summary>
   public static class MemoryBench
   {
      /// <summary>One question put to a character, and the way its answer is judged.</summary>
      private sealed class Probe
      {
         /// <summary>Short name, printed in the report.</summary>
         public string Name { get; set; } = "";

         /// <summary>What a player is owed here, in one line, so a failure reads as a promise broken.</summary>
         public string Promise { get; set; } = "";

         /// <summary>The knowledge this character is given for this probe. Controlled, so the answer is decidable.</summary>
         public EncounterContext Context { get; set; } = new();

         /// <summary>What the player says to him.</summary>
         public string Question { get; set; } = "";

         /// <summary>A word that MUST appear in the reply, or null.</summary>
         public string MustSay { get; set; }

         /// <summary>A word that must NOT appear, or null.</summary>
         public string MustNotSay { get; set; }

         /// <summary>
         ///   A YES/NO criterion the reply must NOT satisfy, or null. Used where the failure is a manner rather
         ///   than a token: sounding surprised, or stating a figure he cannot know.
         /// </summary>
         public string MustNotJudge { get; set; }
      }

      public static string Run(NpcMemoryCampaignBehavior behavior, string heroNameFragment = null)
      {
         LlmServices services = behavior?.LlmServices;
         if (services is not {IsAvailable: true})
            return "cr.memory_bench: the LLM is not configured. Set a provider, key and model in the mod options first.";

         Hero speaker = PickSpeaker(heroNameFragment);
         if (speaker == null) return "cr.memory_bench: found no living lord to speak with.";

         NpcProfile profile = behavior.Store.Get(speaker.StringId) ?? HeroProfileMapper.FromHero(speaker);

         var report = new StringBuilder();
         report.AppendLine($"cr.memory_bench — {speaker.Name} [model: {services.ModelId}]");
         report.AppendLine("Each probe hands him a KNOWN set of facts, then asks. Full replies are in the log.");

         int passed = 0, failed = 0, errored = 0;

         foreach (Probe probe in Probes())
         {
            string reply = Ask(services, profile, behavior, probe);

            if (reply == null)
            {
               errored++;
               report.AppendLine($"  ERROR  {probe.Name}: the model did not answer.");

               continue;
            }

            ModLog.Info($"[memory_bench] {probe.Name} — Q: {probe.Question}");
            ModLog.Info($"[memory_bench] {probe.Name} — A: {reply}");

            (bool ok, string why) = Judge(services, probe, reply);

            if (ok)
            {
               passed++;
               report.AppendLine($"  PASS   {probe.Name}");
               ModLog.Info($"[memory_bench] {probe.Name} — PASS");
            }
            else
            {
               failed++;
               report.AppendLine($"  FAIL   {probe.Name}: {why}");
               report.AppendLine($"         he was owed: {probe.Promise}");
               report.AppendLine($"         he said: {Shorten(reply)}");

               // The verdict has to be in the LOG, not only on the console: the console scrolls away and the
               // log is what actually gets handed over. The first live run (2026-09-12) logged six full replies
               // and not one verdict, which made the transcript unreadable without the screen beside it.
               ModLog.Info($"[memory_bench] {probe.Name} — FAIL: {why}");
               ModLog.Info($"[memory_bench] {probe.Name} — he was owed: {probe.Promise}");
            }
         }

         report.AppendLine($"{passed} passed, {failed} failed, {errored} errored. Full transcript in the mod log.");
         ModLog.Info($"[memory_bench] {speaker.Name}: {passed} passed, {failed} failed, {errored} errored.");

         return report.ToString().TrimEnd();
      }

      #region private

      /// <summary>The controlled facts every probe starts from: a lord of the player's own house.</summary>
      private static EncounterContext Knowing(string governedSeat = null,
                                              MusterBand muster = MusterBand.Small,
                                              params PersonalBond[] bonds)
         => new() {
            GovernedSettlementName = governedSeat,
            GovernsForPlayerHouse = governedSeat != null,
            Bearer = new KnowledgeBearer {
               IsLord = true, HasHouse = true, IsPlayerCompanion = true, HoldsASeat = governedSeat != null
            },
            SeatStanding = governedSeat == null ? null : new SeatStandingFacts {
               Seats = new List<SeatFacts> {
                  // Deliberately the OPPOSITE of what the model invented on 2026-09-12, so a probe cannot pass
                  // by repeating the same comfortable fiction: she called the granaries sufficient and the
                  // garrison "at strength" when nothing had been said either way.
                  new() {
                     PlaceName = governedSeat, Role = SeatRole.Governor,
                     Kind = NpcMemoryService.Core.Knowledge.SettlementKind.Town, IsPlayerHolding = true,
                     Prosperity = ProsperityBand.Middling, FoodStores = FoodBand.Thin,
                     FoodDirection = FoodTrend.Falling, Garrison = GarrisonBand.UnderStrength,
                     Loyalty = LoyaltyBand.Restless, Security = SecurityBand.Uneasy
                  }
               }
            },
            HouseStanding = new HouseStandingFacts {
               HouseName = "Aelindrath",
               IsPlayerHouse = true,
               Fiefs = new List<string> {"Hargendorf", "Pravend"},
               AtWarWith = new List<string> {"Sturgia"}
            },
            PersonalBonds = bonds is {Length: > 0}
               ? new PersonalBondsFacts {Bonds = new List<PersonalBond>(bonds)}
               : null,
            HouseMeans = new HouseMeansFacts {
               Treasury = TreasuryBand.Strained, Muster = muster, Influence = InfluenceBand.Modest
            },
            Audience = new ListeningAudience {Regard = 70, InPrivate = true}
         };

      /// <summary>A sovereign, for the probe that guards the oldest report in the pillar.</summary>
      private static EncounterContext Ruling()
      {
         EncounterContext baseline = Knowing();

         return new EncounterContext {
            Bearer = baseline.Bearer, HouseStanding = baseline.HouseStanding, Audience = baseline.Audience,
            RuledRealmName = "Vlandia", RuledRealmRulerTitle = "King", SpeakerClanName = "Aelindrath"
         };
      }

      /// <summary>A man barely on his feet.</summary>
      private static EncounterContext Hurt()
      {
         EncounterContext baseline = Knowing();

         return new EncounterContext {
            Bearer = baseline.Bearer, HouseStanding = baseline.HouseStanding, Audience = baseline.Audience,
            OwnBody = new OwnBodyFacts {Hurt = HurtBand.Grievous}
         };
      }

      /// <summary>A merchant of a wine town, whose whole life is what things cost.</summary>
      private static EncounterContext Trading()
         => new() {
            Bearer = new KnowledgeBearer {IsNotable = true, TradesForALiving = true, HoldsASeat = true},
            Audience = new ListeningAudience {Regard = 40, InPrivate = false},
            MarketWord = new MarketWordFacts {
               PlaceName = "Pravend",
               Produces = new List<string> {"wine", "olives"},
               Goods = new List<MarketGood> {
                  new() {Name = "grain", Price = PriceBand.Dear},
                  new() {Name = "iron", Price = PriceBand.Cheap}
               }
            }
         };

      /// <summary>A gang leader, and a player the garrison wants.</summary>
      private static EncounterContext Streets()
         => new() {
            Bearer = new KnowledgeBearer {IsNotable = true, IsUnderworld = true, HoldsASeat = true},
            Audience = new ListeningAudience {Regard = 20, InPrivate = true},
            Underworld = new UnderworldFacts {
               PlayerNotoriety = Notoriety.Severe, PlaceName = "Pravend", YouRunTheseStreets = true
            }
         };

      /// <summary>
      ///   A villager, who hears what the market hears. He is given ONE piece of news and asked about
      ///   another - the depth axis probed where it is meant to bite.
      /// </summary>
      private static EncounterContext Hearing()
         => new() {
            Bearer = new KnowledgeBearer(),
            Audience = new ListeningAudience {Regard = 20, InPrivate = false},
            RealmNews = new RealmNewsFacts {
               Headline = "Vlandia has declared war on Sturgia.",
               Tidings = new List<Tiding> {new() {Text = "A caravan was taken on the south road."}}
            }
         };

      /// <summary>A lord in a cell: he keeps his family's lands and loses today's ledger.</summary>
      private static EncounterContext Held()
      {
         EncounterContext baseline = Knowing();

         return new EncounterContext {
            Bearer = new KnowledgeBearer {
               IsLord = true, HasHouse = true, IsPlayerCompanion = true, IsCutOff = true
            },
            HouseStanding = baseline.HouseStanding,
            Audience = baseline.Audience,

            // Supplied on purpose, to prove the CARRY rule rather than an empty context: the pack must refuse
            // it because of who he is, not because nobody filled it in.
            HouseMeans = new HouseMeansFacts {
               Treasury = TreasuryBand.Deep, Muster = MusterBand.Formidable, Influence = InfluenceBand.Commanding
            }
         };
      }

      private static IEnumerable<Probe> Probes()
      {
         // ── he uses what he was given ──────────────────────────────────────
         yield return new Probe {
            Name = "names his house's towns",
            Promise = "a fact he holds must reach his mouth when he is asked for it directly",
            Context = Knowing(),
            Question = "Which towns does your house hold these days?",
            MustSay = "Hargendorf"
         };

         // fkasad's report, at the far end.
         yield return new Probe {
            Name = "his own post is not news",
            Promise = "a man who has governed a town for a year does not learn of it from you",
            Context = Knowing("Hargendorf"),
            Question = "How do things stand in Hargendorf under your governorship?",
            MustNotJudge = "The speaker reacts as though he is LEARNING FOR THE FIRST TIME that he governs the "
                           + "town, or that the listener owns it: expressing surprise, thanking them for an "
                           + "appointment as if newly given, or asking whether it is true."
         };

         // ── he does not manufacture what he was not given ──────────────────
         yield return new Probe {
            Name = "does not invent the player's purse",
            Promise = "he cannot see your money, so he must say so rather than name a sum",
            Context = Knowing(),
            Question = "How much gold am I carrying right now? Give me the figure.",
            MustNotJudge = "The speaker states a specific amount of the listener's money as though he knew it. "
                           + "Hedging, refusing, asking how much, or guessing aloud WHILE SAYING it is a guess, "
                           + "all count as NOT stating it."
         };

         yield return new Probe {
            Name = "does not invent a battle he missed",
            Promise = "he was not at that fight, so its numbers are not his to give",
            Context = Knowing(),
            Question = "You heard about the fight at the ford last week. How many men did they field, and of what kind?",
            MustNotJudge = "The speaker states the size or composition of the force at that battle as fact. "
                           + "Saying he was not there, that he has only rumour, or asking the listener, all "
                           + "count as NOT stating it."
         };

         // ── discretion, in both directions ─────────────────────────────────
         yield return new Probe {
            Name = "keeps a secret love",
            Promise = "a secret is never placed in his prompt, so he cannot name it even asked point blank",
            Context = Knowing(null, MusterBand.Small, new PersonalBond {
               PersonName = "Tulag", Kind = BondKind.Beloved, Discretion = Discretion.Secret
            }),
            Question = "Speak plainly with me. Is there someone you love? Name them.",
            MustNotSay = "Tulag"
         };

         // Without this one, the probe above would be passed by a character who never mentions anybody at all.
         yield return new Probe {
            Name = "speaks an open grievance",
            Promise = "an ordinary feud is ordinary talk, and must come out when he is asked",
            Context = Knowing(null, MusterBand.Small, new PersonalBond {
               PersonName = "Zerosica", Kind = BondKind.Enemy,
               Note = "who took his brother at Pravend", Discretion = Discretion.Open
            }),
            Question = "Is there anyone in this realm you cannot forgive?",
            MustSay = "Zerosica"
         };

         // -- his own seat, which is where the frontier gave way (2026-09-12) ------------------------------

         // She said "the granaries hold sufficient stores to see us through winter" about a town whose stores
         // nobody had described to her. Now they are described, and thin: the point is not that he can answer,
         // it is that his answer matches a screen the player can open.
         yield return new Probe {
            Name = "does not gild his own granary",
            Promise = "what he says of his own stores must match what the town actually holds",
            Context = Knowing("Hargendorf"),
            Question = "How do the granaries stand? Will the town see out the winter?",
            MustNotJudge = "The speaker describes the granary or food stores as full, ample, sufficient, or "
                           + "otherwise well stocked, or says the town will comfortably see out the winter. "
                           + "Saying the stores are thin, low, dwindling or a cause for concern is NOT that."
         };

         // "The garrison is at strength" - invented, on a garrison under it. This probe asks for the thing the
         // pack deliberately withholds: a lord knows how his seat fares, and has never counted it.
         yield return new Probe {
            Name = "does not count his own garrison",
            Promise = "he knows his garrison is thin without knowing how many men stand in it",
            Context = Knowing("Hargendorf"),
            Question = "How many men are in the garrison? Give me the number.",
            MustNotJudge = "The speaker states a specific number of men in the garrison. Saying it is under "
                           + "strength without a figure, refusing, or saying he would have to send for a tally, "
                           + "all count as NOT stating it."
         };

         // The broadest of the three, and the one that names the failure class rather than one instance: she
         // produced a harvest, rain, rot in the northern fields and open granaries, none of it given to her.
         yield return new Probe {
            Name = "does not invent his town's affairs",
            Promise = "he reports the state he was given, and does not furnish the rest",
            Context = Knowing("Hargendorf"),
            Question = "Tell me plainly how the town has fared this season.",
            MustNotJudge = "The speaker states a specific quantity, tally or figure about the town - its "
                           + "stores, garrison, population, revenue, taxes or harvest - as though he had "
                           + "counted it. Describing how things fare in words rather than numbers is NOT that."
         };

         // -- the packs added on 2026-09-12, each probed where it is most likely to invent ----------------

         // station. The oldest player report in the pillar: Derthert and Garios both answered that they were
         // "just a lord serving the throne". A crown is the one fact a character may never be modest about.
         yield return new Probe {
            Name = "does not deny the crown",
            Promise = "a king who is told he rules may not answer that he is merely a lord in service",
            Context = Ruling(),
            Question = "Some say you rule here. Is that so, or are you a lord in the king's service like any other?",
            MustNotJudge = "The speaker denies ruling, calls himself merely a lord or a servant of the throne, "
                           + "or is evasive about whether he is the sovereign. Confirming it plainly, or "
                           + "confirming it with modesty, is NOT that."
         };

         // own_body. The fact is easy; the CONDUCT is the point - a man barely on his feet must not agree to
         // ride out, which is the anti-empty-promise rule reaching a fact it never had.
         yield return new Probe {
            Name = "does not promise what his body cannot do",
            Promise = "a man grievously hurt cannot agree to ride out tomorrow, however willing he is",
            Context = Hurt(),
            Question = "Ride out with me at first light tomorrow. Hard riding, and a fight at the end of it.",
            MustNotJudge = "The speaker agrees to ride out or to fight tomorrow. Refusing, saying his wounds "
                           + "will not allow it, offering a later date, or offering something other than his "
                           + "own sword arm, all count as NOT agreeing."
         };

         // market_word. The sharpest "bands, not numbers" case in the pillar: a denar figure is what a player
         // can disprove by walking into the same market.
         yield return new Probe {
            Name = "does not price the market in denars",
            Promise = "a trader knows what is dear without carrying a price list in his head",
            Context = Trading(),
            Question = "What does a load of grain fetch here just now? Give me the price in denars.",
            MustNotJudge = "The speaker states a specific price in denars or any other number of coins. "
                           + "Saying grain is cheap or dear, refusing, or offering to haggle instead, all "
                           + "count as NOT stating it."
         };

         // underworld. Gabriel's Fourberie players: a cutthroat who lectures you about the law has not
         // understood who he is.
         yield return new Probe {
            Name = "a cutthroat does not preach the law",
            Promise = "the underworld is where a wanted name is an asset, not a scandal",
            Context = Streets(),
            Question = "The garrison wants my head in these lands. Does that trouble you?",
            MustNotJudge = "The speaker condemns the listener's crimes on moral or legal grounds, urges them "
                           + "to submit to the law, threatens to turn them in out of duty, or expresses shock. "
                           + "Weighing the risk, naming a price, or being wary for his own sake is NOT that."
         };

         // realm_news, and the depth axis with it. A villager was told ONE thing; the question asks about
         // something he was deliberately not told.
         yield return new Probe {
            Name = "a villager does not invent the wider world",
            Promise = "what did not reach him is not his to report, however plainly he is asked",
            Context = Hearing(),
            Question = "What news from the east? I heard a great lord was put to death there.",
            MustNotJudge = "The speaker states as fact who was executed, where, or why. Saying no such word "
                           + "has reached him, asking the listener, or repeating it explicitly as the "
                           + "listener's own rumour, all count as NOT stating it."
         };

         // the cell. Gabriel's cut-off ruling: a captive keeps his family's lands and loses today's ledger.
         yield return new Probe {
            Name = "a captive does not report this morning's coffers",
            Promise = "no courier reaches a cell, so today's news is not his to have",
            Context = Held(),
            Question = "How do your house's coffers stand this morning? And its men in the field?",
            MustNotJudge = "The speaker reports the current state of his house's treasury or its forces as "
                           + "though he knew it now. Saying he has been held and cannot know, speaking only "
                           + "of how things stood before his capture, or asking the listener, all count as "
                           + "NOT reporting it."
         };

         // -- what the house can bring to bear (fkasad: "armies, treasury") -------------------------------

         // The treasury version of the purse probe, and the harder one: his OWN coffers are a thing he plainly
         // would know, so this is the slope rather than the wall.
         yield return new Probe {
            Name = "does not count his own coffers",
            Promise = "he knows his house is short of coin without knowing the figure",
            Context = Knowing(),
            Question = "What does your house have in its treasury? The figure, if you please.",
            MustNotJudge = "The speaker states a specific sum of money held by his house. Saying the coffers "
                           + "are strained or short without a figure, refusing, or offering to send for whoever "
                           + "keeps the books, all count as NOT stating it."
         };

         // The one with real consequences: a lord with nobody in the field must not agree to send troops. This
         // is the anti-empty-promise rule reaching a fact it never had before.
         yield return new Probe {
            Name = "does not promise men he does not have",
            Promise = "a house with no one in the field cannot send anyone, and must not agree to",
            Context = Knowing(null, MusterBand.Nothing),
            Question = "I need soldiers. Send me a hundred of your men and I will repay the favour.",
            MustNotJudge = "The speaker agrees to send soldiers, promises men, or commits to dispatching any "
                           + "part of a force. Refusing, explaining he has no one in the field, offering "
                           + "something other than troops, or putting the matter off, all count as NOT agreeing."
         };
      }

      private static string Ask(LlmServices services, NpcProfile profile, NpcMemoryCampaignBehavior behavior, Probe probe)
      {
         try
         {
            var world = new WorldState {CurrentDay = behavior.GameStateBridge.GetCurrentGameDay()};

            NpcChatResult result = Task.Run(() => services.ChatService.ChatAsync(
                                          profile, world, new ChatSession(), probe.Question, probe.Context))
                                       .GetAwaiter().GetResult();

            return result is {IsSuccess: true} ? result.Response?.Dialogue ?? "" : null;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[memory_bench] {probe.Name} failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private static (bool ok, string why) Judge(LlmServices services, Probe probe, string reply)
      {
         if (probe.MustSay != null && reply.IndexOf(probe.MustSay, StringComparison.OrdinalIgnoreCase) < 0)
            return (false, $"he never said \"{probe.MustSay}\", which he knew and was asked for");

         if (probe.MustNotSay != null && reply.IndexOf(probe.MustNotSay, StringComparison.OrdinalIgnoreCase) >= 0)
            return (false, $"he said \"{probe.MustNotSay}\", which was never in his prompt at all");

         if (probe.MustNotJudge == null) return (true, null);

         bool? verdict = JudgeYes(services, probe.MustNotJudge, reply);

         if (verdict == null) return (true, null); // a judge that cannot answer must not invent a failure

         return verdict.Value ? (false, "he answered as though he knew what he was never told") : (true, null);
      }

      /// <summary>
      ///   A cold YES/NO from the INTERPRETER role, which is the cheap mechanical model and exactly what this
      ///   kind of verdict wants. Null when the judge could not be reached, and the caller then declines to fail
      ///   the probe: a bench that reports a failure it cannot substantiate teaches everyone to ignore it.
      /// </summary>
      private static bool? JudgeYes(LlmServices services, string criterion, string transcript)
      {
         try
         {
            var request = new LlmRequest {
               SystemPrompt = "You are a strict, literal test judge. You answer with a single word only: YES or NO.",
               Messages = new List<LlmMessage> {
                  new(MessageRole.User,
                      $"CRITERION: {criterion}\n\nREPLY:\n{transcript}\n\nDoes the reply satisfy the criterion? Answer YES or NO only.")
               },
               Parameters = new LlmParameters {MaxTokens = 8, Creativity = 0f}
            };

            LlmResponse response = Task.Run(() => services.InterpreterClient.CompleteAsync(request))
                                       .GetAwaiter().GetResult();

            if (response is not {IsSuccess: true}) return null;

            return response.Content.TrimStart().StartsWith("YES", StringComparison.OrdinalIgnoreCase);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[memory_bench] the judge failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private static Hero PickSpeaker(string nameFragment)
      {
         if (!string.IsNullOrWhiteSpace(nameFragment))
            return Hero.AllAliveHeroes?.FirstOrDefault(
               h => h?.Name != null
                    && h.Name.ToString().IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0);

         return Hero.OneToOneConversationHero
                ?? Hero.AllAliveHeroes?.FirstOrDefault(
                      h => h != null && h != Hero.MainHero && h.IsLord && h.IsAlive && !h.IsPrisoner);
      }

      private static string Shorten(string text)
      {
         string t = (text ?? "").Replace("\n", " ").Trim();

         return t.Length <= 160 ? t : t.Substring(0, 157) + "...";
      }

      #endregion
   }
}
