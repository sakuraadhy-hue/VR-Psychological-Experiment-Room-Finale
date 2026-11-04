using System.Globalization;
using UnityEngine;
using TMPro;

/// The three environmental conditions of the experiment.
/// Neutral is the control baseline; Calm and Stress are the manipulated conditions.
public enum ExperimentCondition { Neutral, Calm, Stress }

/// Central controller of the experiment. It runs one trial at a time as a simple state machine:
///
///   Briefing  -> the researcher picks a condition, the participant reads the instructions
///   Running   -> 60 s countdown, the participant sorts balls, score/errors are counted
///   Survey    -> time is up (or all balls sorted), grabbing is locked, the Likert survey is shown
///   Complete  -> answers were saved to results.csv, waiting for the researcher to press RESET
///
/// It also switches the environment conditions (activation / deactivation of objects at runtime),
/// updates the 3D TextMeshPro timer and score, and resets everything for the next participant.
///
/// Setup: located on the ExperimentUI prefab root (together with DataLogger).
/// Build the prefab automatically with the menu Tools > Experiment > Build Experiment UI.
public class ExperimentManager : MonoBehaviour
{
    public enum TrialState { Briefing, Running, Survey, Complete }

    [Header("Sorting task (Member B)")]
    [Tooltip("The sorting table with bins and balls. Found automatically if left empty.")]
    public SortingStation sortingStation;

    [Tooltip("Length of one trial in seconds.")]
    public float trialDuration = 60f;

    [Header("Environment conditions (Member A)")]
    [Tooltip("Member A's EnvironmentManager. When set, it switches the Neutral/Calm/Stress sets and the " +
             "daylight; the object lists below are only used as a fallback when it is missing.")]
    public EnvironmentManager environmentManager;

    [Tooltip("Keys 1 / 2 / 3 select Neutral / Calm / Stress (researcher shortcut, only before a trial starts).")]
    public bool keyboardShortcuts = true;

    [Tooltip("Objects that are active ONLY in the Neutral condition (e.g. NeutralSet).")]
    public GameObject[] neutralObjects;

    [Tooltip("Objects that are active ONLY in the Calm condition (e.g. CalmSet / Environment_Calm).")]
    public GameObject[] calmObjects;

    [Tooltip("Objects that are active ONLY in the Stress condition (e.g. StressSet / Environment_Stress).")]
    public GameObject[] stressObjects;

    [Tooltip("The scene's main Directional Light. It is switched off in the Stress condition, so the room " +
             "is lit only by the cold flickering lights (same rule as Member A's EnvironmentManager).")]
    public Light mainDirectionalLight;

    [Header("Participant panels (world-space canvas)")]
    [Tooltip("The whole participant canvas. Hidden during the trial so the 3D clock behind it is visible.")]
    public GameObject participantCanvas;

    [Tooltip("The START button and its wall mount. Only shown before the trial.")]
    public GameObject startButtonMount;

    public GameObject briefingPanel;
    public SurveyPanel surveyPanel;
    public GameObject completePanel;
    public TMP_Text completeMessage;

    [Header("3D TextMeshPro displays")]
    [Tooltip("The clock board behind the canvas. Shown only while the trial runs, so it never shows " +
             "through the instructions or the survey. Found automatically by name if left empty.")]
    public GameObject timerDisplay;

    public TMP_Text timerText;
    public TMP_Text scoreText;
    public TMP_Text researcherStatusText;

    [Header("3D push buttons")]
    public RemoteButton startButton;
    public RemoteButton neutralButton;
    public RemoteButton calmButton;
    public RemoteButton stressButton;
    public RemoteButton resetButton;

    [Header("Data")]
    public DataLogger dataLogger;

    [Header("Timer colours")]
    public Color timerNormalColor = new Color(0.35f, 1f, 0.55f);
    public Color timerWarningColor = new Color(1f, 0.3f, 0.25f);

    [Tooltip("The timer turns red when this many seconds or fewer are left.")]
    public float warningThreshold = 10f;

