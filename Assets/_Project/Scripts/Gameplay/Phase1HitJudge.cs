using UnityEngine;

namespace JoyconBaseball.Phase1.Gameplay
{
    public enum HitResult
    {
        Out,
        Foul,
        Single,
        Double,
        HomeRun
    }

    public static class Phase1HitJudge
    {
        public static HitResult Judge(Vector3 landingPosition)
        {
            var distance = new Vector2(landingPosition.x, landingPosition.z).magnitude;

            if (distance >= 32f)
            {
                return HitResult.HomeRun;
            }

            if (distance >= 20f)
            {
                return HitResult.Double;
            }

            if (distance >= 10f)
            {
                return HitResult.Single;
            }

            return HitResult.Out;
        }
    }
}
