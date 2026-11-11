// Code written by Gabriel Mailhot, 11/11/2026.
// The host side of ArmyRoster. Schmidt (Discord, 11/11/2026): "Lords know which lords they invited to their army when they
// form a grand army, as well as the army size of the lords and the total number of men in the grand army." Reads the army
// the speaker marches in, the leader's party first, and hands the pure policy each party's lord and men. A lord in no
// army, or a captive, is told nothing here; his own command already says what rides with him.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.War;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>The army a character marches in, for the prompt.</summary>
   public static class ArmyRosterReader
   {
      /// <summary>The lines for the prompt; null when the speaker marches in no army of two parties or more.</summary>
      public static IReadOnlyList<string> Read(Hero speaker)
      {
         try
         {
            if (speaker == null || speaker == Hero.MainHero || speaker.IsPrisoner) return null;

            MobileParty own = speaker.PartyBelongedTo;
            Army army = own?.Army;
            if (army?.LeaderParty == null || army.Parties == null) return null;

            var parties = new List<ArmyRosterParty> {PartyOf(army.LeaderParty, own)};
            foreach (MobileParty p in army.Parties)
               if (p != null && p != army.LeaderParty)
                  parties.Add(PartyOf(p, own));

            IReadOnlyList<string> lines = ArmyRoster.Describe(new ArmyRosterFacts {Parties = parties});

            return lines.Count == 0 ? null : lines;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ARMY] army roster failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private static ArmyRosterParty PartyOf(MobileParty party, MobileParty speakers) => new ArmyRosterParty {
         Lord = (party.LeaderHero?.Name ?? party.Name)?.ToString(),
         Men = party.MemberRoster?.TotalManCount ?? 0,
         IsSpeaker = party == speakers,
         IsPlayer = party == MobileParty.MainParty
      };
   }
}
