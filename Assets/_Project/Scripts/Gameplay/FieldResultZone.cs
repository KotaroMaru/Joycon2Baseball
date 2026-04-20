using UnityEngine;

namespace JoyconBaseball.Phase1.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public sealed class FieldResultZone : MonoBehaviour
    {
        [SerializeField] private HitResult hitResult = HitResult.Out;

        public HitResult HitResult => hitResult;

        private void Reset()
        {
            var collider = GetComponent<Collider>();
            collider.isTrigger = true;
        }
    }
}
