using System;
using System.Collections.Generic;
using Exiled.API.Features;

namespace Tesla_General
{
    /// <summary>
    /// Сбор и хранение событий, произошедших на сервере.
    /// </summary>
    public static class EventCollector
    {
        private static readonly List<GameEvent> Events = new List<GameEvent>();

        // Время последнего добавленного события
        private static DateTime _lastEventTime = DateTime.MinValue;
        public static DateTime LastEventTime => _lastEventTime;

        /// <summary>
        /// Добавить «PlayerAction» (упрощённая версия, без дополнительных данных).
        /// </summary>
        public static void AddPlayerEvent(string action, string playerName, string targetName = null)
        {
            AddPlayerEvent(action, playerName, targetName, null);
        }

        /// <summary>
        /// Добавить «PlayerAction» с дополнительными данными (роль, IP, UserId и прочее).
        /// </summary>
        public static void AddPlayerEvent(string action, string playerName, string targetName, Dictionary<string, string> extraData)
        {
            if (MainPlugin.Singleton?.Config.Debug == true)
            {
                var dbg = $"[EventCollector] Player event: {action} by {playerName}";
                if (targetName != null) dbg += $" -> {targetName}";
                if (extraData != null) dbg += $" [details={extraData.Count}]";
                Log.Info(dbg);
            }

            var ev = new GameEvent
            {
                EventType = "PlayerAction",
                Description = action,
                PlayerName = playerName,
                TargetName = targetName,
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            if (extraData != null && extraData.Count > 0)
                ev.AdditionalData = extraData;

            Events.Add(ev);
            _lastEventTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Добавить «SystemEvent».
        /// </summary>
        public static void AddSystemEvent(string description)
        {
            if (MainPlugin.Singleton?.Config.Debug == true)
            {
                Log.Info($"[EventCollector] System event: {description}");
            }

            Events.Add(new GameEvent
            {
                EventType = "SystemEvent",
                Description = description,
                Timestamp = DateTime.UtcNow.ToString("o")
            });

            _lastEventTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Добавить «ModerationEvent» (упрощённая версия, без дополнительных данных).
        /// </summary>
        public static void AddModerationEvent(string action, string staffOrSystem, string targetPlayer, string reason = null)
        {
            AddModerationEvent(action, staffOrSystem, targetPlayer, reason, null);
        }

        /// <summary>
        /// Добавить «ModerationEvent» с дополнительными данными.
        /// </summary>
        public static void AddModerationEvent(string action, string staffOrSystem, string targetPlayer, string reason, Dictionary<string, string> extraData)
        {
            if (MainPlugin.Singleton?.Config.Debug == true)
            {
                var dbg = $"[EventCollector] Moderation event: {action} by {staffOrSystem} -> {targetPlayer}";
                if (!string.IsNullOrEmpty(reason)) dbg += $" (reason={reason})";
                if (extraData != null && extraData.Count > 0) dbg += $" [details={extraData.Count}]";
                Log.Info(dbg);
            }

            var ev = new GameEvent
            {
                EventType = "Moderation",  // Позволяет отличать эти логи от обычных
                Description = action,      // например: "Ban", "Kick", "Mute", "RA_Command", и т.д.
                PlayerName = staffOrSystem, // кто выполнил действие (админ, система)
                TargetName = targetPlayer,  // над кем совершается действие
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            if (!string.IsNullOrEmpty(reason))
            {
                // Если в extraData ещё нет "Reason", добавим его туда
                if (extraData == null)
                    extraData = new Dictionary<string, string>();

                if (!extraData.ContainsKey("Reason"))
                    extraData["Reason"] = reason;
            }

            if (extraData != null && extraData.Count > 0)
                ev.AdditionalData = extraData;

            Events.Add(ev);
            _lastEventTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Получить текущий список собранных событий.
        /// </summary>
        public static List<GameEvent> GetRecentEvents()
        {
            return new List<GameEvent>(Events);
        }

        /// <summary>
        /// Очистить список событий.
        /// </summary>
        public static void ClearEvents()
        {
            Events.Clear();
        }

        /// <summary>
        /// Проверка, есть ли события.
        /// </summary>
        public static bool HasEvents()
        {
            return Events.Count > 0;
        }
    }
}

