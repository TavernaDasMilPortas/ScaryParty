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

        [Test]
        public void DismissErrorToIdle_AllowsFreshConnectingAttemptWithBalancedLock()
        {
            int lockAcquire = 0;
            int lockRelease = 0;
            var sm = new LoadingTransitionStateMachine(() => lockAcquire++, () => lockRelease++);

            Assert.IsTrue(sm.TryBeginConnecting(null, "MenuOrLobby", 0f, 15f, out int t1));
            Assert.IsTrue(sm.Fail(t1, "Relay offline"));
            sm.DismissErrorToIdle();
            Assert.AreEqual(LoadingOperationState.Idle, sm.State);
            Assert.IsFalse(sm.InputLocked);

            Assert.IsTrue(sm.TryBeginConnecting(null, "MenuOrLobby", 1f, 15f, out int t2));
            Assert.Greater(t2, t1);
            Assert.AreEqual(LoadingOperationState.Connecting, sm.State);
            Assert.IsTrue(sm.InputLocked);
            Assert.AreEqual(2, lockAcquire);
            Assert.AreEqual(1, lockRelease);
        }

        [Test]
        public void SceneManagerRehookAndClientDisconnectGuards_HandleSecondSessionAndOfflineReturn()
        {
            var objA = new object();
            var objB = new object();
            Assert.IsFalse(PizzeriaSceneLoadingCoordinator.ShouldRehookInstance(objA, objA));
            Assert.IsTrue(PizzeriaSceneLoadingCoordinator.ShouldRehookInstance(objA, objB));
            Assert.IsFalse(PizzeriaSceneLoadingCoordinator.ShouldRehookInstance(objA, null));

            // Offline transition back to Lobby must NOT abort when client disconnect fires
            Assert.IsFalse(PizzeriaSceneLoadingCoordinator.ShouldAbortOnClientDisconnect(
                LoadingOperationState.LoadingScene, isNetworkedTransition: false, isServer: false, 1UL, 1UL));
            // Networked scene load / readiness / connecting MUST abort for client disconnect
            Assert.IsTrue(PizzeriaSceneLoadingCoordinator.ShouldAbortOnClientDisconnect(
                LoadingOperationState.LoadingScene, isNetworkedTransition: true, isServer: false, 0UL, 1UL));
            Assert.IsTrue(PizzeriaSceneLoadingCoordinator.ShouldAbortOnClientDisconnect(
                LoadingOperationState.WaitingReadiness, isNetworkedTransition: true, isServer: false, 1UL, 1UL));
            Assert.IsTrue(PizzeriaSceneLoadingCoordinator.ShouldAbortOnClientDisconnect(
                LoadingOperationState.Connecting, isNetworkedTransition: false, isServer: false, 1UL, 1UL));
            Assert.IsFalse(PizzeriaSceneLoadingCoordinator.ShouldAbortOnClientDisconnect(
                LoadingOperationState.LoadingScene, isNetworkedTransition: true, isServer: true, 1UL, 0UL));
        }

        [Test]
        public void SessionLaunchConfig_RebindsWhenCustomMessagingRecreated()
        {
            var nm = new object();
            var msg1 = new object();
            var msg2 = new object();
            Assert.IsFalse(ScaryParty.Pizzeria.Network.SessionLaunchConfig.ShouldRebindMessaging(nm, nm, msg1, msg1, true));
            Assert.IsTrue(ScaryParty.Pizzeria.Network.SessionLaunchConfig.ShouldRebindMessaging(nm, nm, msg1, msg2, true));
            Assert.IsTrue(ScaryParty.Pizzeria.Network.SessionLaunchConfig.ShouldRebindMessaging(nm, nm, null, null, false));
        }

        private sealed class FakeUgsLobbyGateway : IUgsLobbyGateway
        {
            public readonly System.Collections.Generic.List<string> DeletedIds = new System.Collections.Generic.List<string>();

            public System.Threading.Tasks.Task SendHeartbeatPingAsync(string lobbyId)
                => System.Threading.Tasks.Task.CompletedTask;

            public System.Threading.Tasks.Task DeleteLobbyAsync(string lobbyId)
            {
                DeletedIds.Add(lobbyId);
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }

        [Test]
        public void UgsLobbySessionService_PersistsAcrossSceneChanges_UntilExplicitStopOrReplacement()
        {
            var gw = new FakeUgsLobbyGateway();
            UgsLobbySessionService.SetGatewayForTests(gw);
            try
            {
                UgsLobbySessionService.AdoptHostedLobby("lobby_1", 60000);
                Assert.AreEqual("lobby_1", UgsLobbySessionService.ActiveLobbyId);
                Assert.AreEqual(0, gw.DeletedIds.Count);

                UgsLobbySessionService.AdoptHostedLobby("lobby_2", 60000);
                Assert.AreEqual("lobby_2", UgsLobbySessionService.ActiveLobbyId);
                Assert.AreEqual(1, gw.DeletedIds.Count);
                Assert.AreEqual("lobby_1", gw.DeletedIds[0]);

                UgsLobbySessionService.StopHostingLobby();
                Assert.IsNull(UgsLobbySessionService.ActiveLobbyId);
                Assert.AreEqual(2, gw.DeletedIds.Count);
                Assert.AreEqual("lobby_2", gw.DeletedIds[1]);
            }
            finally
            {
                UgsLobbySessionService.SetGatewayForTests(null);
            }
        }
    }
}
