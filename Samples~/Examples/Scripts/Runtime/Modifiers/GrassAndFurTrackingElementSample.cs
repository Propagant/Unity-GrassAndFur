using UnityEngine;

namespace GrassAndFur.Samples
{
    public sealed class GrassAndFurTrackingElementSample : MonoBehaviour
    {
        [SerializeField] private GrassAndFurMaster targetMaster;
        [Space]
        [SerializeField, Min(1.0e-2f)] private float worldSpaceRadius = 1.0f;
        [SerializeField, Min(1.0e-2f)] private float shellCutWorldRadius = 1.0f;
        [SerializeField, Min(1.0e-2f)] private float shellWorldSpaceRadiusSmoothness = 0.5f;
        [SerializeField, Min(0)] private float shellCutHeight = 0.1f;

        private void Update()
        {
            if (targetMaster)
                targetMaster.MasterCreatePlanarTrack(
                    transform.position, worldSpaceRadius, shellCutWorldRadius, shellWorldSpaceRadiusSmoothness, shellCutHeight);
        }
    }
}