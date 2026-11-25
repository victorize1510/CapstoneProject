using UnityEngine;

namespace GDS.Common {
    public class RotateAroundAxis : MonoBehaviour {
        [SerializeField] int speed = 20;
        [SerializeField] Vector3 axis = Vector3.up;

        void Update() {
            transform.RotateAround(transform.position, axis, speed * Time.deltaTime);
        }
    }
}
