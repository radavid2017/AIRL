using UnityEngine;
using System.Collections.Generic;

public class CarController : MonoBehaviour
{
    public Transform frontLeftWheel;
    public Transform frontRightWheel;
    public Transform rearLeftWheel;
    public Transform rearRightWheel;

    [SerializeField] WheelCollider flCollider;
    [SerializeField] WheelCollider frCollider;
    [SerializeField] WheelCollider rlCollider;
    [SerializeField] WheelCollider rrCollider;

    public float maxSteerAngle = 30f;
    public float motorTorque = 1500f;
    public float maxSpeed = 20f;

    public CarTrajectory trajectory;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
    }

    void FixedUpdate()
    {
        // Get the latest path from CarTrajectory
        List<Vector3> waypoints = trajectory.GetCurrentPath();
        if (waypoints == null || waypoints.Count == 0) return;

        // Calculate the center point between the front wheels
        Vector3 frontAxleCenter = (frontLeftWheel.position + frontRightWheel.position) * 0.5f;

        // --- Pure Pursuit: Find lookahead target ---
        float lookaheadDistance = 3.0f; // Tune this value!
        Vector3 lookaheadTarget = waypoints[waypoints.Count - 1]; // Default to last

        foreach (Vector3 wp in waypoints)
        {
            float dist = Vector3.Distance(frontAxleCenter, wp);
            if (dist > lookaheadDistance)
            {
                lookaheadTarget = wp;
                break;
            }
        }

        // Direction from front axle center to lookahead target
        Vector3 targetDir = (lookaheadTarget - frontAxleCenter).normalized;

        // Transform direction to local space
        Vector3 localTarget = transform.InverseTransformDirection(targetDir);

        // Steer based on x offset
        float steerInput = Mathf.Clamp(localTarget.x, -1f, 1f);
        float steerAngle = steerInput * maxSteerAngle;

        flCollider.steerAngle = steerAngle;
        frCollider.steerAngle = steerAngle;

        // Speed limit check
        float currentSpeed = rb.linearVelocity.magnitude;
        float throttle = (currentSpeed < maxSpeed) ? 1f : 0f;

        // Apply motor torque to front wheels (front wheel drive)
        flCollider.motorTorque = throttle * motorTorque;
        frCollider.motorTorque = throttle * motorTorque;

        // No motor torque on rear wheels
        rlCollider.motorTorque = 0f;
        rrCollider.motorTorque = 0f;

        // Reset brake torque on all wheels
        rlCollider.brakeTorque = 0f;
        rrCollider.brakeTorque = 0f;
        flCollider.brakeTorque = 0f;
        frCollider.brakeTorque = 0f;

        UpdateWheels();
    }

    void UpdateWheels()
    {
        UpdateWheelPose(flCollider, frontLeftWheel);
        UpdateWheelPose(frCollider, frontRightWheel);
        UpdateWheelPose(rlCollider, rearLeftWheel);
        UpdateWheelPose(rrCollider, rearRightWheel);
    }

    void UpdateWheelPose(WheelCollider col, Transform wheel)
    {
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        wheel.position = pos;
        wheel.rotation = rot;
    }
}
