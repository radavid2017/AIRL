using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class CarAgent : Agent
{
    [SerializeField] private CarController carController;
    [SerializeField] private GameObject parkingSpot;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private Rigidbody rb;

    private float lastDistanceToSpot;

    public override void Initialize()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
    }

    public override void OnEpisodeBegin()
    {
        transform.position = startPosition;
        transform.rotation = startRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        lastDistanceToSpot = Vector3.Distance(transform.position, parkingSpot.transform.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(parkingSpot.transform.localPosition);
        sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity).x);
        sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity).z);

        Vector3 toParking = parkingSpot.transform.position - transform.position;
        sensor.AddObservation(toParking.normalized);
        sensor.AddObservation(toParking.magnitude);
        sensor.AddObservation(transform.forward);
        sensor.AddObservation(parkingSpot.transform.forward);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float acceleration = Mathf.Clamp(actions.ContinuousActions[1], 0f, 1f);
        float brake = Mathf.Clamp(-actions.ContinuousActions[2], 0f, 1f);

        //carController.Move(0, actions.ContinuousActions[1], 0);

        Vector3 toParking = parkingSpot.transform.position - transform.position;
        float distanceToSpot = toParking.magnitude;
        float alignment = Vector3.Dot(-transform.forward, parkingSpot.transform.forward);

        if (distanceToSpot <= lastDistanceToSpot)
        {
            AddReward(0.01f);
        }
        else
        {
            AddReward(-0.01f); // penalizare pentru indepartare de parcarea
        }
        lastDistanceToSpot = distanceToSpot;

        AddReward(-distanceToSpot * 0.01f);
        AddReward(alignment * 0.01f); // reward pentru aliniere cu spatele

        if (distanceToSpot <= 1.5f && alignment > 0.95f)
        {
            AddReward(1f);
            EndEpisode();
        }
        if (transform.position.y < -1f)
        {
            AddReward(-1f);
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Horizontal");
        continuousActions[1] = Input.GetAxis("Vertical");
    }
}
