using System;
using System.Collections;
using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Presentation
{
    public class OvenTransferVisualizer : MonoBehaviour
    {
        public static OvenTransferVisualizer Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public static void EnsureInstance()
        {
            if (Instance == null)
            {
                var go = new GameObject("OvenTransferVisualizer");
                Instance = go.AddComponent<OvenTransferVisualizer>();
                DontDestroyOnLoad(go);
            }
        }

        public void PlayTransfer(
            int stationId,
            ulong pizzaId,
            int slotIndex,
            ulong clientId,
            byte handIndex,
            bool isInsertion,
            float doorOpenDuration = 0.25f,
            float flightDuration = 0.40f,
            float doorCloseDuration = 0.25f,
            Action onFlightComplete = null)
        {
            StartCoroutine(TransferRoutine(stationId, pizzaId, slotIndex, clientId, handIndex, isInsertion, doorOpenDuration, flightDuration, doorCloseDuration, onFlightComplete));
        }

        private IEnumerator TransferRoutine(
            int stationId,
            ulong pizzaId,
            int slotIndex,
            ulong clientId,
            byte handIndex,
            bool isInsertion,
            float doorOpenDuration,
            float flightDuration,
            float doorCloseDuration,
            Action onFlightComplete)
        {
            StationRegistry.TryGetStation(stationId, out var stationTrans);
            StationDoorPresenter presenter = stationTrans != null ? stationTrans.GetComponent<StationDoorPresenter>() : null;
            OvenStation oven = stationTrans != null ? stationTrans.GetComponent<OvenStation>() : null;

            // 1. Abrir a porta antes da passagem da pizza
            presenter?.SetTransferActive(true);
            yield return new WaitForSeconds(Mathf.Max(0.05f, doorOpenDuration));

            // 2. Localizar visual e âncoras espaciais
            var itemVis = PizzeriaItemVisualizer.Instance;
            if (itemVis != null)
            {
                itemVis.SetItemInTransit(pizzaId, true);
            }

            ItemVisualWrapper wrapper = itemVis != null ? itemVis.GetItemVisual(pizzaId) : null;

            Transform handAnchor = itemVis != null ? itemVis.GetPlayerHandAnchor(clientId, handIndex) : null;
            Transform entryAnchor = oven != null ? oven.GetEntryAnchor() : stationTrans;
            Transform slotAnchor = null;
            if (oven != null && oven.slotAnchors != null && slotIndex >= 0 && slotIndex < oven.slotAnchors.Length)
            {
                slotAnchor = oven.slotAnchors[slotIndex];
            }

            Vector3 pStart, pMid, pEnd;
            Quaternion rStart, rEnd;

            Vector3 handPos = handAnchor != null ? handAnchor.position : (stationTrans != null ? stationTrans.position + Vector3.up * 1.0f - stationTrans.forward * 1.2f : Vector3.zero);
            Quaternion handRot = handAnchor != null ? handAnchor.rotation : Quaternion.identity;

            Vector3 midPos = entryAnchor != null ? entryAnchor.position : (stationTrans != null ? stationTrans.position + Vector3.up * 0.45f : Vector3.zero);

            Vector3 slotPos = slotAnchor != null ? slotAnchor.position : (stationTrans != null ? oven.GetSlotPosition(slotIndex) : Vector3.zero);
            Quaternion slotRot = slotAnchor != null ? slotAnchor.rotation : (stationTrans != null ? stationTrans.rotation : Quaternion.identity);

            if (isInsertion)
            {
                pStart = handPos;
                rStart = handRot;
                pMid = midPos;
                pEnd = slotPos;
                rEnd = slotRot;
            }
            else
            {
                pStart = slotPos;
                rStart = slotRot;
                pMid = midPos;
                pEnd = handPos;
                rEnd = handRot;
            }

            // 3. Animar trajetória parabólica através da boca do forno
            float elapsed = 0f;
            float totalFlight = Mathf.Max(0.1f, flightDuration);

            while (elapsed < totalFlight)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / totalFlight);
                float curvedT = Mathf.SmoothStep(0f, 1f, t);

                Vector3 currentPos = SampleTrajectory(pStart, pMid, pEnd, curvedT, arcHeight: 0.15f);
                Quaternion currentRot = Quaternion.Slerp(rStart, rEnd, curvedT);

                if (wrapper != null && wrapper.transform != null)
                {
                    wrapper.transform.position = currentPos;
                    wrapper.transform.rotation = currentRot;
                }

                yield return null;
            }

            // Garante posição final
            if (wrapper != null && wrapper.transform != null)
            {
                wrapper.transform.position = pEnd;
                wrapper.transform.rotation = rEnd;
            }

            // 4. Liberar flag in-transit e confirmar chegada
            if (itemVis != null)
            {
                itemVis.SetItemInTransit(pizzaId, false);
            }

            onFlightComplete?.Invoke();

            // 5. Fechar porta vertical após a pizza estar totalmente posicionada
            presenter?.SetTransferActive(false);
            yield return new WaitForSeconds(Mathf.Max(0.05f, doorCloseDuration));
            presenter?.CloseAndSuppressHover();
        }

        private Vector3 SampleTrajectory(Vector3 start, Vector3 mid, Vector3 end, float t, float arcHeight)
        {
            // Curva de Bezier quadrática de 3 pontos com arco vertical adicional
            float oneMinusT = 1f - t;
            Vector3 pos = (oneMinusT * oneMinusT * start) + (2f * oneMinusT * t * mid) + (t * t * end);
            float parabola = 4f * arcHeight * t * (1f - t);
            return pos + Vector3.up * parabola;
        }
    }
}
