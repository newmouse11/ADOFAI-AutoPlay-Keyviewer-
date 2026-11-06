using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace AutoFingering.Game
{
    // 전부 이름으로 패치한다. 인자 타입이 D와 F에서 다르므로(long?/ulong?, hitMargin 유무)
    // 양쪽에 다 있는 __instance와 floor만 받는다. 게임에 예외를 던지지 않도록 각 패치에서 잡는다.

    [HarmonyPatch(typeof(scrPlayer), "Simulated_PlayerControl_Update")]
    internal static class AutoSnapshotPatch
    {
        // OttoHoldHit이 Hit 주변에서 RDC.auto를 잠시 켜므로 착지 안에서 읽으면 수동 플레이도 자동으로 보인다.
        private static void Prefix()
        {
            try
            {
                Snapshot();
            }
            catch (Exception e)
            {
                Log.Once("auto snapshot", e);
            }
        }

        // 이 패치는 수동 플레이에서도 매 프레임 돈다. 게임 업데이트로 멤버가 사라지면 JIT 오류가 나는데,
        // 따로 떼어 인라인을 막아야 그 오류가 위의 try 안에서 잡혀 정상 플레이를 깨지 않는다.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Snapshot()
        {
            Session.AutoSnapshot = RDC.auto;
        }
    }

    [HarmonyPatch(typeof(scrPlanet), "MoveToNextFloor")]
    internal static class LandingPatch
    {
        private static void Postfix(scrPlanet __instance, scrFloor floor)
        {
            try
            {
                Session.OnLanding(__instance, floor);
            }
            catch (Exception e)
            {
                Log.Once("landing", e);
            }
        }
    }

    [HarmonyPatch(typeof(scrController), "Start_Rewind")]
    internal static class AttemptStartPatch
    {
        private static void Postfix(scrController __instance)
        {
            try
            {
                Session.OnAttemptStart(__instance);
            }
            catch (Exception e)
            {
                Log.Once("attempt start", e);
            }
        }
    }

    [HarmonyPatch(typeof(scnGame), "ResetScene")]
    internal static class ResetScenePatch
    {
        private static void Postfix()
        {
            try
            {
                Session.Clear();
            }
            catch (Exception e)
            {
                Log.Once("reset scene", e);
            }
        }
    }

    [HarmonyPatch(typeof(scrController), "FailAction")]
    internal static class FailPatch
    {
        private static void Postfix()
        {
            try
            {
                Session.OnFail();
            }
            catch (Exception e)
            {
                Log.Once("fail", e);
            }
        }
    }

    // 마지막 착지의 MoveToNextFloor 안에서 먼저 호출된다. 여기서는 표시만 하고 불은 Tick에서 끈다.
    [HarmonyPatch(typeof(scrController), "OnLandOnPortal")]
    internal static class LevelEndPatch
    {
        private static void Postfix()
        {
            Session.OnLevelEnded();
        }
    }
}
