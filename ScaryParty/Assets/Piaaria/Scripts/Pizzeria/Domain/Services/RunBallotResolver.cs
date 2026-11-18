using System;
using System.Collections.Generic;

namespace ScaryParty.Pizzeria.Domain.Services
{
    /// <summary>
    /// Estado de ciclo de vida da Intermissão 2.1 entre noites.
    /// </summary>
    public enum IntermissionStage
    {
        None = 0,
        Open = 1,
        Locked = 2,
        Revealing = 3,
        Countdown = 4,
        Starting = 5
    }

    /// <summary>
    /// Entrada pura de uma opção candidata em uma urna ponderada.
    /// </summary>
    public readonly struct BallotCandidate
    {
        public readonly string OptionId;
        public readonly int VoteCount;

        public BallotCandidate(string optionId, int voteCount)
        {
            OptionId = optionId ?? string.Empty;
            VoteCount = voteCount < 0 ? 0 : voteCount;
        }
    }

    /// <summary>
    /// Resultado puro da apuração de uma urna ponderada por bilhetes.
    /// </summary>
    public readonly struct BallotResolutionResult
    {
        public readonly string WinningOptionId;
        public readonly int TotalTickets;
        public readonly int WinningRoll;
        public readonly bool UsedUniformFallback;

        public BallotResolutionResult(
            string winningOptionId,
            int totalTickets,
            int winningRoll,
            bool usedUniformFallback)
        {
            WinningOptionId = winningOptionId ?? string.Empty;
            TotalTickets = totalTickets;
            WinningRoll = winningRoll;
            UsedUniformFallback = usedUniformFallback;
        }
    }

    /// <summary>
    /// Resolvedor puro de sorteio ponderado por votos (tickets) para a Intermissão 2.1.
    /// Cada voto vale 1 bilhete; 0 votos totais aciona fallback uniforme entre opções válidas.
    /// </summary>
    public static class RunBallotResolver
    {
        public static BallotResolutionResult ResolveWeighted(
            IReadOnlyList<BallotCandidate> candidates,
            Func<int, int> nextIntExclusive)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return new BallotResolutionResult(string.Empty, 0, 0, true);
            }

            if (nextIntExclusive == null)
            {
                throw new ArgumentNullException(nameof(nextIntExclusive));
            }

            int totalTickets = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!string.IsNullOrEmpty(candidates[i].OptionId) && candidates[i].VoteCount > 0)
                {
                    totalTickets += candidates[i].VoteCount;
                }
            }

            if (totalTickets <= 0)
            {
                int uniformIndex = ClampIndex(nextIntExclusive(candidates.Count), candidates.Count);
                return new BallotResolutionResult(
                    candidates[uniformIndex].OptionId,
                    0,
                    uniformIndex,
                    usedUniformFallback: true);
            }

            int roll = ClampIndex(nextIntExclusive(totalTickets), totalTickets);
            int cumulative = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                int votes = candidates[i].VoteCount;
                if (votes <= 0 || string.IsNullOrEmpty(candidates[i].OptionId))
                {
                    continue;
                }

                cumulative += votes;
                if (roll < cumulative)
                {
                    return new BallotResolutionResult(
                        candidates[i].OptionId,
                        totalTickets,
                        roll,
                        usedUniformFallback: false);
                }
            }

            return new BallotResolutionResult(
                candidates[candidates.Count - 1].OptionId,
                totalTickets,
                roll,
                usedUniformFallback: false);
        }

        private static int ClampIndex(int value, int count)
        {
            if (count <= 0) return 0;
            if (value < 0) return 0;
            if (value >= count) return count - 1;
            return value;
        }
    }
}
