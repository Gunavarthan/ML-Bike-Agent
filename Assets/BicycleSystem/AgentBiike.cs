using UnityEngine;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class Agentbike : Agent
{
    
    [Header("Bicycle References")]
    [SerializeField] private BicycleVehicle bicycleController;
    [SerializeField] private Transform target;
    [SerializeField] private GameObject groundPlane;

    [Header("Checkpoints")]
    [SerializeField] private int numberOfCheckpoints = 5;
    [SerializeField] private float checkpointReward = 2f;
    private List<Vector3> checkpointPositions = new List<Vector3>();
    private int nextCheckpointIndex = 0;
    
    [Header("Training Parameters")]
    [SerializeField] private float maxEpisodeTime = 120f;
    [SerializeField] private float targetReachDistance = 2f;
    [SerializeField] private float fallPenalty = -20f;
    [SerializeField] private float targetReward = 100f;
    [SerializeField] private float distanceReward = 1f;
    
    private float episodeTimer;
    private Vector3 initialPosition;
    private Vector3 initialRotation;
    private Vector3 previousLocalPosition;
    private float previousDistanceToTarget;

    
    private bool checkpointNotReached = true;
    private bool tipOverHandlerSubscribed = false;

    private void GenerateCheckpoints()
    {
        checkpointPositions.Clear();
        Vector3 start = transform.position;
        Vector3 end = target.position;

        for (int i = 1; i <= numberOfCheckpoints; i++)
        {
            float t = (float)i / (numberOfCheckpoints + 1);
            Vector3 checkpoint = Vector3.Lerp(start, end, t);
            checkpointPositions.Add(checkpoint);
        }
    }


    public override void Initialize()
    {
        if (bicycleController != null && !tipOverHandlerSubscribed)
        {
            bicycleController.OnTipOver += HandleTipOver;
            tipOverHandlerSubscribed = true;
        }
    }


    private void OnDestroy()
    {
        if (bicycleController != null && tipOverHandlerSubscribed)
        {
            bicycleController.OnTipOver -= HandleTipOver;
            tipOverHandlerSubscribed = false;
        }
    }


    private void Start()
    {
        // Get bicycle controller if not assigned
        if (bicycleController == null)
            bicycleController = GetComponent<BicycleVehicle>();

        // Store initial transform values
        initialPosition = transform.localPosition;
        initialRotation = transform.localEulerAngles;

    }

    public override void OnEpisodeBegin()
    {
        // Reset episode timer
        episodeTimer = 0f;
        checkpointNotReached = true;

        //Debug.Log("start Episode");
        // Reset bicycle to initial state
        bicycleController.ResetForNewEpisode();
        Debug.Log($"Speed after reset: {bicycleController.currentSpeed}");
        base.OnEpisodeBegin();

        // Set bicycle to specific starting position
        transform.localPosition = new Vector3(6.5f, 1f, 0f);
        // transform.localPosition = new Vector3(6.5f, 8.5f, 0f);
        transform.rotation = Quaternion.identity;

        // Set target to specific position with random X
        //float targetX = Random.Range(3f, 10f);
        //target.localPosition = new Vector3(targetX, 1f, 40f);

        // Initialize tracking variables
        previousLocalPosition = transform.localPosition;
        previousDistanceToTarget = Vector3.Distance(transform.position, target.position);
        
        GenerateCheckpoints();
        nextCheckpointIndex = 0;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Get bicycle state (6 values from your BicycleVehicle.GetState())
        float[] bicycleState = bicycleController.GetState();
        foreach (float state in bicycleState)
        {
            sensor.AddObservation(state);
        }
        
        // Position and rotation observations (LOCAL)
        sensor.AddObservation(transform.localPosition); // 3 values
        sensor.AddObservation(transform.localRotation); // 4 values
        
        // Target relative position (LOCAL to agent)
        Vector3 targetLocalDirection = transform.InverseTransformPoint(target.position).normalized;
        sensor.AddObservation(targetLocalDirection); // 3 values
        
        // Distance to target (normalized)
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        sensor.AddObservation(distanceToTarget / 20f); // 1 value (assuming max distance ~20)
        
        // Velocity relative to target direction (LOCAL)
        Vector3 worldVelocity = bicycleController.GetComponent<Rigidbody>().linearVelocity;
        Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity).normalized;
        Vector3 localTargetDirection = transform.InverseTransformPoint(target.position).normalized;
        float velocityAlignment = Vector3.Dot(localVelocity, localTargetDirection);
        sensor.AddObservation(velocityAlignment); // 1 value
        
        // Total observations: 6 + 3 + 4 + 3 + 1 + 1 = 18
    }


    private void FixedUpdate()
    {
        if (nextCheckpointIndex < checkpointPositions.Count)
        {
            Vector3 currentCheckpoint = checkpointPositions[nextCheckpointIndex];
            float distance = Vector3.Distance(transform.position, currentCheckpoint);
            if (distance < 2f)
            {
                Debug.Log($"Checkpoint {nextCheckpointIndex} reached");
                AddReward(checkpointReward);
                nextCheckpointIndex++;
            }
        }
    }


    public override void OnActionReceived(ActionBuffers actions)
    {
        // Get continuous actions (steering and motor)
        float steerAction = actions.ContinuousActions[0]; // -1 to 1
        float motorAction = actions.ContinuousActions[1]; // -1 to 1

        // Apply actions to bicycle controller
        bicycleController.ApplyActions(steerAction, motorAction);

        // Calculate rewards
        CalculateRewards();

        // Update episode timer
        episodeTimer += Time.fixedDeltaTime;
    }

    public override void Heuristic(in ActionBuffers actionOut)
    {
        var continuousActions = actionOut.ContinuousActions;
        
        // Manual control for testing
        float steerInput = 0f;
        float motorInput = 0f;
        
        // Steering with A/D or Arrow Keys
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) steerInput = -1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) steerInput = 1f;
        
        // Motor with W/S or Arrow Keys
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) motorInput = 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) motorInput = -1f;
        
        continuousActions[0] = steerInput;
        continuousActions[1] = motorInput;
    }

    private void CalculateRewards()
    {
        // Remove Time.fixedDeltaTime from most rewards
        float bicycleReward = bicycleController.GetReward();
        AddReward(bicycleReward * Time.fixedDeltaTime); // Only for continuous rewards
        // Debug.Log($"Balancefactor :{bicycleReward * Time.fixedDeltaTime}");

        // Distance-based reward 
        float currentDistanceToTarget = Vector3.Distance(transform.position, target.position);
        float distanceImprovement = previousDistanceToTarget - currentDistanceToTarget;
        if (distanceImprovement > 0) // Only reward getting closer
        {
            AddReward(distanceImprovement * 2f);
        }
        previousDistanceToTarget = currentDistanceToTarget;

        //Penality for slowing down 
        if (bicycleController.currentSpeed < 0.1f) 
        {
            AddReward(-0.01f);
        }

        // Penalty for timeout
        // if (episodeTimer > maxEpisodeTime)
        // {
        //     Debug.Log("Time Exceeded");
        //     changeColor( new Color(0.5f, 0.1f, 0.1f));
        //     AddReward(-5f); 
        //     EndEpisode();
        // }

        AddReward(-0.01f);      // Small time penalty to encourage efficiency

        Vector3 directionToTarget = (target.position - transform.position).normalized;
        Vector3 forwardDirection = transform.forward;
        float alignment = Vector3.Dot(forwardDirection, directionToTarget);
        if (alignment > 0)      // Only reward when facing target
        {
            AddReward(alignment * 0.1f);
        }

    }

    private void HandleTipOver()
    {
        Debug.Log("Agent received tipover event!");
        bicycleController.hasTippedOver = false;
        // Add big penalty
        AddReward(fallPenalty);
        changeColor( new Color(0.5f, 0.1f, 0.1f));
        // End episode immediately
        EndEpisode();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Target"))
        {
            Debug.Log($"Target acheved: ${targetReward}");
            AddReward(targetReward);
            if (groundPlane != null)
                changeColor(new Color(0.1f, 0.5f, 0.1f));
            EndEpisode();
        }
        else if (other.CompareTag("Wall"))
        {
            Debug.Log($"Hit Wall: ${-30}");
            AddReward(-5f);
            if (groundPlane != null)
                changeColor(new Color(0.5f, 0.1f, 0.1f));
            EndEpisode();
        }
        else if (other.CompareTag("CheckPoint") && checkpointNotReached)
        {
            Debug.Log($"CheckPoint: ${50}");
            AddReward(50f);
            checkpointNotReached = false;
            if (groundPlane != null)
                changeColor(new Color(0.1f, 0.1f, 0.5f));
        }
    }

    // Optional: Visual debugging in scene view
    private void OnDrawGizmos()
    {
        if (target != null)
        {
            // Draw line to target
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, target.position);

            // Draw target reach radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(target.position, targetReachDistance);
        }
        
        Gizmos.color = Color.cyan;
        for (int i = 0; i < checkpointPositions.Count; i++)
        {
            Gizmos.DrawWireSphere(checkpointPositions[i], 1.0f);
        }

        if (target != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, target.position);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(target.position, targetReachDistance);
        }
    }

    private void changeColor(Color color)
    {
        Renderer[] renderers = groundPlane.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            foreach (Renderer rend in renderers)
            {
                rend.material.color = color;
            }
        }
        else
        {
            Renderer mainRenderer = groundPlane.GetComponent<Renderer>();
            mainRenderer.material.color = color;
        }
    }
}