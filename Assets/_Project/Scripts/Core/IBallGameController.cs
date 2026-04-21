using JoyconBaseball.Phase1.Gameplay;
using UnityEngine;

namespace JoyconBaseball.Phase1.Core
{
    /// <summary>
    /// Phase1Ball および BatController がコントローラーに対して呼ぶメソッドの契約。
    /// Phase1GameController・Phase2GameController の両方がこれを実装する。
    /// </summary>
    public interface IBallGameController
    {
        Vector3 BatterPosition { get; }
        bool    PitchInProgress { get; }

        void NotifyBallHit(Vector3 hitVelocity);
        void NotifySwingStarted();
        void NotifyPitchFinishedWithoutHit(bool wasStrike);
        void NotifyBallLanded(HitResult result);
        bool IsInsideStrikeZone(Vector3 worldPosition);
        Vector3 BuildHitVelocity(float contactPower, float verticalInput, float horizontalInput);
    }
}
