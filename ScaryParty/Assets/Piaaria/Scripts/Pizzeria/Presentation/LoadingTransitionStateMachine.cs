using System;
using UnityEngine;

namespace ScaryParty.Pizzeria.Presentation
{
    public enum LoadingOperationState
    {
        Idle = 0,
        Connecting = 1,
        LoadingScene = 2,
        WaitingReadiness = 3,
        Ready = 4,
        FadingOut = 5,
        Completed = 6,
        Failed = 7,
        ErrorVisible = 8
    }

    /// <summary>
    /// Máquina de estados pura para operações de conexão e transição de cena.
    /// Garante token único por tentativa, progresso monotônico <= confirmado e liberação única do input.
    /// </summary>
    public sealed class LoadingTransitionStateMachine
    {
        private readonly Action _acquireInputLock;
        private readonly Action _releaseInputLock;

        public LoadingOperationState State { get; private set; } = LoadingOperationState.Idle;
        public int OperationToken { get; private set; }
        public string TargetScene { get; private set; }
        public string ReadinessProfileName { get; private set; }
        public float StartRealtime { get; private set; }
        public float DeadlineRealtime { get; private set; }
        public float ConfirmedProgress { get; private set; }
        public float DisplayedProgress { get; private set; }
        public bool IsCancelled { get; private set; }
        public bool IsFinalized { get; private set; }
        public string FailureReason { get; private set; }
        public bool InputLocked { get; private set; }

        public bool IsActiveState =>
            State == LoadingOperationState.Connecting ||
            State == LoadingOperationState.LoadingScene ||
            State == LoadingOperationState.WaitingReadiness ||
            State == LoadingOperationState.Ready ||
            State == LoadingOperationState.FadingOut;

        public LoadingTransitionStateMachine(Action acquireInputLock = null, Action releaseInputLock = null)
        {
            _acquireInputLock = acquireInputLock;
            _releaseInputLock = releaseInputLock;
        }

        public bool TryBeginConnecting(
            string targetScene,
            string readinessProfileName,
            float nowRealtime,
            float timeoutSeconds,
            out int token)
        {
            if (IsActiveState)
            {
                token = OperationToken;
                return false;
            }

            return StartNewOperation(
                LoadingOperationState.Connecting,
                targetScene,
                readinessProfileName,
                nowRealtime,
                timeoutSeconds,
                0.02f,
                out token);
        }

        public int BeginSceneTransition(
            string targetScene,
            string readinessProfileName,
            float nowRealtime,
            float timeoutSeconds)
        {
            StartNewOperation(
                LoadingOperationState.LoadingScene,
                targetScene,
                readinessProfileName,
                nowRealtime,
                timeoutSeconds,
                Mathf.Max(0.05f, State == LoadingOperationState.Connecting ? ConfirmedProgress : 0.02f),
                out int token);
            return token;
        }

        public bool PromoteConnectingToSceneLoad(
            int token,
            string targetScene,
            string readinessProfileName,
            float nowRealtime,
            float timeoutSeconds)
        {
            if (!IsTokenActive(token) || State != LoadingOperationState.Connecting)
                return false;

            TargetScene = targetScene;
            ReadinessProfileName = readinessProfileName;
            StartRealtime = nowRealtime;
            DeadlineRealtime = nowRealtime + Mathf.Max(1f, timeoutSeconds);
            State = LoadingOperationState.LoadingScene;
            ConfirmedProgress = Mathf.Max(ConfirmedProgress, 0.08f);
            return true;
        }

        public bool AdvanceConfirmedProgress(int token, LoadingOperationState newState, float progress01)
        {
            if (!IsTokenActive(token)) return false;
            if (newState != LoadingOperationState.Connecting &&
                newState != LoadingOperationState.LoadingScene &&
                newState != LoadingOperationState.WaitingReadiness &&
                newState != LoadingOperationState.Ready &&
                newState != LoadingOperationState.FadingOut)
            {
                return false;
            }

            if ((int)newState < (int)State) return false;

            State = newState;
            ConfirmedProgress = Mathf.Max(ConfirmedProgress, Mathf.Clamp01(progress01));
            return true;
        }

