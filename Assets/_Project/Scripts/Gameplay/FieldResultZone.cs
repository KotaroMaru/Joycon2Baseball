using UnityEngine;

namespace JoyconBaseball.Phase1.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public sealed class FieldResultZone : MonoBehaviour
    {
        [SerializeField] private HitResult hitResult = HitResult.Out;

        [Tooltip("true にするとボールが通過した瞬間に即判定（ファール壁・ホームラン壁用）")]
        [SerializeField] private bool resolveImmediately = false;

        public HitResult HitResult => hitResult;
        public bool ResolveImmediately => resolveImmediately;

        private void Reset()
        {
            var collider = GetComponent<Collider>();
            collider.isTrigger = true;
        }
    }
}