    // Read-only state, useful for debugging and for other scripts.
    public TrialState State { get; private set; }
    public ExperimentCondition CurrentCondition { get; private set; }

    private int score;            // correctly sorted balls in the current trial
    private int errors;           // balls dropped in the wrong bin in the current trial
    private float timeRemaining;  // seconds left in the current trial
    private int participantNumber = 1;

    // Participant IDs look like P001, P002, ...
    private string ParticipantID => "P" + participantNumber.ToString("000");

    void Awake()
    {
        // Fallbacks so the manager still works if a reference was not set in the Inspector.
        if (sortingStation == null)
            sortingStation = FindFirstObjectByType<SortingStation>(FindObjectsInactive.Include);
        if (dataLogger == null)
            dataLogger = GetComponent<DataLogger>();
        if (environmentManager == null)
            environmentManager = FindFirstObjectByType<EnvironmentManager>(FindObjectsInactive.Include);
        if (timerDisplay == null)
        {
            Transform clock = transform.Find("TimerDisplay");
            if (clock != null) timerDisplay = clock.gameObject;
        }

        // The 1/2/3 keys are handled here instead of in EnvironmentManager, so a key press
        // goes through the same checks as the panel buttons (locked during a trial, logged in the CSV).
        if (environmentManager != null)
            environmentManager.keyboardShortcuts = false;

        if (sortingStation == null)
            Debug.LogWarning("[ExperimentManager] No SortingStation found in the scene.");
        else if (!sortingStation.gameObject.activeInHierarchy)
            Debug.LogWarning("[ExperimentManager] The SortingStation is inactive. Tick it active in the Hierarchy.");

        // World-space canvases need a camera to receive mouse clicks.
        // A prefab cannot store a reference to the scene camera, so we assign it here.
        Camera cam = Camera.main;
        foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true))
        {
            if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
                canvas.worldCamera = cam;
        }
    }

    // Listen to the bins only while this component is enabled (avoids leaks/duplicate calls).
    void OnEnable()  { SortingBin.OnItemSorted += HandleItemSorted; }
    void OnDisable() { SortingBin.OnItemSorted -= HandleItemSorted; }

    void Start()
    {
        // Continue the participant numbering from the trials already saved in results.csv,
        // so restarting Play mode does not reuse P001.
        if (dataLogger != null)
            participantNumber = dataLogger.CountLoggedTrials() + 1;

        ResetExperiment();
    }

    void Update()
    {
        // Researcher keyboard shortcut: 1 / 2 / 3 = Neutral / Calm / Stress (same as the panel buttons).
        if (keyboardShortcuts && State == TrialState.Briefing)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectNeutral();
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectCalm();
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectStress();
        }

        // The countdown only runs during a trial.
        if (State != TrialState.Running) return;

        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            EndTrial();
        }

        UpdateTimerDisplay();
    }

    // ------------------------------------------------------------------
    //  Sorting events (sent by SortingBin)
    // ------------------------------------------------------------------

    /// Called every time a ball lands in a bin. true = correct bin, false = wrong bin.
    private void HandleItemSorted(bool isCorrect)
    {
        // Ignore anything that happens outside the timed trial.
        if (State != TrialState.Running) return;

        if (isCorrect) score++;
        else errors++;

        UpdateScoreDisplay();

        // Early finish: every ball has been sorted before the time ran out.
        // The remaining time is kept and saved in the TimeRemaining column.
        if (sortingStation != null && sortingStation.RemainingItems == 0)
            EndTrial();
    }

    // ------------------------------------------------------------------
    //  Researcher controls (called by the 3D buttons on the wall panel)
    // ------------------------------------------------------------------

    public void SelectNeutral() { SetCondition(ExperimentCondition.Neutral); }
    public void SelectCalm()    { SetCondition(ExperimentCondition.Calm); }
    public void SelectStress()  { SetCondition(ExperimentCondition.Stress); }

    /// Changes the environment. Only allowed before the trial starts, so a participant
    /// always experiences a single condition for the whole trial (experimental control).
    public void SetCondition(ExperimentCondition condition)
    {
        if (State != TrialState.Briefing)
        {
            Debug.Log("[ExperimentManager] Condition is locked during a trial. Press RESET first.");
            return;
        }

        ApplyCondition(condition);
    }

    /// Starts the 60-second sorting trial. Called by the START button.
    public void StartTrial()
    {
        if (State != TrialState.Briefing) return;

        score = 0;
        errors = 0;
        timeRemaining = trialDuration;
        State = TrialState.Running;

        // Hide the canvas and the start button (revealing the 3D clock), unlock grabbing.
        if (briefingPanel != null) briefingPanel.SetActive(false);
        if (participantCanvas != null) participantCanvas.SetActive(false);
        if (startButtonMount != null) startButtonMount.SetActive(false);
        else if (startButton != null) startButton.gameObject.SetActive(false);
        if (timerDisplay != null) timerDisplay.SetActive(true);
        SetGrabbing(true);

        // The researcher cannot change the condition in the middle of a trial.
        SetConditionButtonsInteractable(false);

        UpdateTimerDisplay();
        UpdateScoreDisplay();
        UpdateResearcherStatus();
        Debug.Log($"[ExperimentManager] Trial started: {ParticipantID}, condition {CurrentCondition}");
    }

    /// Puts the whole room back to its initial state for the next participant.
    /// Called by the RESET button (and once at Start). It can also abort a running trial;
    /// an aborted trial is not saved.
    public void ResetExperiment()
    {
        State = TrialState.Briefing;
        score = 0;
        errors = 0;
        timeRemaining = trialDuration;

        // Lock grabbing first so a held ball is dropped, then put every ball back.
        SetGrabbing(false);
        if (sortingStation != null && sortingStation.gameObject.activeInHierarchy)
            sortingStation.ResetStation();

        // Participant UI back to the instructions screen.
        if (participantCanvas != null) participantCanvas.SetActive(true);
        if (startButtonMount != null) startButtonMount.SetActive(true);
        if (timerDisplay != null) timerDisplay.SetActive(false);
        if (briefingPanel != null) briefingPanel.SetActive(true);
        if (surveyPanel != null) surveyPanel.Hide();
        if (completePanel != null) completePanel.SetActive(false);
        if (startButton != null)
        {
            startButton.gameObject.SetActive(true);
            startButton.SetInteractable(true);
        }

        // Back to the control baseline.
        SetConditionButtonsInteractable(true);
        ApplyCondition(ExperimentCondition.Neutral);

        UpdateTimerDisplay();
        UpdateScoreDisplay();
        UpdateResearcherStatus();
    }

    // ------------------------------------------------------------------
    //  End of trial and survey
    // ------------------------------------------------------------------

    /// Stops the trial (time up or all balls sorted), locks interaction and shows the survey.
    private void EndTrial()
    {
        State = TrialState.Survey;
        SetGrabbing(false);

        // Hide the clock and bring the canvas back to show the questionnaire.
        if (timerDisplay != null) timerDisplay.SetActive(false);
        if (participantCanvas != null) participantCanvas.SetActive(true);
        if (surveyPanel != null) surveyPanel.Show();

        UpdateTimerDisplay();
        UpdateResearcherStatus();
        Debug.Log($"[ExperimentManager] Trial ended: score {score}, errors {errors}, {timeRemaining:0.0}s left");
    }

    /// Called by the SurveyPanel when the participant presses SUBMIT.
    /// Writes one row to results.csv and shows the completion message.
    public void SubmitSurvey(int stressRating, int focusRating, int comfortRating)
    {
        if (State != TrialState.Survey) return;

        TrialRecord record = new TrialRecord
        {
            participantId = ParticipantID,
            condition = CurrentCondition,
            score = score,
            errors = errors,
            timeRemaining = timeRemaining,
            stressRating = stressRating,
            focusRating = focusRating,
            comfortRating = comfortRating
        };

        bool saved = dataLogger != null && dataLogger.LogTrial(record);

        if (surveyPanel != null) surveyPanel.Hide();
        if (completePanel != null) completePanel.SetActive(true);
        if (completeMessage != null)
        {
            completeMessage.text = saved
                ? "Trial complete.\nThank you! Please inform the researcher."
                : "Trial complete, but the results could NOT be saved.\n(Is results.csv open in another program?)";
        }

        // Only move to the next participant ID if this one was actually recorded.
        if (saved) participantNumber++;

        State = TrialState.Complete;
        UpdateResearcherStatus();
    }

    // ------------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------------

    /// Activates the objects of the chosen condition and deactivates the others.
    private void ApplyCondition(ExperimentCondition condition)
    {
        CurrentCondition = condition;

        if (environmentManager != null)
        {
            // Member A's EnvironmentManager switches the sets and the daylight.
            switch (condition)
            {
                case ExperimentCondition.Neutral: environmentManager.ActivateNeutral(); break;
                case ExperimentCondition.Calm:    environmentManager.ActivateCalm();    break;
                case ExperimentCondition.Stress:  environmentManager.ActivateStress();  break;
            }
        }
        else
        {
            // Fallback without an EnvironmentManager: switch the object lists directly.
            SetObjectsActive(neutralObjects, condition == ExperimentCondition.Neutral);
            SetObjectsActive(calmObjects,    condition == ExperimentCondition.Calm);
            SetObjectsActive(stressObjects,  condition == ExperimentCondition.Stress);

            // Daylight on for Neutral and Calm, off for Stress (darker, harsher room).
            if (mainDirectionalLight != null)
                mainDirectionalLight.enabled = condition != ExperimentCondition.Stress;
        }

        // The selected condition button stays pushed in, like a physical selector switch.
        if (neutralButton != null) neutralButton.SetLatched(condition == ExperimentCondition.Neutral);
        if (calmButton != null)    calmButton.SetLatched(condition == ExperimentCondition.Calm);
        if (stressButton != null)  stressButton.SetLatched(condition == ExperimentCondition.Stress);

        UpdateResearcherStatus();
    }

    private static void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null) return;
        foreach (GameObject obj in objects)
            if (obj != null) obj.SetActive(active);
    }

    private void SetConditionButtonsInteractable(bool interactable)
    {
        if (neutralButton != null) neutralButton.SetInteractable(interactable);
        if (calmButton != null)    calmButton.SetInteractable(interactable);
        if (stressButton != null)  stressButton.SetInteractable(interactable);
    }

    private void SetGrabbing(bool enabled)
    {
        if (sortingStation != null) sortingStation.SetGrabbingEnabled(enabled);
    }

    /// Shows the time as mm:ss.t (e.g. 01:00.0) on the 3D TextMeshPro clock.
    private void UpdateTimerDisplay()
    {
        if (timerText == null) return;

        // Round up to the next tenth so the clock shows 00:00.0 only when time is really over.
        int tenths = Mathf.CeilToInt(timeRemaining * 10f);
        int minutes = tenths / 600;
        float seconds = (tenths % 600) / 10f;

        // InvariantCulture: always use a dot, even on a French system (01:00.0, not 01:00,0).
        timerText.text = string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00.0}", minutes, seconds);

        bool warning = State == TrialState.Running && timeRemaining <= warningThreshold;
        timerText.color = warning ? timerWarningColor : timerNormalColor;
    }

    private void UpdateScoreDisplay()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score}  |  Errors: {errors}";
    }

    /// The small status screen on the researcher panel.
    private void UpdateResearcherStatus()
    {
        if (researcherStatusText == null) return;

        string stateLabel;
        switch (State)
        {
            case TrialState.Briefing: stateLabel = "Ready - choose a condition"; break;
            case TrialState.Running:  stateLabel = "Trial running"; break;
            case TrialState.Survey:   stateLabel = "Participant answering survey"; break;
            default:                  stateLabel = "Saved - press RESET"; break;
        }

        // After a successful save the number has already moved on to the next participant.
        string participantLabel = State == TrialState.Complete
            ? $"Next participant: {ParticipantID}"
            : $"Participant: {ParticipantID}";

        researcherStatusText.text =
            $"Condition: {CurrentCondition}\n{stateLabel}\n{participantLabel}";
    }
}
