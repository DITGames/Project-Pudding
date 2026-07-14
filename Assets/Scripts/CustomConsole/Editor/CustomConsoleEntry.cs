/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file CustomConsoleEntry.cs
 * @author hqrse
 * @date 2026/07/13
 * @brief CustomConsoleWindowが扱う1件分のログエントリ
 * Debug.Logの生メッセージ/スタックトレースを解析し、
 * 拡張ログレベル・カテゴリタグ・送信元スクリプト情報を抽出する
 * =====================================*/
using System;
using System.Text.RegularExpressions;
using CustomConsole;
using UnityEngine;

namespace CustomConsole.Editor
{
    public class CustomConsoleEntry
    {
        private static readonly Regex sBracketRegex = new(@"^\[(?<tag>[^\]]+)\]\s*(?<rest>.*)$", RegexOptions.Singleline);
        private static readonly Regex sStackFrameRegex = new(@"^(?<member>[^\s(]+)\s*\([^()]*\)\s*(\(at (?<file>.+):(?<line>\d+)\))?\s*$");

        public CustomLogLevel Level { get; private set; }
        public LogType UnityLogType { get; private set; }
        public string Category { get; private set; } = string.Empty;
        public string Message { get; private set; } = string.Empty;
        public string RawMessage { get; private set; } = string.Empty;
        public string StackTrace { get; private set; } = string.Empty;
        public string SourceMember { get; private set; } = string.Empty;
        public string SourceType { get; private set; } = string.Empty;
        public string ScriptPath { get; private set; } = string.Empty;
        public int ScriptLine { get; private set; }
        public UnityEngine.Object Context { get; private set; }
        public DateTime Timestamp { get; private set; }
        public int FrameCount { get; private set; }

        public string SourceLabel => Context != null ? Context.name :
            (string.IsNullOrEmpty(SourceType) ? "(unknown)" : SourceType);

        public bool HasScriptLocation => !string.IsNullOrEmpty(ScriptPath);

        public static CustomConsoleEntry Parse(string aCondition, string aStackTrace, LogType aType, UnityEngine.Object aContext)
        {
            var entry = new CustomConsoleEntry
            {
                UnityLogType = aType,
                RawMessage = aCondition ?? string.Empty,
                StackTrace = aStackTrace ?? string.Empty,
                Context = aContext,
                Timestamp = DateTime.Now,
                FrameCount = Time.frameCount,
            };

            var remaining = entry.RawMessage;
            entry.Level = DefaultLevel(aType);

            var levelMatch = sBracketRegex.Match(remaining);
            if (levelMatch.Success && TryParseLevelMarker(levelMatch.Groups["tag"].Value, aType, out var parsedLevel))
            {
                entry.Level = parsedLevel;
                remaining = levelMatch.Groups["rest"].Value;
            }

            var tagMatch = sBracketRegex.Match(remaining);
            if (tagMatch.Success)
            {
                entry.Category = tagMatch.Groups["tag"].Value;
                remaining = tagMatch.Groups["rest"].Value;
            }

            entry.Message = remaining;

            ParseStackTrace(entry);

            return entry;
        }

        private static CustomLogLevel DefaultLevel(LogType aType)
        {
            return aType switch
            {
                LogType.Warning => CustomLogLevel.Warning,
                LogType.Error or LogType.Exception or LogType.Assert => CustomLogLevel.Error,
                _ => CustomLogLevel.Log,
            };
        }

        private static bool TryParseLevelMarker(string aMarker, LogType aType, out CustomLogLevel aLevel)
        {
            if (aType == LogType.Log && string.Equals(aMarker, "VERBOSE", StringComparison.OrdinalIgnoreCase))
            {
                aLevel = CustomLogLevel.Verbose;
                return true;
            }

            if ((aType is LogType.Error or LogType.Exception) &&
                string.Equals(aMarker, "CRITICAL", StringComparison.OrdinalIgnoreCase))
            {
                aLevel = CustomLogLevel.Critical;
                return true;
            }

            aLevel = CustomLogLevel.Log;
            return false;
        }

        private static void ParseStackTrace(CustomConsoleEntry aEntry)
        {
            if (string.IsNullOrEmpty(aEntry.StackTrace))
            {
                return;
            }

            foreach (var rawLine in aEntry.StackTrace.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || IsIgnoredFrame(line))
                {
                    continue;
                }

                var match = sStackFrameRegex.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                // スタックトレースの行は "Namespace.Class:Method (Args)" 形式のため、
                // 最後の':'でクラス部分とメソッド部分を分離する
                aEntry.SourceMember = match.Groups["member"].Value;
                var lastColon = aEntry.SourceMember.LastIndexOf(':');
                aEntry.SourceType = lastColon >= 0 ? aEntry.SourceMember[..lastColon] : aEntry.SourceMember;

                if (match.Groups["file"].Success)
                {
                    aEntry.ScriptPath = match.Groups["file"].Value;
                    int.TryParse(match.Groups["line"].Value, out var line1);
                    aEntry.ScriptLine = line1;
                }

                break;
            }
        }

        private static bool IsIgnoredFrame(string aLine)
        {
            return aLine.StartsWith("UnityEngine.Debug:") ||
                   aLine.StartsWith("UnityEngine.Logger:") ||
                   aLine.StartsWith("CustomConsole.CustomConsoleLog:");
        }
    }
}
