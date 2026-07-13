// Code written by Gabriel Mailhot, 12/07/2026.
// Pure tally of an assembly's roster by status: the "X of N present" counter and the "is everyone accounted
// for?" check, kept out of the mod behavior so the counting rules are documented by tests.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>A snapshot count of an assembly's invited seats, bucketed by lifecycle status.</summary>
    public readonly struct AssemblyProgress
    {
        private AssemblyProgress(int total, int present, int inTransit, int declined, int awaiting)
        {
            Total = total;
            Present = present;
            InTransit = inTransit;
            Declined = declined;
            Awaiting = awaiting;
        }

        /// <summary>Every invited seat.</summary>
        public int Total { get; }

        /// <summary>Seats that are filled: physically Present, or held by an Ambassador.</summary>
        public int Present { get; }

        /// <summary>Accepted and on the way (Accepted or EnRoute): coming, not yet seated.</summary>
        public int InTransit { get; }

        /// <summary>Refused the summons.</summary>
        public int Declined { get; }

        /// <summary>Summoned but not yet replied (Invited).</summary>
        public int Awaiting { get; }

        /// <summary>
        ///   True when no seat is still pending or in transit: everyone has landed on a final outcome
        ///   (Present, Ambassador, or Declined), so there is nothing left to wait for.
        /// </summary>
        public bool AllResolved => Awaiting == 0 && InTransit == 0;

        public static AssemblyProgress From(IEnumerable<AssemblyInviteeStatus> statuses)
        {
            int total = 0, present = 0, inTransit = 0, declined = 0, awaiting = 0;
            if (statuses != null)
                foreach (AssemblyInviteeStatus s in statuses)
                {
                    total++;
                    switch (s)
                    {
                        case AssemblyInviteeStatus.Present:
                        case AssemblyInviteeStatus.Ambassador:
                            present++;
                            break;
                        case AssemblyInviteeStatus.Accepted:
                        case AssemblyInviteeStatus.EnRoute:
                            inTransit++;
                            break;
                        case AssemblyInviteeStatus.Declined:
                            declined++;
                            break;
                        default: // Invited
                            awaiting++;
                            break;
                    }
                }

            return new AssemblyProgress(total, present, inTransit, declined, awaiting);
        }
    }
}
