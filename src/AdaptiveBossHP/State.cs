using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TShockAPI;

namespace AdaptiveBossHP
{
    /// <summary>单个 Boss 组的动态状态（持久化到磁盘，重启不丢）。</summary>
    public class BossGroupState
    {
        /// <summary>累计成功（击败）次数 —— 驱动永久递减 Z。</summary>
        public int SuccessCount { get; set; }

        /// <summary>当前连败次数（自上次成功以来）—— 驱动临时减免 Y，成功时清零。</summary>
        public int FailStreak { get; set; }

        /// <summary>历史失败总次数（仅统计用）。</summary>
        public int TotalFails { get; set; }
    }

    public class PluginState
    {
        public Dictionary<string, BossGroupState> Groups { get; set; } =
            new Dictionary<string, BossGroupState>(StringComparer.OrdinalIgnoreCase);

        public BossGroupState GetOrCreate(string key)
        {
            if (!Groups.TryGetValue(key, out var s))
            {
                s = new BossGroupState();
                Groups[key] = s;
            }
            return s;
        }

        public void Reset(string key) => Groups.Remove(key);

        public void ResetAll() => Groups.Clear();

        public static PluginState Load(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    var st = JsonConvert.DeserializeObject<PluginState>(File.ReadAllText(path));
                    if (st?.Groups != null)
                        return st;
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError($"[AdaptiveBossHP] 状态读取失败，已重置：{ex.Message}");
                }
            }
            return new PluginState();
        }

        public void Save(string path)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
