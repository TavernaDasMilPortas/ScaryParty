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
        phaseTimer += Time.deltaTime;
        float currentTargetDuration = GetCurrentPhaseDuration();

        if (phaseTimer >= currentTargetDuration)
        {
            phaseTimer = 0f;
            AdvanceToNextPhase();
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
        bool isVehiclesGreen = currentPhase == IntersectionPhase.PhaseA_VehiclesGreen;
        bool isVehiclesYellow = currentPhase == IntersectionPhase.PhaseA_VehiclesYellow;

        for (int i = 0; i < trafficLights.Count; i++)
        {
            var tl = trafficLights[i];
            if (tl == null) continue;

            if (isVehiclesGreen) tl.currentState = TrafficLight.TrafficLightState.Green;
            else if (isVehiclesYellow) tl.currentState = TrafficLight.TrafficLightState.Yellow;
            else tl.currentState = TrafficLight.TrafficLightState.Red;
        }
    }

    public bool IsPedestrianCrossingPermitted(PedestrianCrossing crossing)
    {
        // Pedestrians cross during AllRed or PedestrianClearance phases, or PhaseB
        return currentPhase == IntersectionPhase.Phase_AllRedTransition ||
               currentPhase == IntersectionPhase.Phase_PedestrianClearance ||
               currentPhase == IntersectionPhase.PhaseB_VehiclesGreen;
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
