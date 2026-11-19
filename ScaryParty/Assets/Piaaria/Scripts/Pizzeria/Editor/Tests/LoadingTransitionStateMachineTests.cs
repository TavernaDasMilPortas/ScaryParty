using NUnit.Framework;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class LoadingTransitionStateMachineTests
    {
        [Test]
        public void HappyPath_TransitionsAndReleasesInputOnce()
        {
            int lockAcquire = 0;
            int lockRelease = 0;
            var sm = new LoadingTransitionStateMachine(() => lockAcquire++, () => lockRelease++);

            Assert.AreEqual(LoadingOperationState.Idle, sm.State);
            Assert.IsTrue(sm.TryBeginConnecting("ReadyScene", "ReadyRoom", 10f, 15f, out int token));
            Assert.AreEqual(LoadingOperationState.Connecting, sm.State);
            Assert.AreEqual(1, lockAcquire);
            Assert.IsTrue(sm.InputLocked);

            Assert.IsTrue(sm.PromoteConnectingToSceneLoad(token, "ReadyScene", "ReadyRoom", 11f, 30f));
            Assert.AreEqual(LoadingOperationState.LoadingScene, sm.State);

            Assert.IsTrue(sm.AdvanceConfirmedProgress(token, LoadingOperationState.WaitingReadiness, 0.75f));
            Assert.AreEqual(LoadingOperationState.WaitingReadiness, sm.State);
            Assert.LessOrEqual(sm.DisplayedProgress, sm.ConfirmedProgress);

            sm.StepDisplayedProgress(10f);
            Assert.AreEqual(0.75f, sm.DisplayedProgress, 0.0001f);

            Assert.IsTrue(sm.MarkReady(token));
            Assert.AreEqual(LoadingOperationState.Ready, sm.State);
            Assert.IsTrue(sm.BeginFadeOut(token));
            Assert.AreEqual(LoadingOperationState.FadingOut, sm.State);

            Assert.IsTrue(sm.Complete(token));
            Assert.AreEqual(LoadingOperationState.Completed, sm.State);
            Assert.IsFalse(sm.Complete(token)); // idempotent
            Assert.AreEqual(1, lockAcquire);
            Assert.AreEqual(1, lockRelease);
            Assert.IsFalse(sm.InputLocked);
        }

        [Test]
        public void DuplicateBeginConnecting_IsRejectedWhileActive()
        {
            var sm = new LoadingTransitionStateMachine();
            Assert.IsTrue(sm.TryBeginConnecting("ReadyScene", "ReadyRoom", 0f, 10f, out int firstToken));
            Assert.IsFalse(sm.TryBeginConnecting("GameScene", "Gameplay", 1f, 10f, out int secondToken));
            Assert.AreEqual(firstToken, secondToken);
            Assert.AreEqual("ReadyScene", sm.TargetScene);
        }

        [Test]
        public void TimeoutAndRetry_CreatesNewTokenAndIgnoresStaleToken()
        {
            int lockAcquire = 0;
            int lockRelease = 0;
            var sm = new LoadingTransitionStateMachine(() => lockAcquire++, () => lockRelease++);

            Assert.IsTrue(sm.TryBeginConnecting(null, "MenuOrLobby", 5f, 10f, out int oldToken));
            Assert.IsFalse(sm.IsTimedOut(14.9f));
            Assert.IsTrue(sm.IsTimedOut(15.0f));

            Assert.IsTrue(sm.Fail(oldToken, "Timeout"));
            Assert.AreEqual(LoadingOperationState.ErrorVisible, sm.State);
            Assert.AreEqual("Timeout", sm.FailureReason);

            Assert.IsTrue(sm.Retry(16f, 12f, out int newToken));
            Assert.Greater(newToken, oldToken);
            Assert.AreEqual(LoadingOperationState.Connecting, sm.State);
            Assert.IsNull(sm.FailureReason);

            // Callback atrasado do token antigo deve ser ignorado
            Assert.IsFalse(sm.Fail(oldToken, "Stale error"));
            Assert.IsFalse(sm.AdvanceConfirmedProgress(oldToken, LoadingOperationState.LoadingScene, 0.5f));
            Assert.AreEqual(LoadingOperationState.Connecting, sm.State);

            Assert.IsTrue(sm.Complete(newToken));
            Assert.AreEqual(1, lockAcquire);
            Assert.AreEqual(1, lockRelease);
        }

        [Test]
        public void CancelAndDestroy_AlwaysReleaseInputLockOnce()
        {
            int lockAcquire = 0;
            int lockRelease = 0;
            var sm = new LoadingTransitionStateMachine(() => lockAcquire++, () => lockRelease++);

            Assert.IsTrue(sm.TryBeginConnecting("ReadyScene", "ReadyRoom", 0f, 10f, out int token1));
            Assert.IsTrue(sm.Cancel(token1));
            Assert.AreEqual(LoadingOperationState.Idle, sm.State);
            Assert.IsTrue(sm.IsCancelled);
            Assert.AreEqual(1, lockRelease);
            Assert.IsFalse(sm.Cancel(token1));

            Assert.IsTrue(sm.TryBeginConnecting("GameScene", "Gameplay", 1f, 10f, out int token2));
            Assert.IsTrue(sm.Fail(token2, "Erro de rede"));
            sm.ForceReleaseOnDestroy();
            sm.ForceReleaseOnDestroy();
            Assert.AreEqual(2, lockAcquire);
            Assert.AreEqual(2, lockRelease);
            Assert.IsFalse(sm.InputLocked);
        }
    }
}
