using UnityEngine;

namespace GrassAndFur.Samples
{
    public sealed class GrassAndFurExplosionSample : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;
        [SerializeField] private GrassAndFurMaster targetMaster;
        [Space]
        [SerializeField, Range(0f, 1f)] private float explosionIntensity01 = 0.2f;
        [SerializeField] private float explosionWorldSpaceRadius = 10f;
        [SerializeField] private float explosionWorldSpaceRadiusBlendSmoothness = 2f;
        [SerializeField] private float explosionShockwaveDuration = 0.4f;
        [SerializeField] private float explosionWiggleDuration = 3f;
        [SerializeField] private float explosionImpactCutIntensity = 0.2f;
        [SerializeField] private float explosionShockwavePulsingSpeed = 5f;
        [Space]
        [SerializeField] private AnimationCurve explosionEasing = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField] private AnimationCurve explosionWiggleEasing = AnimationCurve.Linear(0, 0, 1, 1);

        private void Update()
        {
            if (mainCamera == null || targetMaster == null)
                return;

            Ray r = mainCamera.ScreenPointToRay(Input.mousePosition);

            if (Input.GetMouseButtonDown(0))
            {
                if (Physics.Raycast(r, out RaycastHit hit, 100) && hit.collider)
                {
                    targetMaster.MasterSimulatePlanarExplosion(
                        hit.point,
                        explosionWorldSpaceRadius,
                        explosionWorldSpaceRadiusBlendSmoothness,
                        explosionIntensity01,
                        explosionShockwaveDuration,
                        explosionWiggleDuration,
                        explosionImpactCutIntensity,
                        explosionShockwavePulsingSpeed,
                        explosionEasing, explosionWiggleEasing);
                }
            }
        }
    }
}