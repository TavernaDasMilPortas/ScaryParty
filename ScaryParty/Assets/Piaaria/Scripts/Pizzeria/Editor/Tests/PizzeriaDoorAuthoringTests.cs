#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class PizzeriaDoorAuthoringTests
    {
        private GameObject _stationGo;
        private StationDoorPresenter _presenter;
        private TransformDoorView _doorView;

        [SetUp]
        public void SetUp()
        {
            _stationGo = new GameObject("TestStorageStation");
            _presenter = _stationGo.AddComponent<StationDoorPresenter>();

            var pivotGo = new GameObject("DoorPivot");
            pivotGo.transform.SetParent(_stationGo.transform, false);
            _doorView = pivotGo.AddComponent<TransformDoorView>();
            _doorView.motionMode = DoorMotionMode.Rotation;
            _doorView.localAxis = Vector3.up;
            _doorView.openAmount = 90f;
            _doorView.hoverAmount = 8f;
            _doorView.durationOpen = 0.1f;
            _doorView.durationClose = 0.1f;
            _doorView.durationHover = 0.1f;
            _doorView.CaptureClosedPose();

            _presenter.SetDoors(new[] { _doorView });
        }

        [TearDown]
        public void TearDown()
        {
            if (_stationGo != null) Object.DestroyImmediate(_stationGo);
        }

        [Test]
        public void TransformDoorView_ClosedTarget_HasZeroFraction()
        {
            _doorView.SetTarget(DoorVisualTarget.Closed, immediate: true);
            Assert.AreEqual(0f, _doorView.CurrentFraction, 0.001f);
            Assert.AreEqual(DoorVisualTarget.Closed, _doorView.CurrentTarget);
        }

        [Test]
        public void TransformDoorView_HoverTarget_HasExpectedFraction()
        {
            _doorView.SetTarget(DoorVisualTarget.Hover, immediate: true);
            float expectedFraction = 8f / 90f;
            Assert.AreEqual(expectedFraction, _doorView.CurrentFraction, 0.005f);
            Assert.AreEqual(DoorVisualTarget.Hover, _doorView.CurrentTarget);
        }

        [Test]
        public void TransformDoorView_OpenTarget_HasFullFraction()
        {
            _doorView.SetTarget(DoorVisualTarget.Open, immediate: true);
            Assert.AreEqual(1f, _doorView.CurrentFraction, 0.001f);
            Assert.AreEqual(DoorVisualTarget.Open, _doorView.CurrentTarget);
        }

        [Test]
        public void StationDoorPresenter_SessionOpen_OverridesHover()
        {
            _presenter.OnHoverEnter();
            Assert.AreEqual(DoorVisualTarget.Hover, _doorView.CurrentTarget);

            // Abre o menu/sessão: deve prevalecer sobre o hover e abrir para 90°
            _presenter.SetSessionOpen(true);
            Assert.AreEqual(DoorVisualTarget.Open, _doorView.CurrentTarget);
            Assert.IsTrue(_presenter.IsSessionOpen);

            // Mesmo se OnHoverExit for disparado enquanto o menu estiver aberto, a porta permanece aberta
            _presenter.OnHoverExit();
            Assert.AreEqual(DoorVisualTarget.Open, _doorView.CurrentTarget);
        }

        [Test]
        public void StationDoorPresenter_CloseSession_SuppressesHoverUntilCursorRetires()
        {
            _presenter.OnHoverEnter();
            _presenter.SetSessionOpen(true);
            Assert.AreEqual(DoorVisualTarget.Open, _doorView.CurrentTarget);

            // Fechamento de sessão (ex: coleta confirmada): deve fechar a porta e suprimir reabertura por hover
            _presenter.SetSessionOpen(false);
            Assert.AreEqual(DoorVisualTarget.Closed, _doorView.CurrentTarget);
            Assert.IsTrue(_presenter.IsHoverSuppressed);

            // Re-disparar OnHoverEnter sem ter retirado o cursor NÃO deve reabrir
            _presenter.OnHoverEnter();
            Assert.AreEqual(DoorVisualTarget.Closed, _doorView.CurrentTarget);

            // Ao desviar a mira (OnHoverExit), a supressão é liberada
            _presenter.OnHoverExit();
            Assert.IsFalse(_presenter.IsHoverSuppressed);

            // Agora uma nova mira reabre suavemente no hover
            _presenter.OnHoverEnter();
            Assert.AreEqual(DoorVisualTarget.Hover, _doorView.CurrentTarget);
        }

        [Test]
        public void TransformDoorView_TranslationMode_MovesAlongAxis()
        {
            _doorView.motionMode = DoorMotionMode.Translation;
            _doorView.openAmount = 0.55f;
            _doorView.hoverAmount = 0.08f;
            _doorView.CaptureClosedPose();

            _doorView.SetTarget(DoorVisualTarget.Hover, immediate: true);
            Assert.AreEqual(0.08f, _doorView.transform.localPosition.y, 0.005f);

            _doorView.SetTarget(DoorVisualTarget.Open, immediate: true);
            Assert.AreEqual(0.55f, _doorView.transform.localPosition.y, 0.005f);

            _doorView.SetTarget(DoorVisualTarget.Closed, immediate: true);
            Assert.AreEqual(0f, _doorView.transform.localPosition.y, 0.005f);
        }

        [Test]
        public void TransformDoorView_NegativeSignRotation_RotatesOutward()
        {
            _doorView.motionMode = DoorMotionMode.Rotation;
            _doorView.localAxis = Vector3.up;
            _doorView.sign = -1f;
            _doorView.openAmount = 90f;
            _doorView.hoverAmount = 20f;
            _doorView.CaptureClosedPose();

            _doorView.SetTarget(DoorVisualTarget.Hover, immediate: true);
            Quaternion hoverRot = _doorView.transform.localRotation;
            Vector3 rotatedForwardHover = hoverRot * Vector3.forward;
            // -20 graus ao redor de Y deve girar o vetor forward para a esquerda (-X)
            Assert.Less(rotatedForwardHover.x, 0f);

            _doorView.SetTarget(DoorVisualTarget.Open, immediate: true);
            Quaternion openRot = _doorView.transform.localRotation;
            Vector3 rotatedLeft = openRot * new Vector3(-1, 0, 0);
            // Porta estendendo-se em -X aberta a -90 graus deve apontar em -Z (em direção ao usuário)
            Assert.AreEqual(-1f, rotatedLeft.z, 0.005f);
            Assert.AreEqual(0f, rotatedLeft.x, 0.005f);
        }
    }
}
#endif
