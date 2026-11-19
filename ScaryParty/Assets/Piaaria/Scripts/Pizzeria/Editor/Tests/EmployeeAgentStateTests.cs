using NUnit.Framework;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class EmployeeAgentStateTests
    {
        [Test]
        public void MapToHighLevelState_MapsIdleBlockedDeadAndWorkSteps()
        {
            Assert.AreEqual(
                EmployeeHighLevelState.RecoverReplan,
                EmployeeAgentController.MapToHighLevelState(EmployeeAgentController.WorkStep.CookAssemble, EmployeeBlockReason.None, isAlive: false));

            Assert.AreEqual(
                EmployeeHighLevelState.Blocked,
                EmployeeAgentController.MapToHighLevelState(EmployeeAgentController.WorkStep.CookMoveToOven, EmployeeBlockReason.PathBlocked, isAlive: true));

            Assert.AreEqual(
                EmployeeHighLevelState.IdleObserve,
                EmployeeAgentController.MapToHighLevelState(EmployeeAgentController.WorkStep.Idle, EmployeeBlockReason.None, isAlive: true));

            Assert.AreEqual(
                EmployeeHighLevelState.Navigate,
                EmployeeAgentController.MapToHighLevelState(EmployeeAgentController.WorkStep.CashierMoveToCounter, EmployeeBlockReason.None, isAlive: true));

            Assert.AreEqual(
                EmployeeHighLevelState.Work,
                EmployeeAgentController.MapToHighLevelState(EmployeeAgentController.WorkStep.CookBakeInOven, EmployeeBlockReason.None, isAlive: true));

            Assert.AreEqual(
                EmployeeHighLevelState.Deliver,
                EmployeeAgentController.MapToHighLevelState(EmployeeAgentController.WorkStep.CookDeliverToStaging, EmployeeBlockReason.None, isAlive: true));
        }

        [Test]
        public void EmployeeStationCache_Invalidate_DoesNotThrow()
        {
            Assert.DoesNotThrow(EmployeeStationCache.Invalidate);
        }
    }
}
