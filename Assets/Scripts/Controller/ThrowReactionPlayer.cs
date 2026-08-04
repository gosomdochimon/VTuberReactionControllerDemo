using System.Collections;
using UnityEngine;

namespace VTuberReactionControllerDemo.Controller
{
    public sealed class ThrowReactionPlayer : MonoBehaviour
    {
        [Header("Projectile")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform throwStartPoint;
        [SerializeField] private Transform hitPoint;
        [SerializeField] private Vector3 fallbackStartOffset = new Vector3(-2f, 1.6f, 0.5f);
        [SerializeField] private Vector3 fallbackHitOffset = new Vector3(0f, 1.2f, 0.2f);
        [SerializeField] private Vector3 projectileScale = new Vector3(0.18f, 0.18f, 0.18f);
        [SerializeField, Min(0.01f)] private float flightSeconds = 0.6f;
        [SerializeField, Min(0f)] private float arcHeight = 1.2f;
        [SerializeField, Min(0f)] private float spinDegreesPerSecond = 540f;

        [Header("Random Start Area")]
        [SerializeField] private Transform randomStartArea;
        [SerializeField] private RandomStartAreaShape randomStartAreaShape = RandomStartAreaShape.Sphere;
        [SerializeField, Min(0f)] private float randomStartAreaRadius = 0.5f;
        [SerializeField] private Vector3 randomStartAreaBoxSize = Vector3.one;

        [Header("Gizmos")]
        [SerializeField] private bool drawRandomStartAreaDebug = true;
        [SerializeField] private Color randomStartAreaDebugColor = Color.cyan;

        [Header("Hit")]
        [SerializeField] private GameObject hitVfxPrefab;
        [SerializeField, Min(0f)] private float hitVfxLifetimeSeconds = 2f;
        [SerializeField] private Transform avatarRoot;
        [SerializeField, Min(0f)] private float shakeDistance = 0.08f;
        [SerializeField, Min(0.01f)] private float shakeSeconds = 0.18f;
        [SerializeField] private Vector3 hitShakeDirection = new Vector3(0f, 0f, -1f);

        private GameObject activeProjectile;
        private bool hasStoredAvatarPosition;
        private Vector3 storedAvatarLocalPosition;

        private void OnDrawGizmosSelected()
        {
            DrawRandomStartAreaGizmos();
        }

        private void OnDisable()
        {
            if (activeProjectile != null)
            {
                Destroy(activeProjectile);
                activeProjectile = null;
            }

            ResetAvatarPosition();
        }

        public IEnumerator Play()
        {
            Vector3 startPosition = GetThrowStartPosition();
            Vector3 hitPosition = GetWorldPoint(hitPoint, fallbackHitOffset);
            GameObject projectile = CreateProjectile(startPosition);

            if (projectile != null)
            {
                yield return MoveProjectile(projectile.transform, startPosition, hitPosition);
                Destroy(projectile);
                activeProjectile = null;
            }

            PlayHitVfx(hitPosition);

            if (avatarRoot != null && shakeDistance > 0f)
            {
                yield return ShakeAvatar();
            }
        }

        private Vector3 GetThrowStartPosition()
        {
            if (randomStartArea != null)
            {
                return GetRandomStartAreaPosition();
            }

            return GetWorldPoint(throwStartPoint, fallbackStartOffset);
        }

        private Vector3 GetRandomStartAreaPosition()
        {
            Vector3 localPosition = randomStartAreaShape == RandomStartAreaShape.Box
                ? GetRandomBoxLocalPosition()
                : Random.insideUnitSphere * randomStartAreaRadius;

            return randomStartArea.TransformPoint(localPosition);
        }

        private Vector3 GetRandomBoxLocalPosition()
        {
            Vector3 size = new Vector3(
                Mathf.Abs(randomStartAreaBoxSize.x),
                Mathf.Abs(randomStartAreaBoxSize.y),
                Mathf.Abs(randomStartAreaBoxSize.z));

            return new Vector3(
                Random.Range(size.x * -0.5f, size.x * 0.5f),
                Random.Range(size.y * -0.5f, size.y * 0.5f),
                Random.Range(size.z * -0.5f, size.z * 0.5f));
        }

        private void DrawRandomStartAreaGizmos()
        {
            if (!drawRandomStartAreaDebug || randomStartArea == null)
            {
                return;
            }

            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;

            Gizmos.color = randomStartAreaDebugColor;
            Gizmos.matrix = randomStartArea.localToWorldMatrix;

            if (randomStartAreaShape == RandomStartAreaShape.Box)
            {
                DrawRandomStartBoxGizmos();
            }
            else
            {
                DrawRandomStartSphereGizmos();
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private void DrawRandomStartSphereGizmos()
        {
            float radius = Mathf.Max(0f, randomStartAreaRadius);
            Gizmos.DrawWireSphere(Vector3.zero, radius);
            DrawLocalGizmoAxes(new Vector3(radius, radius, radius));
        }

        private void DrawRandomStartBoxGizmos()
        {
            Vector3 size = new Vector3(
                Mathf.Abs(randomStartAreaBoxSize.x),
                Mathf.Abs(randomStartAreaBoxSize.y),
                Mathf.Abs(randomStartAreaBoxSize.z));

            Gizmos.DrawWireCube(Vector3.zero, size);
            DrawLocalGizmoAxes(size * 0.5f);
        }

        private void DrawLocalGizmoAxes(Vector3 halfSize)
        {
            Gizmos.DrawLine(new Vector3(-halfSize.x, 0f, 0f), new Vector3(halfSize.x, 0f, 0f));
            Gizmos.DrawLine(new Vector3(0f, -halfSize.y, 0f), new Vector3(0f, halfSize.y, 0f));
            Gizmos.DrawLine(new Vector3(0f, 0f, -halfSize.z), new Vector3(0f, 0f, halfSize.z));
        }

        private Vector3 GetWorldPoint(Transform point, Vector3 fallbackOffset)
        {
            if (point != null)
            {
                return point.position;
            }

            return transform.position + fallbackOffset;
        }

        private GameObject CreateProjectile(Vector3 startPosition)
        {
            GameObject projectile = projectilePrefab != null
                ? Instantiate(projectilePrefab, startPosition, Quaternion.identity)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);

            projectile.name = "Throw Projectile";
            projectile.transform.position = startPosition;
            projectile.transform.localScale = projectileScale;
            activeProjectile = projectile;
            return projectile;
        }

        private IEnumerator MoveProjectile(Transform projectile, Vector3 startPosition, Vector3 hitPosition)
        {
            float elapsedSeconds = 0f;

            while (elapsedSeconds < flightSeconds)
            {
                elapsedSeconds += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedSeconds / flightSeconds);
                Vector3 position = Vector3.Lerp(startPosition, hitPosition, t);
                position += Vector3.up * (Mathf.Sin(t * Mathf.PI) * arcHeight);

                projectile.position = position;

                if (spinDegreesPerSecond > 0f)
                {
                    projectile.Rotate(Vector3.one, spinDegreesPerSecond * Time.deltaTime, Space.Self);
                }

                yield return null;
            }

            projectile.position = hitPosition;
        }

        private void PlayHitVfx(Vector3 hitPosition)
        {
            if (hitVfxPrefab == null)
            {
                return;
            }

            GameObject hitVfx = Instantiate(hitVfxPrefab, hitPosition, Quaternion.identity);
            if (hitVfxLifetimeSeconds > 0f)
            {
                Destroy(hitVfx, hitVfxLifetimeSeconds);
            }
        }

        private IEnumerator ShakeAvatar()
        {
            storedAvatarLocalPosition = avatarRoot.localPosition;
            hasStoredAvatarPosition = true;
            Vector3 direction = hitShakeDirection.sqrMagnitude > 0f
                ? hitShakeDirection.normalized
                : Vector3.back;
            float elapsedSeconds = 0f;

            while (elapsedSeconds < shakeSeconds)
            {
                elapsedSeconds += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedSeconds / shakeSeconds);
                float fade = 1f - t;
                float wave = Mathf.Sin(t * Mathf.PI * 4f);
                avatarRoot.localPosition = storedAvatarLocalPosition + direction * (wave * fade * shakeDistance);
                yield return null;
            }

            ResetAvatarPosition();
        }

        private void ResetAvatarPosition()
        {
            if (!hasStoredAvatarPosition || avatarRoot == null)
            {
                hasStoredAvatarPosition = false;
                return;
            }

            avatarRoot.localPosition = storedAvatarLocalPosition;
            hasStoredAvatarPosition = false;
        }

        private enum RandomStartAreaShape
        {
            Sphere,
            Box
        }
    }
}
