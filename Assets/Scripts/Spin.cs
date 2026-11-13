using UnityEngine;

public class Spin : MonoBehaviour
{
    public Vector3 degreesPerSecond = new Vector3(0, 90, 0);
    void Update() { transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self); }
}
