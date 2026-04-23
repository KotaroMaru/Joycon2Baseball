using JoyconBaseball.Phase1.Gameplay;

namespace JoyconBaseball.Phase1.Core
{
    /// <summary>
    /// PitcherController が投球を通知する先のコントローラー契約。
    /// Phase2GameController・Phase3GameController の両方が実装する。
    /// </summary>
    public interface IPitchReceiver
    {
        void NotifyPitchThrown(PitchData pitch);
    }
}
