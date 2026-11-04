using UnityEngine;

/// Switches the room between the three experimental conditions by activating one
/// environment set (Neutral, Calm or Stress) and deactivating the other two.
/// In the Stress condition the main directional light is also turned off, so the room
/// is lit only by the cold flickering lights inside StressSet.
///
/// The researcher panel buttons call ActivateNeutral / ActivateCalm / ActivateStress
/// through the ExperimentManager. Keys 1/2/3 are a researcher shortcut.
public class EnvironmentManager : MonoBehaviour
{
    [Header("Environment Prefabs")]
    [SerializeField] private GameObject neutralSet;
    [SerializeField] private GameObject calmSet;
    [SerializeField] private GameObject stressSet;

    [Header("Global Scene Lighting")]
    [SerializeField] private Light mainDirectionalLight;

    [Header("Testing")]
    [Tooltip("Keys 1/2/3 switch the condition directly. The ExperimentManager turns this off and handles " +
             "the keys itself, so the condition can't change mid-trial and is always logged correctly.")]
    public bool keyboardShortcuts = true;

    void Start()
    {
        // Start the experiment in the Neutral control condition
        ActivateNeutral();
    }

    void Update()
    {
        // Keyboard controls for testing without the UI (disabled when the ExperimentManager is present)
        if (!keyboardShortcuts) return;
        if (Input.GetKeyDown(KeyCode.Alpha1)) ActivateNeutral();
        if (Input.GetKeyDown(KeyCode.Alpha2)) ActivateCalm();
        if (Input.GetKeyDown(KeyCode.Alpha3)) ActivateStress();
    }

    public void ActivateNeutral()
    {
        neutralSet.SetActive(true);
        calmSet.SetActive(false);
        stressSet.SetActive(false);
        if (mainDirectionalLight != null) mainDirectionalLight.enabled = true;
    }

    public void ActivateCalm()
    {
        neutralSet.SetActive(false);
        calmSet.SetActive(true);
        stressSet.SetActive(false);
        if (mainDirectionalLight != null) mainDirectionalLight.enabled = true;
    }

    public void ActivateStress()
    {
        neutralSet.SetActive(false);
        calmSet.SetActive(false);
        stressSet.SetActive(true);
        if (mainDirectionalLight != null) mainDirectionalLight.enabled = false;
    }
}