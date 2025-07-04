using UnityEngine;
using UnityEditor;
using System;

public class BicycleVehicle : MonoBehaviour
{
    // Event that AgentBike can subscribe to
    public event Action OnTipOver;
    
    //debugInfo	
    float horizontalInput;
    float verticalInput;
    bool braking;
    Rigidbody rb;

    [Header("Power/Braking")]
    [Space(5)]
    [SerializeField] float motorForce;
    [SerializeField] float brakeForce;
    public Vector3 COG;

    [Space(20)]
    [Header("Steering")]
    [Space(5)]
    [Tooltip("Defines the maximum steering angle for the bicycle")]
    [SerializeField] float maxSteeringAngle;
    [Tooltip("Sets how current_MaxSteering is reduced based on the speed of the RB, (0 - No effect) (1 - Full)")]
    [Range(0f, 1f)] [SerializeField] float steerReductorAmmount;
    [Tooltip("Sets the Steering sensitivity [Steering Stiffness] 0 - No turn, 1 - FastTurn)")]
    [Range(0.001f, 1f)] [SerializeField] float turnSmoothing;

    [Space(20)]
    [Header("Physics-Based Balance")]
    [Space(5)]
    [Tooltip("Gyroscopic effect strength - higher values make bike more stable at speed")]
    [SerializeField] float gyroscopicStrength = 1.0f;
    [Tooltip("Self-correcting steering strength - how much bike steers into lean")]
    [SerializeField] float selfSteerStrength = 0.5f;
    [Tooltip("Gravitational restoring force when leaning")]
    [SerializeField] float gravityRestoreForce = 10f;
    [Tooltip("Air resistance affecting stability")]
    [SerializeField] float airResistance = 0.1f;
    [Tooltip("Trail effect - how steering angle affects stability")]
    [SerializeField] float trailEffect = 0.3f;

    [Space(20)]
    [Header("Dynamic Tipover Physics")]
    [Space(5)]
    [Tooltip("Base tipover angle at zero speed")]
    [SerializeField] float baseTipoverAngle = 15f;
    [Tooltip("Maximum tipover angle at high speed")]
    [SerializeField] float maxTipoverAngle = 75f;
    [Tooltip("Speed at which maximum tipover angle is reached")]
    [SerializeField] float speedForMaxAngle = 20f;
    [Tooltip("Minimum speed for any stability (below this = immediate fall)")]
    [SerializeField] float criticalSpeed = 0.5f;

    [Space(10)]
    [Header("Tipped Over Physics")]
    [Tooltip("Drag coefficient when bike is on the ground")]
    [SerializeField] float groundDrag = 2f;
    [Tooltip("Angular drag when bike is on the ground")]
    [SerializeField] float groundAngularDrag = 5f;

    float targetLeanAngle;
    float currentLeanAngle;
    public bool hasTippedOver = false;
    private float originalDrag;
    private float originalAngularDrag;

    [Space(20)]
    [Header("Object References")]	
    public Transform handle;
    [Space(10)]
    [SerializeField] WheelCollider frontWheel;
    [SerializeField] WheelCollider backWheel;
    [Space(10)]
    [SerializeField] Transform frontWheelTransform;
    [SerializeField] Transform backWheelTransform;
    [Space(10)]
    [SerializeField] TrailRenderer frontTrail;
    [SerializeField] TrailRenderer rearTrail;
    ContactProvider frontContact;
    ContactProvider rearContact;	

    [Space(20)]
    [HeaderAttribute("Info")]
    [SerializeField] float currentSteeringAngle;
    [SerializeField] float current_maxSteeringAngle;
    [SerializeField] public float dynamicTipoverAngle;
    [Space(20)]
    [HeaderAttribute("Speed")]
    [SerializeField] public float currentSpeed;
    
    public float CurrentLeanAngle => currentLeanAngle;

