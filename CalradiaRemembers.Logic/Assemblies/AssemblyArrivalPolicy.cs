// Code written by Gabriel Mailhot, 12/07/2026.
// Pure arrival decision for an invitee who already ACCEPTED: can they reach the host by the deadline and
// attend in person, or does an ambassador take their seat? This is the robust answer to fragile forced-AI
// movement (the courier scar tissue): when the journey does not fit the deadline, narration (a stand-in)
// covers the gap instead of a stuck party breaking the assembly. Re-evaluated as the invitee travels.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Maps an estimated time of arrival against the deadline to an <see cref="ArrivalMode" />.</summary>
    public static class AssemblyArrivalPolicy
    {
        /// <summary>
        ///   <see cref="ArrivalMode.InPerson" /> when the invitee can reach the host by the deadline (estimated
        ///   days to arrive is no greater than the days left), else <see cref="ArrivalMode.Ambassador" />. A
        ///   hero already at or past the host (<paramref name="etaDays" /> at or below 0) always attends in
        ///   person, even if the deadline has passed; a passed deadline for a hero still on the road yields an
        ///   ambassador.
        /// </summary>
        public static ArrivalMode ResolveArrival(int etaDays, int daysUntilDeadline)
            => etaDays <= 0 || etaDays <= daysUntilDeadline
                ? ArrivalMode.InPerson
                : ArrivalMode.Ambassador;
    }
}
