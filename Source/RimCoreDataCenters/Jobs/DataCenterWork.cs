using RimWorld;
using UnityEngine;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>
    /// Work speed for data center jobs. Both jobs blend the pawn's vanilla research speed
    /// (Intellectual skill) and construction speed (Construction skill); operations lean on
    /// intellect, servicing leans on construction.
    /// </summary>
    public static class DataCenterWork
    {
        public const float OperationsIntellectWeight = 0.75f;
        public const float ServiceConstructionWeight = 0.75f;

        public static float OperationsSpeed(Pawn pawn)
        {
            return Blend(pawn, OperationsIntellectWeight);
        }

        public static float ServiceSpeed(Pawn pawn)
        {
            return Blend(pawn, 1f - ServiceConstructionWeight);
        }

        private static float Blend(Pawn pawn, float intellectWeight)
        {
            if (pawn == null)
            {
                return 1f;
            }
            float research = pawn.GetStatValue(StatDefOf.ResearchSpeed);
            float construction = pawn.GetStatValue(StatDefOf.ConstructionSpeed);
            float speed = research * intellectWeight + construction * (1f - intellectWeight);
            return Mathf.Max(0.1f, speed);
        }
    }
}
