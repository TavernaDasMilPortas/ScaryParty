using UnityEngine;

/// <summary>
/// Traffic light component that cycles through Red, Yellow, Green states.
/// Changes the color of its light sphere using MaterialPropertyBlock.
/// </summary>
public class TrafficLight : MonoBehaviour
{
    public enum TrafficLightState { Red, Yellow, Green }

    [Header("State")]
    public TrafficLightState currentState = TrafficLightState.Green;

    [Header("Timing")]
    [Tooltip("Duration of green light in seconds")]
    public float greenDuration = 5f;
    [Tooltip("Duration of yellow light in seconds")]
    public float yellowDuration = 2f;
    [Tooltip("Duration of red light in seconds")]
    public float redDuration = 5f;

    private Renderer _lightRenderer;
    private MaterialPropertyBlock _propBlock;
    private float _timer;
    private float _timeOffset;

    /// <summary>
    /// Sets up the traffic light with its light renderer and time offset.
    /// </summary>
    public void Setup(Renderer lightRenderer, float timeOffset)
    {
        _lightRenderer = lightRenderer;
        _timeOffset = timeOffset;
        _propBlock = new MaterialPropertyBlock();
        _timer = timeOffset;
        UpdateVisual();
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        float cycleDuration = greenDuration + yellowDuration + redDuration;
        float cycleTime = _timer % cycleDuration;

        TrafficLightState newState;
        if (cycleTime < greenDuration)
            newState = TrafficLightState.Green;
        else if (cycleTime < greenDuration + yellowDuration)
            newState = TrafficLightState.Yellow;
        else
            newState = TrafficLightState.Red;

        if (newState != currentState)
        {
            currentState = newState;
            UpdateVisual();
        }
    }

    private void UpdateVisual()
    {
        if (_lightRenderer == null || _propBlock == null) return;

        Color color;
        switch (currentState)
        {
            case TrafficLightState.Green: color = Color.green; break;
            case TrafficLightState.Yellow: color = Color.yellow; break;
            case TrafficLightState.Red: color = Color.red; break;
            default: color = Color.white; break;
        }

        _propBlock.SetColor("_Color", color);
        _propBlock.SetColor("_BaseColor", color);
        _propBlock.SetColor("_EmissionColor", color * 2f);
        _lightRenderer.SetPropertyBlock(_propBlock);
    }
}
