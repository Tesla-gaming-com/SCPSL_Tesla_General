using System;
using System.Collections.Generic;
using Exiled.API.Features;

namespace Tesla_General
{
    /// <summary>
    /// RU: Центральное хранилище (коллекция) игровых событий. Служит буфером между «сырыми» событиями Exiled и последующей
    ///     отправкой агрегированного JSON в менеджер-эндпоинт. Расширяйте методами для новых типов событий (например, unban),
    ///     добавляйте больше полей/данных в AddPlayerEvent, AddModerationEvent и др.
    ///     Также можно предусмотреть фильтрацию, систему приоритетов событий и так далее.
    /// EN: Central storage for game events. Acts as a buffer between raw Exiled callbacks and subsequent JSON dispatch
    ///     to the manager-endpoint. Extend with new methods for new event types (e.g. unban), add more fields/data in
    ///     AddPlayerEvent, AddModerationEvent, etc. You can also introduce filtering, event priority systems, etc.
    /// </summary>
    public static class EventCollector
    {
        private static readonly List<GameEvent> Events = new List<GameEvent>();

        private static DateTime _lastEventTime = DateTime.MinValue;
        public static DateTime LastEventTime => _lastEventTime;

        /// <summary>
        /// RU: Добавляет событие игрока с минимальным набором параметров (action, playerName, targetName).
        /// EN: Adds a player event with minimal parameters (action, playerName, targetName).
        /// </summary>
        public static void AddPlayerEvent(string action, string playerName, string targetName = null)
        {
            AddPlayerEvent(action, playerName, targetName, null);
        }

        /// <summary>
        /// RU: Добавляет событие игрока с дополнительными данными (Dictionary). 
        /// EN: Adds a player event with additional data (Dictionary).
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
        /// RU: Добавляет системное событие (например, запуск раунда, запуск вархеда и т.д.).
        /// EN: Adds a system event (e.g., round start, warhead start, etc.).
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
        /// RU: Добавляет событие модерации (ban/kick/mute/и т.п.). 
        ///     Можно расширять подхватом большего количества данных (например, продолжительность, IP, привилегии, и т.д.).
        /// EN: Adds a moderation event (ban/kick/mute/etc.). 
        ///     Can be extended to capture more data (e.g., duration, IP, privileges, etc.).
        /// </summary>
        public static void AddModerationEvent(string action, string staffOrSystem, string targetPlayer, string reason = null)
        {
            AddModerationEvent(action, staffOrSystem, targetPlayer, reason, null);
        }

        /// <summary>
        /// RU: Добавляет событие модерации с дополнительными данными.
        /// EN: Adds a moderation event with additional data.
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
                EventType = "Moderation",
                Description = action,
                PlayerName = staffOrSystem,
                TargetName = targetPlayer,
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            if (!string.IsNullOrEmpty(reason))
            {
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
        /// RU: Заготовка для обработки события разбана (unban). Нужно вызывать этот метод когда происходит разблокировка игрока.
        ///     Сейчас не вызывается, так как Exiled не имеет события разбана по умолчанию, но вы можете вручную вызывать,
        ///     если у вас есть своя логика.
        /// EN: Stub for handling an unban event. Should be called when a player’s ban is lifted.
        ///     Currently not triggered automatically, since Exiled does not provide an unban event by default, 
        ///     but you can call it manually if you have custom logic.
        /// </summary>
        public static void AddUnbanEvent(string staffOrSystem, string targetPlayer, string reason = null, Dictionary<string, string> extraData = null)
        {
            // RU: Дополнительная реализация: AddModerationEvent("Unban", staffOrSystem, targetPlayer, reason, extraData);
            // EN: Additional implementation: AddModerationEvent("Unban", staffOrSystem, targetPlayer, reason, extraData);

            AddModerationEvent("Unban", staffOrSystem, targetPlayer, reason, extraData);
        }

        /// <summary>
        /// RU: Возвращает копию списка всех накопленных событий.
        /// EN: Returns a copy of the list of all accumulated events.
        /// </summary>
        public static List<GameEvent> GetRecentEvents()
        {
            return new List<GameEvent>(Events);
        }

        /// <summary>
        /// RU: Очищает накопленные события (после отправки на менеджер-эндпоинт).
        /// EN: Clears accumulated events (after sending them to the manager-endpoint).
        /// </summary>
        public static void ClearEvents()
        {
            Events.Clear();
        }

        /// <summary>
        /// RU: Проверка наличия событий в очереди.
        /// EN: Checks if there are any events in the queue.
        /// </summary>
        public static bool HasEvents()
        {
            return Events.Count > 0;
        }
    }
}