        public bool MarkReady(int token)
        {
            if (!IsTokenActive(token)) return false;
            if (State == LoadingOperationState.FadingOut) return false;

            State = LoadingOperationState.Ready;
            ConfirmedProgress = 1f;
            DisplayedProgress = 1f;
            return true;
        }

        public bool BeginFadeOut(int token)
        {
            if (!IsTokenActive(token)) return false;
            if (State != LoadingOperationState.Ready && State != LoadingOperationState.WaitingReadiness)
                return false;

            State = LoadingOperationState.FadingOut;
            return true;
        }

        public bool Complete(int token)
        {
            if (token != OperationToken || IsFinalized || !IsActiveState)
                return false;

            State = LoadingOperationState.Completed;
            ConfirmedProgress = 1f;
            DisplayedProgress = 1f;
            IsFinalized = true;
            ReleaseInputLockOnce();
            return true;
        }

        public bool Fail(int token, string reason)
        {
            if (token != OperationToken || IsFinalized || !IsActiveState)
                return false;

            FailureReason = string.IsNullOrWhiteSpace(reason) ? "Falha na transição." : reason;
            State = LoadingOperationState.Failed;
            State = LoadingOperationState.ErrorVisible;
            IsFinalized = true;
            return true;
        }

        public bool Cancel(int token)
        {
            if (token != OperationToken || IsFinalized)
                return false;

            if (!IsActiveState && State != LoadingOperationState.ErrorVisible && State != LoadingOperationState.Failed)
                return false;

            IsCancelled = true;
            IsFinalized = true;
            FailureReason = null;
            State = LoadingOperationState.Idle;
            ReleaseInputLockOnce();
            return true;
        }

        public bool Retry(float nowRealtime, float timeoutSeconds, out int newToken)
        {
            if (State != LoadingOperationState.ErrorVisible && State != LoadingOperationState.Failed)
            {
                newToken = 0;
                return false;
            }

            return StartNewOperation(
                LoadingOperationState.Connecting,
                TargetScene,
                ReadinessProfileName,
                nowRealtime,
                timeoutSeconds,
                0.02f,
                out newToken);
        }

        public void DismissErrorToIdle()
        {
            if (State == LoadingOperationState.ErrorVisible || State == LoadingOperationState.Failed)
            {
                State = LoadingOperationState.Idle;
                FailureReason = null;
            }
            ReleaseInputLockOnce();
        }

        public void ForceReleaseOnDestroy()
        {
            if (IsActiveState)
            {
                IsCancelled = true;
                IsFinalized = true;
                State = LoadingOperationState.Idle;
            }
            ReleaseInputLockOnce();
        }

        public float StepDisplayedProgress(float maxDelta)
        {
            if (!IsActiveState) return DisplayedProgress;
            float clampedDelta = Mathf.Max(0f, maxDelta);
            DisplayedProgress = Mathf.Min(ConfirmedProgress, Mathf.MoveTowards(DisplayedProgress, ConfirmedProgress, clampedDelta));
            return DisplayedProgress;
        }

        public bool IsTimedOut(float nowRealtime)
        {
            return IsActiveState && nowRealtime >= DeadlineRealtime;
        }

        public bool IsTokenActive(int token)
        {
            return token == OperationToken && !IsFinalized && IsActiveState;
        }

        private bool StartNewOperation(
            LoadingOperationState initialState,
            string targetScene,
            string readinessProfileName,
            float nowRealtime,
            float timeoutSeconds,
            float initialProgress,
            out int token)
        {
            OperationToken++;
            token = OperationToken;
            TargetScene = targetScene;
            ReadinessProfileName = readinessProfileName;
            StartRealtime = nowRealtime;
            DeadlineRealtime = nowRealtime + Mathf.Max(1f, timeoutSeconds);
            ConfirmedProgress = Mathf.Clamp01(initialProgress);
            DisplayedProgress = ConfirmedProgress;
            IsCancelled = false;
            IsFinalized = false;
            FailureReason = null;
            State = initialState;
            AcquireInputLockOnce();
            return true;
        }

        private void AcquireInputLockOnce()
        {
            if (InputLocked) return;
            InputLocked = true;
            _acquireInputLock?.Invoke();
        }

        private void ReleaseInputLockOnce()
        {
            if (!InputLocked) return;
            InputLocked = false;
            _releaseInputLock?.Invoke();
        }
    }
}
