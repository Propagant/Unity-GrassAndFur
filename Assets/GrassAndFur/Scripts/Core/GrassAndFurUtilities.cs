using UnityEngine;

namespace GrassAndFur
{
    public static class GrassAndFurUtilities
    {
        public static Vector2 ConvertWorldSpaceToUVSpace(Vector3 worldSpacePosition, Vector3 maxLocalBounds, Transform relatedToTransform, bool invertUVCoords = false)
        {
            Vector2 coords = Vector2.zero;
            Vector3 max = maxLocalBounds;
            worldSpacePosition = relatedToTransform.InverseTransformPoint(worldSpacePosition);
            // Expecting that the pivot point is in the middle
            coords.x = (worldSpacePosition.x + max.x) / (max.x * 2f);
            coords.y = (worldSpacePosition.z + max.z) / (max.z * 2f);

            if (invertUVCoords)
                coords = Vector2.one - coords;

            return coords;
        }

        public static Vector2 ConvertWorldSpaceToUVSpace(Vector2 worldSpacePosition, Vector3 maxLocalBounds, Transform relatedToTransform, bool invertUVCoords = false)
            => ConvertWorldSpaceToUVSpace(new Vector3(worldSpacePosition.x, 0, worldSpacePosition.y), maxLocalBounds, relatedToTransform, invertUVCoords);

        public static float ConvertWorldSpaceToUVSpace(float entryWorldSpaceValue, Vector3 localBoundsSize)
            => entryWorldSpaceValue / Mathf.Max(localBoundsSize.x, localBoundsSize.z);
    }
}