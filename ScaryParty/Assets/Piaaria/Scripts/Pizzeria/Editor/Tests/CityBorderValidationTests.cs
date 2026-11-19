using System.Collections.Generic;
using NUnit.Framework;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class CityBorderValidationTests
    {
        [Test]
        public void ComputeGapMetrics_ClosedCoverage_ReturnsZeroGaps()
        {
            var intervals = new List<(float, float)> { (0f, 6f), (5.5f, 12f), (11.8f, 20f) };
            StreetGridGenerator.ComputeGapMetrics(20f, intervals, out float maxGap, out float totalGap);

            Assert.AreEqual(0f, maxGap, 0.001f);
            Assert.AreEqual(0f, totalGap, 0.001f);
        }

        [Test]
        public void ComputeGapMetrics_IntentionalGaps_MeasuresMaxAndTotalAccurately()
        {
            var intervals = new List<(float, float)> { (0f, 5f), (8f, 14f) };
            StreetGridGenerator.ComputeGapMetrics(20f, intervals, out float maxGap, out float totalGap);

            // Gaps: [5..8]=3m e [14..20]=6m
            Assert.AreEqual(6f, maxGap, 0.001f);
            Assert.AreEqual(9f, totalGap, 0.001f);
        }

        [Test]
        public void ValidationReport_DefaultZeroEdges_IsNeverComplete()
        {
            var emptyReport = default(StreetGridGenerator.CityBorderValidationReport);
            Assert.IsFalse(emptyReport.IsComplete);

            var overlapReport = new StreetGridGenerator.CityBorderValidationReport
            {
                totalEdgesEvaluated = 4,
                maxUncoveredGapMeters = 0f,
                streetOverlapCount = 1
            };
            Assert.IsFalse(overlapReport.IsComplete);

            var validReport = new StreetGridGenerator.CityBorderValidationReport
            {
                totalEdgesEvaluated = 4,
                maxUncoveredGapMeters = 0.1f,
                streetOverlapCount = 0
            };
            Assert.IsTrue(validReport.IsComplete);
        }
    }
}
