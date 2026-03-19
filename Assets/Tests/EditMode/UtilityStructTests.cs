using Core.Simulation.Commands;
using Core.Simulation.Runtime;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class UtilityStructTests
    {
        // ── TransferPlanBuffer ──

        [Test]
        public void TransferPlanBuffer_Add_And_ToBatch_Creates_Correct_Command()
        {
            var buffer = new TransferPlanBuffer();
            buffer.Add(targetIndex: 10, plannedMass: 500_000);
            buffer.Add(targetIndex: 20, plannedMass: 300_000);

            Assert.That(buffer.Count, Is.EqualTo(2));

            FlowBatchCommand batch = buffer.ToBatch(
                sourceIndex: 5,
                elementId: 3,
                temperature: 25,
                mode: FlowBatchMode.Normal);

            Assert.That(batch.SourceIndex, Is.EqualTo(5));
            Assert.That(batch.ElementId, Is.EqualTo(3));
            Assert.That(batch.SourceTemperature, Is.EqualTo(25));
            Assert.That(batch.TransferCount, Is.EqualTo(2));
            Assert.That(batch.Transfer0.TargetIndex, Is.EqualTo(10));
            Assert.That(batch.Transfer0.PlannedMass, Is.EqualTo(500_000));
            Assert.That(batch.Transfer1.TargetIndex, Is.EqualTo(20));
            Assert.That(batch.Transfer1.PlannedMass, Is.EqualTo(300_000));
        }

        [Test]
        public void TransferPlanBuffer_Max_Four_Transfers()
        {
            var buffer = new TransferPlanBuffer();
            buffer.Add(1, 100);
            buffer.Add(2, 200);
            buffer.Add(3, 300);
            buffer.Add(4, 400);
            buffer.Add(5, 500); // 5번째는 무시

            Assert.That(buffer.Count, Is.EqualTo(4));
        }

        // ── CandidateSet ──

        [Test]
        public void CandidateSet_Add_And_Get()
        {
            var set = new CandidateSet();
            set.Add(index: 10, actionType: DirectionCandidate.ActionFlow, weight: 1.0f);
            set.Add(index: 20, actionType: DirectionCandidate.ActionSwap, weight: 2.0f);

            Assert.That(set.Count, Is.EqualTo(2));
            Assert.That(set.Get(0).Index, Is.EqualTo(10));
            Assert.That(set.Get(0).ActionType, Is.EqualTo(DirectionCandidate.ActionFlow));
            Assert.That(set.Get(1).Index, Is.EqualTo(20));
            Assert.That(set.Get(1).ActionType, Is.EqualTo(DirectionCandidate.ActionSwap));
        }

        [Test]
        public void CandidateSet_TotalWeight()
        {
            var set = new CandidateSet();
            set.Add(10, 0, 1.0f);
            set.Add(20, 0, 2.0f);
            set.Add(30, 0, 3.0f);

            Assert.That(set.TotalWeight(), Is.EqualTo(6.0f).Within(0.001f));
        }

        [Test]
        public void CandidateSet_SelectByWeight_Picks_Correct_Candidate()
        {
            var set = new CandidateSet();
            set.Add(10, 0, 1.0f); // 0~1
            set.Add(20, 0, 3.0f); // 1~4

            // normalizedRandom=0.1 → 0.1*4=0.4 → 첫 번째 (weight 1.0)
            var picked = set.SelectByWeight(0.1f);
            Assert.That(picked.Index, Is.EqualTo(10));

            // normalizedRandom=0.5 → 0.5*4=2.0 → 두 번째 (weight 3.0)
            picked = set.SelectByWeight(0.5f);
            Assert.That(picked.Index, Is.EqualTo(20));
        }

        [Test]
        public void CandidateSet_Max_Four_Candidates()
        {
            var set = new CandidateSet();
            set.Add(1, 0, 1f);
            set.Add(2, 0, 1f);
            set.Add(3, 0, 1f);
            set.Add(4, 0, 1f);
            set.Add(5, 0, 1f); // 5번째는 무시

            Assert.That(set.Count, Is.EqualTo(4));
        }
    }
}