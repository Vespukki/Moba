using UnityEngine;
using UnityEditor;

public class FaceCamera : MonoBehaviour
{
    public float rotation;
    

    private void LateUpdate()
    {
        // Make the object face the camera
        transform.forward = Camera.main.transform.forward;

        // Apply rotation around the axis facing the camera
        transform.RotateAround(transform.position, transform.forward, rotation);

    }

}
