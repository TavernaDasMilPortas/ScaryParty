using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordinates signal phases across an intersection.
/// Manages vehicle traffic lights and pedestrian crossings to eliminate conflicts.
/// </summary>
public class IntersectionTrafficController : MonoBehaviour
{
    public enum IntersectionPhase
    {
        PhaseA_VehiclesGreen,
        PhaseA_VehiclesYellow,
        Phase_AllRedTransition,
        PhaseB_VehiclesGreen,
        PhaseB_VehiclesYellow,
        Phase_PedestrianClearance
    }

    [Header("Phase Timers")]
    public float phaseAGreenDuration = 12f;
    public float phaseAYellowDuration = 3f;
    public float allRedDuration = 2f;
    public float phaseBGreenDuration = 10f;
    public float phaseBYellowDuration = 3f;
    public float clearanceDuration = 4f;

    [Header("State")]
    public IntersectionPhase currentPhase = IntersectionPhase.PhaseA_VehiclesGreen;
    public float phaseTimer = 0f;

    [Header("Attached Entities")]
    public List<TrafficLight> trafficLights = new List<TrafficLight>();
    public List<PedestrianCrossing> crossings = new List<PedestrianCrossing>();

    private void Update()
    {
        // A11: relógio determinístico baseado em ServerTime replicado quando conectado
        double clockNow = (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            ? Unity.Netcode.NetworkManager.Singleton.ServerTime.Time
            : Time.timeAsDouble;

        float totalCycle = phaseAGreenDuration + phaseAYellowDuration + allRedDuration +
                           phaseBGreenDuration + phaseBYellowDuration + clearanceDuration;
        if (totalCycle > 0.01f)
        {
            float deterministicOffset = Mathf.Abs(Mathf.Sin(transform.position.x * 0.13f + transform.position.z * 0.17f)) * totalCycle;
            float t = (float)((clockNow + deterministicOffset) % totalCycle);

            if (t < phaseAGreenDuration)
            {
                currentPhase = IntersectionPhase.PhaseA_VehiclesGreen;
                phaseTimer = t;
            }
            else if ((t -= phaseAGreenDuration) < phaseAYellowDuration)
            {
                currentPhase = IntersectionPhase.PhaseA_VehiclesYellow;
                phaseTimer = t;
            }
            else if ((t -= phaseAYellowDuration) < allRedDuration)
            {
                currentPhase = IntersectionPhase.Phase_AllRedTransition;
                phaseTimer = t;
            }
            else if ((t -= allRedDuration) < phaseBGreenDuration)
            {
                currentPhase = IntersectionPhase.PhaseB_VehiclesGreen;
                phaseTimer = t;
            }
            else if ((t -= phaseBGreenDuration) < phaseBYellowDuration)
            {
                currentPhase = IntersectionPhase.PhaseB_VehiclesYellow;
                phaseTimer = t;
            }
            else
            {
                currentPhase = IntersectionPhase.Phase_PedestrianClearance;
                phaseTimer = t - phaseBYellowDuration;
            }
        }

        UpdateControlledEntities();
    }

    private float GetCurrentPhaseDuration()
    {
        switch (currentPhase)
        {
            case IntersectionPhase.PhaseA_VehiclesGreen: return phaseAGreenDuration;
            case IntersectionPhase.PhaseA_VehiclesYellow: return phaseAYellowDuration;
            case IntersectionPhase.Phase_AllRedTransition: return allRedDuration;
            case IntersectionPhase.PhaseB_VehiclesGreen: return phaseBGreenDuration;
            case IntersectionPhase.PhaseB_VehiclesYellow: return phaseBYellowDuration;
            case IntersectionPhase.Phase_PedestrianClearance: return clearanceDuration;
            default: return 5f;
        }
    }

    private void AdvanceToNextPhase()
    {
        switch (currentPhase)
        {
            case IntersectionPhase.PhaseA_VehiclesGreen:
                currentPhase = IntersectionPhase.PhaseA_VehiclesYellow;
                break;
            case IntersectionPhase.PhaseA_VehiclesYellow:
                currentPhase = IntersectionPhase.Phase_AllRedTransition;
                break;
            case IntersectionPhase.Phase_AllRedTransition:
                currentPhase = IntersectionPhase.PhaseB_VehiclesGreen;
                break;
            case IntersectionPhase.PhaseB_VehiclesGreen:
                currentPhase = IntersectionPhase.PhaseB_VehiclesYellow;
                break;
            case IntersectionPhase.PhaseB_VehiclesYellow:
                currentPhase = IntersectionPhase.Phase_PedestrianClearance;
                break;
            case IntersectionPhase.Phase_PedestrianClearance:
                currentPhase = IntersectionPhase.PhaseA_VehiclesGreen;
                break;
        }
    }

    private void UpdateControlledEntities()
    {
        for (int i = 0; i < trafficLights.Count; i++)
        {
            var tl = trafficLights[i];
            if (tl == null) continue;

            if (tl.signalGroupIndex == 0)
            {
                if (currentPhase == IntersectionPhase.PhaseA_VehiclesGreen) tl.currentState = TrafficLight.TrafficLightState.Green;
                else if (currentPhase == IntersectionPhase.PhaseA_VehiclesYellow) tl.currentState = TrafficLight.TrafficLightState.Yellow;
                else tl.currentState = TrafficLight.TrafficLightState.Red;
            }
            else
            {
                if (currentPhase == IntersectionPhase.PhaseB_VehiclesGreen) tl.currentState = TrafficLight.TrafficLightState.Green;
                else if (currentPhase == IntersectionPhase.PhaseB_VehiclesYellow) tl.currentState = TrafficLight.TrafficLightState.Yellow;
                else tl.currentState = TrafficLight.TrafficLightState.Red;
            }
        }
    }

    public bool IsPedestrianCrossingPermitted(PedestrianCrossing crossing)
    {
        if (currentPhase == IntersectionPhase.Phase_AllRedTransition ||
            currentPhase == IntersectionPhase.Phase_PedestrianClearance)
        {
            return true;
        }

        if (crossing == null)
        {
            return false;
        }

        // Pedestre atravessando paralelo ao fluxo verde atual tem passagem protegida do fluxo perpendicular vermelho
        Vector3 crossDir = crossing.crossingDirection;
        bool crossesEastWest = Mathf.Abs(crossDir.x) >= Mathf.Abs(crossDir.z);
        if (crossesEastWest)
            return currentPhase == IntersectionPhase.PhaseB_VehiclesGreen;

        return currentPhase == IntersectionPhase.PhaseA_VehiclesGreen;
    }

    public void RegisterTrafficLight(TrafficLight tl)
    {
        if (tl != null && !trafficLights.Contains(tl))
        {
            trafficLights.Add(tl);
        }
    }

    public void RegisterCrossing(PedestrianCrossing crossing)
    {
        if (crossing != null && !crossings.Contains(crossing))
        {
            crossings.Add(crossing);
            crossing.controller = this;
        }
    }
}
