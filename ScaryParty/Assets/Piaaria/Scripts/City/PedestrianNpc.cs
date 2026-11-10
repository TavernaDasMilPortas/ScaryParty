using System;
using UnityEngine;
using UnityEngine.AI;
using ScaryParty.Navigation.Agents;
using ScaryParty.Navigation.Urban;
using ScaryParty.Player;

public enum PedestrianState
{
    InsideBuilding,
    ExitingBuilding,
    Travelling,
    WaitingAtCrosswalk,
    CrossingStreet,
    EnteringBuilding,
    VisitingPizzeria
}

/// <summary>
/// Individual pedestrian NPC living in the procedural city.
/// Operates on a state machine: exits buildings, travels along sidewalks, waits at traffic lights,
/// crosses zebra crossings, visits other buildings, or transitions to visiting the pizzeria.
/// </summary>
[RequireComponent(typeof(NpcNavigationMotor))]
[RequireComponent(typeof(NpcLocomotionAdapter))]
public class PedestrianNpc : MonoBehaviour
{
    public string PedestrianId { get; private set; }
    public PedestrianState State { get; private set; } = PedestrianState.Travelling;

    public NpcNavigationMotor Motor { get; private set; }
    public NpcLocomotionAdapter Locomotion { get; private set; }
    public CharacterVisualController VisualController { get; private set; }
    public CharacterAppearance Appearance { get; private set; }

    [Header("Current Goal")]
    public CityBuilding TargetBuilding;
    public PedestrianCrossing CurrentCrossing;
    public Vector3 TargetPosition;

    [Header("Timers")]
    private float _stateTimer;
    private float _dwellDuration = 8f;
    private float _walkSpeed = 2.2f;

    public event Action<PedestrianNpc> OnReachedPizzeria;
    public event Action<PedestrianNpc, CityBuilding> OnEnteredBuilding;

    private static readonly Color[] PedestrianPalette = new[]
    {
        new Color(0.25f, 0.58f, 0.85f),
        new Color(0.85f, 0.32f, 0.28f),
        new Color(0.32f, 0.75f, 0.42f),
        new Color(0.88f, 0.68f, 0.22f),
        new Color(0.68f, 0.38f, 0.82f),
        new Color(0.22f, 0.75f, 0.72f),
        new Color(0.90f, 0.48f, 0.25f),
        new Color(0.75f, 0.35f, 0.58f)
    };

    private void Awake()
    {
        Motor = GetComponent<NpcNavigationMotor>();
        Locomotion = GetComponent<NpcLocomotionAdapter>();
    }

    public void Initialize(string id, GameObject visualInstance, float speed = 2.2f, int colorIndex = 0)
    {
        PedestrianId = id;
        _walkSpeed = speed;

        if (visualInstance != null)
        {
            visualInstance.transform.SetParent(transform, false);
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.identity;

            VisualController = visualInstance.GetComponent<CharacterVisualController>()
                            ?? visualInstance.AddComponent<CharacterVisualController>();
            Appearance = visualInstance.GetComponent<CharacterAppearance>()
                      ?? visualInstance.AddComponent<CharacterAppearance>();

            VisualController.ResolveReferences();
            Locomotion.BindVisual(VisualController);
            VisualController.SetPreparing(false);
            VisualController.SetSitting(false, false);
            VisualController.SetLocomotion(0f, 1f, true);

            if (Appearance != null)
            {
                Color c = PedestrianPalette[Mathf.Abs(colorIndex) % PedestrianPalette.Length];
                Appearance.ApplyPrimaryColor(c);
            }
        }

        Motor.Configure(_walkSpeed, 360f, 12f, 0.35f);
        Motor.OnDestinationReached += HandleDestinationReached;
        Motor.OnNavigationFailed += HandleNavigationFailed;
    }

    private void OnDestroy()
    {
        if (Motor != null)
        {
            Motor.OnDestinationReached -= HandleDestinationReached;
            Motor.OnNavigationFailed -= HandleNavigationFailed;
        }
    }

    public void SpawnAt(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;
        if (Motor != null && Motor.Agent != null)
        {
            Motor.Agent.Warp(position);
        }
        gameObject.SetActive(true);
    }

    public void AssignTravelToBuilding(CityBuilding target, float dwellTime = 8f)
    {
        if (target == null) return;
        TargetBuilding = target;
        _dwellDuration = dwellTime;
        State = PedestrianState.Travelling;
        TargetPosition = target.entrancePosition;
        Motor.MoveTo(TargetPosition);
    }

    public void AssignVisitPizzeria(Vector3 pizzeriaEntrance)
    {
        TargetBuilding = null;
        State = PedestrianState.VisitingPizzeria;
        TargetPosition = pizzeriaEntrance;
        Motor.MoveTo(TargetPosition);
    }

    private void Update()
    {
        if (State == PedestrianState.WaitingAtCrosswalk)
        {
            _stateTimer += Time.deltaTime;
            if (CurrentCrossing != null && CurrentCrossing.CanBeginCrossing(true))
            {
                // Signal turned green for pedestrians!
                State = PedestrianState.CrossingStreet;
                CurrentCrossing.NotifyCrossingStarted(this);
                Vector3 oppositeSide = CurrentCrossing.GetOppositeWaitingPoint(transform.position);
                Motor.MoveTo(oppositeSide);
            }
        }
    }

    private void HandleDestinationReached()
    {
        switch (State)
        {
            case PedestrianState.Travelling:
                if (TargetBuilding != null && TargetBuilding.portal != null)
                {
                    State = PedestrianState.EnteringBuilding;
                    TargetBuilding.portal.RegisterOccupant(PedestrianId);
                    OnEnteredBuilding?.Invoke(this, TargetBuilding);
                }
                break;

            case PedestrianState.CrossingStreet:
                if (CurrentCrossing != null)
                {
                    CurrentCrossing.NotifyCrossingCompleted(this);
                    CurrentCrossing = null;
                }
                // Resume travelling to target building or pizzeria
                if (TargetPosition != Vector3.zero)
                {
                    State = PedestrianState.Travelling;
                    Motor.MoveTo(TargetPosition);
                }
                break;

            case PedestrianState.VisitingPizzeria:
                OnReachedPizzeria?.Invoke(this);
                break;
        }
    }

    private void HandleNavigationFailed()
    {
        // If path failed, attempt to find a nearby reachable building or recovery
        var nearest = CityBuildingRegistry.Instance?.GetRandomBuilding(null, pedestrianEligibleOnly: true);
        if (nearest != null)
        {
            AssignTravelToBuilding(nearest, 5f);
        }
    }
}