    void Start()
    {
        frontContact = frontTrail.transform.GetChild(0).GetComponent<ContactProvider>();
        rearContact = rearTrail.transform.GetChild(0).GetComponent<ContactProvider>();		
        frontWheel.ConfigureVehicleSubsteps(5, 12, 15);
        backWheel.ConfigureVehicleSubsteps(5, 12, 15);
        rb = GetComponent<Rigidbody>();
        
        originalDrag = rb.linearDamping;
        originalAngularDrag = rb.angularDamping;
        
        rb.centerOfMass = new Vector3(0, 0.8f, 0);
    }
    
    private void Update()
    {
        GetInput();		
    }
    
    void FixedUpdate()
    {
        if (!hasTippedOver)
        {
            ApplyPhysicsBasedBalance();
            HandleEngine();
            HandleSteering();
            UpdateHandles();
        }
        
        UpdateWheels();		
        EmitTrail();
        Speed_O_Meter();
    }

    private void GetInput()
    {
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");
        braking = Input.GetKey(KeyCode.Space);
    }

    private void ApplyPhysicsBasedBalance()
    {
        if (hasTippedOver) return;
        
        float speed = rb.linearVelocity.magnitude;
        float absLeanAngle = Mathf.Abs(currentLeanAngle);
        
        float speedFactor = Mathf.Min(speed / speedForMaxAngle, 1f);
        dynamicTipoverAngle = Mathf.Lerp(baseTipoverAngle, maxTipoverAngle, speedFactor);
        
        if (speed < criticalSpeed)
        {
            float destabilizeForce = (criticalSpeed - speed) * 20f;
            rb.AddTorque(transform.right * destabilizeForce * Mathf.Sign(currentLeanAngle), ForceMode.Force);
            
            if (absLeanAngle > baseTipoverAngle * 0.5f)
            {
                TipOver();
                return;
            }
        }
        
        if (absLeanAngle > dynamicTipoverAngle)
        {
            TipOver();
            return;
        }
        
        // Physics forces (same as before)
        float wheelAngularVel = Mathf.Abs(frontWheel.rpm + backWheel.rpm) * 0.1047f;
        Vector3 gyroscopicTorque = Vector3.up * currentLeanAngle * wheelAngularVel * gyroscopicStrength;
        rb.AddTorque(-gyroscopicTorque, ForceMode.Force);
        
        float autoSteerAngle = currentLeanAngle * selfSteerStrength * (speed / 10f);
        frontWheel.steerAngle += autoSteerAngle;
        
        float gravityTorque = Mathf.Sin(currentLeanAngle * Mathf.Deg2Rad) * gravityRestoreForce;
        rb.AddTorque(transform.right * -gravityTorque, ForceMode.Force);
        
        float centrifugalForce = (speed * speed * Mathf.Tan(currentSteeringAngle * Mathf.Deg2Rad)) / 2f;
        rb.AddForce(transform.right * centrifugalForce, ForceMode.Force);
        
        float trailStabilization = currentSteeringAngle * trailEffect * speed;
        rb.AddTorque(transform.forward * trailStabilization, ForceMode.Force);
        
        Vector3 airResistanceForce = -rb.linearVelocity.normalized * rb.linearVelocity.sqrMagnitude * airResistance;
        rb.AddForce(airResistanceForce, ForceMode.Force);
        
        float randomPerturbation = UnityEngine.Random.Range(-0.5f, 0.5f) * (1f / (speed + 1f));
        rb.AddTorque(transform.right * randomPerturbation, ForceMode.Force);
        
        Vector3 eulerAngles = transform.eulerAngles;
        currentLeanAngle = eulerAngles.z;
        if (currentLeanAngle > 180f) currentLeanAngle -= 360f;
    }

