using System;
using System.Linq;
using System.Reflection;
using MonoMod.RuntimeDetour;
using Terraria;

namespace NoNaturalInvasion
{
    /// <summary>原版 Main.StartInvasion 的签名（type：1=哥布林军团、3=海盗入侵）。</summary>
    public delegate void StartInvasionOrig(int type);

    /// <summary>detour 回调签名：首参为原版调用委托（MonoMod 约定）。</summary>
    internal delegate void StartInvasionDetourDelegate(StartInvasionOrig orig, int type);

    /// <summary>
    /// Main.StartInvasion 的 MonoMod 桥接：本文件是整个插件唯一触碰 MonoMod 类型的文件。
    /// 按"名字 + 单 int 参数"绑定重载；若服务器运行时缺 MonoMod 或 1.4.5 改了签名，
    /// TryCreate 会抛异常，由 Initialize 捕获并降级为纯计时模式。
    /// </summary>
    internal static class DetourBridge
    {
        public static IDisposable? TryCreate(Delegate detour)
        {
            var mi = typeof(Main).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == nameof(Main.StartInvasion)
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(int))
                ?? throw new MissingMemberException(typeof(Main).FullName, "StartInvasion(int)");

            return new Hook(mi, detour);
        }
    }
}
