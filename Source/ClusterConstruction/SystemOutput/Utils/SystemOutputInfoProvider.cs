// ============================================================================
// 文件：SystemOutputInfoProvider.cs
// 说明：SystemOutput信息提供者，使用RimWorld原版API获取现实时间和Steam账号信息
// ============================================================================

using System;

namespace DMS_Legion
{
    /// <summary>
    /// SystemOutput信息提供者
    /// 使用RimWorld原版API提供现实日期时间和Steam账号信息的获取
    /// </summary>
    public static class SystemOutputInfoProvider
    {
        /// <summary>
        /// 获取当前现实日期和时间
        /// 格式: YYYY-MM-DD HH:MM:SS
        /// </summary>
        public static string GetCurrentRealDateTime()
        {
            try
            {
                DateTime now = DateTime.Now;
                return now.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"SystemOutput: 获取现实时间失败: {ex.Message}");
                return "时间获取失败";
            }
        }

        /// <summary>
        /// 获取当前现实日期
        /// 格式: YYYY-MM-DD
        /// </summary>
        public static string GetCurrentRealDate()
        {
            try
            {
                DateTime now = DateTime.Now;
                return now.ToString("yyyy-MM-dd");
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"SystemOutput: 获取现实日期失败: {ex.Message}");
                return "日期获取失败";
            }
        }

        /// <summary>
        /// 获取当前现实时间
        /// 格式: HH:MM:SS
        /// </summary>
        public static string GetCurrentRealTime()
        {
            try
            {
                DateTime now = DateTime.Now;
                return now.ToString("HH:mm:ss");
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"SystemOutput: 获取现实时间失败: {ex.Message}");
                return "时间获取失败";
            }
        }

        /// <summary>
        /// 获取Steam账号名称
        /// 使用RimWorld原版API: Verse.SteamUtility.SteamPersonaName
        /// </summary>
        public static string GetSteamUsername()
        {
            try
            {
                // 使用RimWorld原版Steam API获取用户名
                return Verse.SteamUtility.SteamPersonaName;
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"SystemOutput: 获取Steam用户名失败: {ex.Message}");
                return "未知用户";
            }
        }

        /// <summary>
        /// 获取完整的系统信息摘要
        /// 包含日期、时间和用户名
        /// </summary>
        public static string GetSystemInfoSummary()
        {
            string dateTime = GetCurrentRealDateTime();
            string username = GetSteamUsername();

            return $"{dateTime} | 用户: {username}";
        }

        /// <summary>
        /// 获取格式化的时间戳字符串
        /// 用于日志记录
        /// </summary>
        public static string GetFormattedTimestamp()
        {
            return $"[{GetCurrentRealDateTime()}]";
        }

        /// <summary>
        /// 获取简化的用户信息
        /// 只包含用户名，用于空间有限的显示
        /// </summary>
        public static string GetUserInfo()
        {
            return $"用户: {GetSteamUsername()}";
        }

        /// <summary>
        /// 获取带时间戳的用户信息
        /// 格式: [时间] 用户名
        /// </summary>
        public static string GetTimestampedUserInfo()
        {
            string time = GetCurrentRealTime();
            string username = GetSteamUsername();

            return $"[{time}] {username}";
        }

        /// <summary>
        /// 获取显示用户名
        /// 如果无法获取Steam用户名，返回"管理者"
        /// </summary>
        public static string GetDisplayUsername()
        {
            try
            {
                string username = GetSteamUsername();
                // 检查是否是默认的错误值
                if (string.IsNullOrEmpty(username) ||
                    username == "???" ||
                    username == "未知用户" ||
                    username == "Steam用户")
                {
                    return "管理者";
                }
                return username;
            }
            catch
            {
                return "管理者";
            }
        }

        /// <summary>
        /// 获取格式化的日期字符串
        /// 格式: 年/月/日
        /// </summary>
        public static string GetFormattedDateForWelcome()
        {
            try
            {
                DateTime now = DateTime.Now;
                return now.ToString("yyyy/MM/dd");
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"SystemOutput: 获取格式化日期失败: {ex.Message}");
                return "日期获取失败";
            }
        }

        /// <summary>
        /// 获取格式化的时间字符串
        /// 格式: 时：分：秒
        /// </summary>
        public static string GetFormattedTimeForWelcome()
        {
            try
            {
                DateTime now = DateTime.Now;
                return now.ToString("HH：mm：ss");
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"SystemOutput: 获取格式化时间失败: {ex.Message}");
                return "时间获取失败";
            }
        }

        /// <summary>
        /// 根据当前时间获取问候语
        /// 6:00-10:59: 早上好
        /// 11:00-12:59: 中午好
        /// 13:00-17:59: 下午好
        /// 18:00-22:59: 晚上好
        /// 23:00-5:59: 注意休息
        /// </summary>
        public static string GetTimeBasedGreeting()
        {
            try
            {
                DateTime now = DateTime.Now;
                int hour = now.Hour;

                if (hour >= 6 && hour <= 10)
                {
                    return "早上好";
                }
                else if (hour >= 11 && hour <= 12)
                {
                    return "中午好";
                }
                else if (hour >= 13 && hour <= 17)
                {
                    return "下午好";
                }
                else if (hour >= 18 && hour <= 22)
                {
                    return "晚上好";
                }
                else // 23:00-5:59
                {
                    return "注意休息";
                }
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"SystemOutput: 获取时间问候语失败: {ex.Message}");
                return "您好";
            }
        }
    }
}
