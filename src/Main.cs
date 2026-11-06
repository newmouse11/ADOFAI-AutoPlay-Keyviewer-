using System;
using System.Collections.Generic;
using System.Reflection;
using AutoFingering.Game;
using AutoFingering.Viewers;
using HarmonyLib;
using UnityModManagerNet;

namespace AutoFingering
{
    public static class Main
    {
        internal static UnityModManager.ModEntry Mod;
        internal static Settings Settings;
        internal static Harmony Harmony;

        private static bool patched;

        // 이 시점은 Persistence.Load 이전이다. RDC와 RDConstants를 건드리지 않는다.
        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Mod = modEntry;
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            Harmony = new Harmony(modEntry.Info.Id);
            modEntry.OnToggle = OnToggle;
            modEntry.OnUpdate = OnUpdate;
            modEntry.OnShowGUI = SettingsGui.OnShow;
            modEntry.OnGUI = SettingsGui.Draw;
            modEntry.OnSaveGUI = entry => Settings.Save(entry);
            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool enabled)
        {
            if (enabled) return true;

            // AdofaiTweaks의 OnEndInputs와 키 통계 복원은 Clear 안에서 끝난다.
            Session.Clear();
            Planner.Cancel();
            if (patched)
            {
                Harmony.UnpatchAll(modEntry.Info.Id);
                PollKeys.Forget();
                patched = false;
            }

            return true;
        }

        private static void OnUpdate(UnityModManager.ModEntry modEntry, float deltaTime)
        {
            try
            {
                // Load에서 패치하면 대상 메서드의 JIT가 Persistence.Load 이전에 게임 정적 클래스를 초기화할 수 있다.
                // OnUpdate는 UMM UI가 생긴 뒤(ADOStartup.Startup 이후)에만 오므로 플레이 전에 패치가 끝난다.
                if (!patched)
                {
                    patched = true;
                    Harmony.PatchAll(Assembly.GetExecutingAssembly());
                }

                Session.Tick();
            }
            catch (Exception e)
            {
                Log.Once("update", e);
            }
        }
    }

    // 매 프레임 호출되는 경로에서 같은 예외가 로그를 덮지 않도록 항목별로 한 번만 남긴다.
    internal static class Log
    {
        private static readonly HashSet<string> Seen = new HashSet<string>();

        public static void Once(string what, Exception e)
        {
            if (Seen.Add(what)) Main.Mod.Logger.LogException(what, e);
        }

        public static void Once(string what, string message)
        {
            if (Seen.Add(what)) Main.Mod.Logger.Error(what + ": " + message);
        }
    }
}
