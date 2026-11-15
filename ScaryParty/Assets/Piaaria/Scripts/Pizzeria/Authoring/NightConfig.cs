using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Services;

namespace ScaryParty.Pizzeria.Authoring
{
    [Serializable]
    public struct NightLevelEntry
    {
        public int nightNumber;
        public float durationSeconds;
        public int revenueGoal;
        public float prepSeconds;
        public float closingGraceSeconds;
    }

    [CreateAssetMenu(fileName = "NightConfig", menuName = "Scary Party/Pizzeria/Night Config")]
    public class NightConfig : ScriptableObject
    {
        public List<NightLevelEntry> nights = new List<NightLevelEntry>
        {
            new NightLevelEntry { nightNumber = 1, durationSeconds = 360f, revenueGoal = 60, prepSeconds = 4f, closingGraceSeconds = 20f },
            new NightLevelEntry { nightNumber = 2, durationSeconds = 390f, revenueGoal = 110, prepSeconds = 4f, closingGraceSeconds = 20f },
            new NightLevelEntry { nightNumber = 3, durationSeconds = 420f, revenueGoal = 170, prepSeconds = 4f, closingGraceSeconds = 20f },
        };

        public NightGoalParameters ResolveParameters(int nightNumber)
        {
            int target = Mathf.Max(1, nightNumber);
            if (nights != null)
            {
                for (int i = 0; i < nights.Count; i++)
                {
                    if (nights[i].nightNumber == target)
                    {
                        var e = nights[i];
                        return new NightGoalParameters(
                            e.nightNumber,
                            e.durationSeconds > 10f ? e.durationSeconds : 360f,
                            e.revenueGoal > 0 ? e.revenueGoal : 60,
                            e.prepSeconds >= 0f ? e.prepSeconds : 4f,
                            e.closingGraceSeconds >= 0f ? e.closingGraceSeconds : 20f);
                    }
                }
            }

            return NightCycleService.GetDefaultParameters(target);
        }
    }
}