    // MAIN TIPOVER TRIGGER FUNCTION
    private void TipOver()
    {
		if (!hasTippedOver)
		{
			// hasTippedOver = true;

			// Physics setup for falling
			// rb.centerOfMass = new Vector3(0, -0.2f, 0);
			// rb.linearDamping = groundDrag;
			// rb.angularDamping = groundAngularDrag;

			// float fallTorque = Mathf.Sign(currentLeanAngle) * 50f;
			// rb.AddTorque(transform.right * fallTorque, ForceMode.Impulse);

			// // Stop wheel forces
			// frontWheel.motorTorque = 0f;
			// backWheel.motorTorque = 0f;
			// frontWheel.brakeTorque = 0f;
			// backWheel.brakeTorque = 0f;

			// TRIGGER THE EVENT - This will notify AgentBike
			OnTipOver?.Invoke();

			

			Debug.Log($"Bicycle tipped over! Speed: {rb.linearVelocity.magnitude:F2}, Lean: {currentLeanAngle:F2}°, Threshold: {dynamicTipoverAngle:F2}°");
		}
    }
    
    private void HandleTippedOverState()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetBicycle();
        }
        
        Vector3 eulerAngles = transform.eulerAngles;
        currentLeanAngle = eulerAngles.z;
        if (currentLeanAngle > 180f) currentLeanAngle -= 360f;
        
        frontWheel.motorTorque = 0f;
        backWheel.motorTorque = 0f;
        frontWheel.brakeTorque = 0f;
        backWheel.brakeTorque = 0f;
    }
    
    public void ResetBicycle()
    {
        hasTippedOver = false;
        transform.rotation = Quaternion.identity;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        currentLeanAngle = 0f;
        rb.centerOfMass = new Vector3(0, 0.8f, 0);
        rb.linearDamping = originalDrag;
        rb.angularDamping = originalAngularDrag;
        currentSteeringAngle = 0f;
        frontWheel.steerAngle = 0f;
    }

    public bool HasTippedOver => hasTippedOver;

    private void HandleEngine()
    {
        if (hasTippedOver) return;
        backWheel.motorTorque = braking ? 0f : verticalInput * motorForce;	
        float force = braking ? brakeForce : 0f;
        ApplyBraking(force);
    }
    
    public void ApplyBraking(float brakeForce)
    {
        if (hasTippedOver) return;
        frontWheel.brakeTorque = brakeForce;
        backWheel.brakeTorque = brakeForce;	
    }

    void MaxSteeringReductor() 
    {
        float t = (rb.linearVelocity.magnitude / 30) * steerReductorAmmount;		
        t = t > 1? 1 : t; 
        current_maxSteeringAngle = Mathf.LerpAngle(maxSteeringAngle, 5, t);
    }
    
    public void HandleSteering()
    {		
        if (hasTippedOver) return;
        
        MaxSteeringReductor();
        currentSteeringAngle = Mathf.Lerp(currentSteeringAngle, current_maxSteeringAngle * horizontalInput, turnSmoothing * 0.1f);
        frontWheel.steerAngle = currentSteeringAngle;
    }
    
    public void UpdateHandles()
    {		
        handle.localEulerAngles = new Vector3(handle.localEulerAngles.x, currentSteeringAngle, handle.localEulerAngles.z);
    }
    
    public void UpdateWheels()
    {
        UpdateSingleWheel(frontWheel, frontWheelTransform);
        UpdateSingleWheel(backWheel, backWheelTransform);
    }
    
    private void EmitTrail() 
    {
        if (braking && !hasTippedOver)
        {
            frontTrail.emitting = frontContact.GetCOntact();
            rearTrail.emitting = rearContact.GetCOntact();
        }
        else
        {			
            frontTrail.emitting = false;
            rearTrail.emitting = false;
        }		
    }

    private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform)
    {
        Vector3 position;
        Quaternion rotation;
        wheelCollider.GetWorldPose(out position, out rotation);
        wheelTransform.rotation = rotation;
        wheelTransform.position = position;
    }	

    void Speed_O_Meter() 
    {
        currentSpeed = rb.linearVelocity.magnitude;
    }
    
    // === ML/RL INTERFACE METHODS ===
	
	/// <summary>
	/// Get current state for ML agent
	/// </summary>
	public float[] GetState()
	{
		return new float[] {
			currentLeanAngle,           // Current lean angle
			rb.linearVelocity.magnitude, // Current speed
			rb.angularVelocity.z,       // Angular velocity (lean rate)
			currentSteeringAngle,       // Current steering
			dynamicTipoverAngle,        // Current tipover threshold
			hasTippedOver ? 1f : 0f     // Tip state
		};
	}
	
	/// <summary>
	/// Apply actions from ML agent
	/// </summary>
	public void ApplyActions(float turnAction, float motorAction)
    {
        // Handle steering (turning left, right, or no turn)
        switch (Mathf.RoundToInt(turnAction))
        {
            case 0: horizontalInput = 0f; break; // No turn
                
            case 1: horizontalInput = -1f; break; // Turn left
                
            case 2: horizontalInput = 1f; break; // Turn right
                
        }

        // Handle motor (no force, forward, backward)
        switch (Mathf.RoundToInt(motorAction))
        {
            case 0: verticalInput = 0f; break; // No force
                
            case 1: verticalInput = 1f; break;// Move forward
                
            case 2: verticalInput = -1f; break;// Move backward
                
        }
    }

    /// <summary>
    /// Reset speed back to Zero
    /// </summary>
    public void ResetForNewEpisode()
    {
        Debug.Log("Resetting bike for new episode");

        // CRITICAL: Reset rigidbody state completely
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Force rigidbody to recognize the changes
        rb.WakeUp();
        rb.Sleep(); // Put it to sleep
        rb.WakeUp(); // Wake it up again - this forces a clean state

        // Reset wheel states
        frontWheel.motorTorque = 0f;
        backWheel.motorTorque = 0f;
        frontWheel.brakeTorque = 0f;
        backWheel.brakeTorque = 0f;
        frontWheel.steerAngle = 0f;

        currentLeanAngle = 0f; 

        // Explicitly set current speed to ensure it's zero
        currentSpeed = 0f;
    }



    /// <summary>
    /// Get reward for ML training
    /// </summary>
    public float GetReward()
    {
        if (hasTippedOver) return -10f; // Big penalty for falling

        float balanceReward = 1f - (Mathf.Abs(currentLeanAngle) / dynamicTipoverAngle); // Reward for staying upright
        float speedReward = Mathf.Min(rb.linearVelocity.magnitude / 10f, 1f); // Reward for maintaining speed

        return (balanceReward + speedReward * 0.5f);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(BicycleVehicle))]
