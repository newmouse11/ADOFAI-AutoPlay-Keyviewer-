using System;
using System.Collections.Generic;

namespace AutoFingering.Viewers
{
    using AutoFingering.Core;

    // 묶인 뷰어 전부에 같은 입력을 보낸다. 한 뷰어의 실패가 게임이나 다른 뷰어로 번지지 않게 여기서 막는다.
    internal static class ViewerHub
    {
        private static readonly List<ViewerAdapter> Bound = new List<ViewerAdapter>();

        private static string layoutSource = "";

        // 시도 시작 때마다 부른다. JALib 모드는 비동기로 뜨므로 로드 시점에는 아직 없다. 묶인 뷰어가 없으면 null이다.
        public static Layout Bind()
        {
            End();
            Bound.AddRange(Probe());
            if (Bound.Count == 0) return null;

            // Probe가 Jipper, LineKeyViewer, AdofaiTweaks 순서이므로 첫 번째가 배치 기준이다.
            layoutSource = Bound[0].Name;
            Layout layout = BuildLayout(Bound[0]);

            for (int i = Bound.Count - 1; i >= 0; i--)
            {
                ViewerAdapter adapter = Bound[i];
                try
                {
                    adapter.Map = MapSlots(adapter, layout);
                    adapter.Begin();
                }
                catch (Exception e)
                {
                    Log.Once(adapter.Name + " begin", e);
                    Bound.RemoveAt(i);
                }
            }

            return Bound.Count > 0 ? layout : null;
        }

        public static List<ViewerAdapter> Probe()
        {
            var result = new List<ViewerAdapter>();
            var candidates = new ViewerAdapter[] { new JipperAdapter(), new LineKeyViewerAdapter(), new TweaksAdapter() };
            foreach (ViewerAdapter adapter in candidates)
            {
                try
                {
                    if (adapter.TryBind()) result.Add(adapter);
                }
                catch (Exception e)
                {
                    Log.Once(adapter.Name + " bind", e);
                }
            }

            return result;
        }

        public static void End()
        {
            foreach (ViewerAdapter adapter in Bound)
            {
                try
                {
                    adapter.End();
                }
                catch (Exception e)
                {
                    Log.Once(adapter.Name + " end", e);
                }
            }

            Bound.Clear();
        }

        // 시도 중이면 묶인 뷰어를, 아니면 지금 묶일 뷰어를 보여 준다. 역할 수정 결과는 다음 시도부터 반영된다.
        public static Layout PreviewLayout(List<ViewerAdapter> probed, out string source, out string names)
        {
            List<ViewerAdapter> list = Bound.Count > 0 ? Bound : probed;
            source = names = "";
            if (list.Count == 0) return null;

            source = Bound.Count > 0 ? layoutSource : list[0].Name;
            var shownNames = new string[list.Count];
            for (int i = 0; i < list.Count; i++) shownNames[i] = list[i].Name;
            names = string.Join(", ", shownNames);

            return BuildLayout(list[0]);
        }

        private static Layout BuildLayout(ViewerAdapter adapter)
        {
            Layout built = Layouts.Build(adapter.KeyCodes, adapter.Labels);
            Layouts.ApplyOverrides(built, Main.Settings.RoleOverrides);
            return built;
        }

        private static int[] MapSlots(ViewerAdapter adapter, Layout target)
        {
            var map = new int[target.Slots.Length];
            for (int slot = 0; slot < map.Length; slot++)
            {
                int index = Array.IndexOf(adapter.KeyCodes, target.Slots[slot].KeyCode);
                if (index < 0 && adapter.KeyCodes.Length == map.Length) index = slot;
                map[slot] = index;
            }

            return map;
        }

        public static void Fire(int slot, bool down)
        {
            for (int i = Bound.Count - 1; i >= 0; i--)
            {
                ViewerAdapter adapter = Bound[i];
                int index = adapter.Map[slot];
                if (index < 0) continue;

                try
                {
                    if (down) adapter.Press(index);
                    else adapter.Release(index);
                }
                catch (Exception e)
                {
                    Log.Once(adapter.Name + " input", e);
                    Drop(i);
                }
            }
        }

        private static void Drop(int position)
        {
            ViewerAdapter adapter = Bound[position];
            Bound.RemoveAt(position);
            try
            {
                adapter.End();
            }
            catch (Exception e)
            {
                Log.Once(adapter.Name + " end", e);
            }
        }
    }
}
