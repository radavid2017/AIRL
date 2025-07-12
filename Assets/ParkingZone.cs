using UnityEngine;

public class ParkingZone : MonoBehaviour
{
    public Collider carCollider;

    private void OnTriggerEnter(Collider other)
    {
        if (other == carCollider)
        {
            Debug.Log("Parking successful!");
            // Additional logic for rewards or other actions
        }
    }
}