public class BicycleInspector : Editor
{
	BicycleVehicle bicycle;

	private void OnEnable()
	{
		bicycle = target as BicycleVehicle;
	}

	public override void OnInspectorGUI()
	{
		SetLabel("Physics-Based Bicycle for ML/RL", 20, FontStyle.Bold, TextAnchor.UpperLeft);
		SetLabel("Real-world bicycle dynamics simulation", 12, FontStyle.Italic, TextAnchor.UpperLeft);
		base.OnInspectorGUI();
		
		if (Application.isPlaying)
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Current Dynamic Tipover Angle: " + bicycle.dynamicTipoverAngle.ToString("F1") + "°");
			EditorGUILayout.LabelField("Current Lean: " + bicycle.CurrentLeanAngle.ToString("F1") + "°");
			EditorGUILayout.LabelField("Speed: " + bicycle.currentSpeed.ToString("F1") + " m/s");
			EditorGUILayout.LabelField("Tipped Over: " + bicycle.HasTippedOver.ToString());
		}
	}
	
	void SetLabel(string title, int size, FontStyle style, TextAnchor alignment) 
	{
		GUI.skin.label.alignment = alignment;
		GUI.skin.label.fontSize = size;
		GUI.skin.label.fontStyle = style;
		GUILayout.Label(title);
	}
}
#endif