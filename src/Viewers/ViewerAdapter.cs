using System;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

namespace AutoFingering.Viewers
{
    internal abstract class ViewerAdapter
    {
        protected const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
        protected const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        protected const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

        protected static readonly Type[] KeyCodeArg = { typeof(KeyCode) };

        public abstract string Name { get; }

        // 뷰어 자체 배열 순서의 코드. 4096 이상은 Jipper 계열이 쓰는 네이티브 VK 코드다.
        public int[] KeyCodes;
        public string[] Labels;

        // 배치 슬롯 번호를 이 뷰어의 배열 인덱스로 바꾼다. 없는 키는 -1이다.
        public int[] Map;

        // 부작용 없이 멤버만 찾는다. 설정 화면의 미리보기에서도 쓰기 때문이다.
        public abstract bool TryBind();

        public virtual void Begin()
        {
        }

        public abstract void Press(int index);

        public abstract void Release(int index);

        public virtual void End()
        {
        }

        // JAMod은 실제 어셈블리가 뜨기 전까지 mAssembly가 부트스트랩이라 GetType이 null을 준다. 그래서 시도마다 다시 찾는다.
        protected static Type FindType(string modId, string fullName)
        {
            UnityModManager.ModEntry entry = UnityModManager.FindMod(modId);
            if (entry == null || !entry.Enabled || !entry.Active) return null;
            return entry.Assembly?.GetType(fullName, false);
        }

        // 모드는 켜져 있는데 멤버가 없으면 뷰어 쪽이 바뀐 것이다. 말없이 내장 뷰어로 넘어가면 원인을 알 수 없다.
        protected bool Missing(string member)
        {
            Log.Once(Name + " " + member, member + " 멤버를 찾지 못해 이 뷰어를 건너뜀");
            return false;
        }

        protected void SetKeys(KeyCode[] codes)
        {
            KeyCodes = new int[codes.Length];
            Labels = new string[codes.Length];
            for (int i = 0; i < codes.Length; i++)
            {
                int code = (int)codes[i];
                KeyCodes[i] = code;
                Labels[i] = code < VkOffset ? codes[i].ToString() : "VK" + (code - VkOffset);
            }
        }

        private const int VkOffset = 4096;
    }
}
