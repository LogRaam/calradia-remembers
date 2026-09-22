// Code written by Gabriel Mailhot, 22/08/2026.

#region

using System.Globalization;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   Turns a bridge action type into the short, player-facing label the Memories panel shows in blue beside a
   ///   memory, so a journal entry that carries a REAL mechanical consequence (gold moved, a recruit, a fief
   ///   granted, regard shifted) reads at a glance. Pure: the caller supplies the type and, for the relation
   ///   verbs, whether the shift was positive. Chat-flow controls that are not deeds (end_conversation,
   ///   request_privacy, witness_leaves, retire) and blanks return null, so they never earn a tag.
   /// </summary>
   public static class ActionMemoryLabel
   {
      /// <summary>
      ///   The label for one executed action, or null when the type is a non-deed flow control or unknown-blank.
      ///   <paramref name="wasPositive" /> only colours the relation verbs (rose vs fell); ignored otherwise.
      /// </summary>
      public static string Describe(string actionType, bool wasPositive = true)
      {
         if (string.IsNullOrWhiteSpace(actionType)) return null;

         switch (actionType.Trim().ToLowerInvariant())
         {
            // Not deeds: chat-flow controls never earn a memory tag.
            case "end_conversation":
            case "request_privacy":
            case "witness_leaves":
            case "retire":
            case "gather_news": // deprecated alias
               return null;

            // Coin and goods.
            case "give_gold": return "Gift of gold";
            case "take_gold": return "Gold paid";
            case "pay_blackmail": return "Blackmail paid";
            case "give_item": return "A gift given";
            case "give_influence": return "Influence given";
            case "spend_influence": return "Influence spent";
            case "grant_stipend": return "Stipend granted";

            // Relation / standing (direction from the outcome).
            case "change_relation": return wasPositive ? "Regard rose" : "Regard fell";
            case "sway_opinion": return "Opinion swayed";

            // Prisoners.
            case "free_prisoner":
            case "release_prisoner": return "Set free";
            case "execute_prisoner": return "Executed";
            case "recruit_prisoner": return "Recruited from the cells";
            case "give_prisoner": return "Prisoner handed over";
            case "buy_prisoner": return "Prisoner bought";
            case "sell_prisoner": return "Prisoner sold";
            case "turn_nemesis": return "Turned to your side";
            case "harm_prisoner": return "Harmed";

            // Service and clan.
            case "join_party": return "Joined your party";
            case "recruit_notable": return "Taken into service";
            case "join_as_mercenary": return "Hired as a mercenary";
            case "end_mercenary": return "Mercenary contract ended";
            case "join_as_vassal": return "Sworn as your vassal";
            case "join_clan": return "Joined your clan";
            case "expel_from_clan": return "Cast out of the clan";
            case "swear_oath": return "An oath sworn";
            case "part_ways": return "Parted ways";
            case "rejoin_party": return "Returned to your party";
            case "recall_companion": return "Recalled";
            case "reassure_companion": return "Reassured";
            case "dispatch_mission": return "Sent on an errand";
            case "assign_party_role": return "Given a party role";
            case "follow_me": return "Rode out with you";
            case "ride_with_me": return "Joined your retinue";
            case "dismiss_escort": return "Escort dismissed";
            case "lend_troops": return "Troops lent";
            case "give_troops": return "Troops given";

            // Governance.
            case "grant_fief": return "Granted a fief";
            case "revoke_fief": return "A fief revoked";
            case "appoint_governor": return "Appointed governor";

            // Romance and household.
            case "marry": return "Married";
            case "arrange_marriage": return "A match arranged";
            case "take_as_consort": return "Taken as consort";
            case "take_as_secret_lover": return "A secret lover";
            case "open_relationship": return "The bond opened";
            case "accept_other_partners": return "A plural household accepted";
            case "close_relationship": return "The bond closed";
            case "end_affair": return "An affair ended";
            case "end_marriage":
            case "end_own_marriage": return "A marriage ended";
            case "accept_divorce": return "Divorce accepted";
            case "decline_divorce": return "Divorce refused";
            case "make_amends": return "Amends made";
            case "hear_account": return "A grievance answered";
            case "pledge_against": return "Pledged against a foe";
            case "grant_blessing": return "A blessing given";
            case "mediate_peace": return "Peace brokered";

            // Anything else the bridge handles: a spaced, capitalised fallback ("some_deed" -> "Some deed").
            default: return Humanize(actionType);
         }
      }

      private static string Humanize(string type)
      {
         string spaced = type.Trim().Replace('_', ' ');
         if (spaced.Length == 0) return null;

         var sb = new StringBuilder(spaced.Length);
         sb.Append(char.ToUpper(spaced[0], CultureInfo.InvariantCulture));
         if (spaced.Length > 1) sb.Append(spaced.Substring(1));

         return sb.ToString();
      }
   }
}
