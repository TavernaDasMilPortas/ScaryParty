using System.Diagnostics;
using ScaryParty.VisualStyles;
using UnityEngine;

namespace ScaryParty.Lighting
{
    /// <summary>
    /// Gate leve para garantir que o primeiro frame renderizado ja tenha estilo, hora do dia
    /// e contratos B0 avaliados de forma deterministica, sem qualquer bake ao abrir partida.
    /// </summary>
    public sealed class LightingReadyGate : MonoBehaviour
    {
        public static LightingReadyGate Active { get; private set; }

        public bool IsReady { get; private set; }
        public float LastWarmupMilliseconds { get; private set; }

        private void Awake()
        {
            Active = this;
        }

        private void Start()
        {
            EnsureReadyForFirstFrame();
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        public void EnsureReadyForFirstFrame()
        {
            var sw = Stopwatch.StartNew();

            VisualStyleGameplayBootstrap.EvaluateActiveScene();

            if (TimeOfDayLightingDirector.Active != null && TimeOfDayLightingDirector.Active.IsActivelyControllingScene)
            {
                TimeOfDayLightingDirector.Active.EvaluateAndApply(TimeOfDayLightingDirector.Active.currentClockHours);
            }

            sw.Stop();
            LastWarmupMilliseconds = (float)sw.Elapsed.TotalMilliseconds;
            IsReady = true;
        }
    }
}
