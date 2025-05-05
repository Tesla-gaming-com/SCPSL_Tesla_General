using System;
using System.Linq;
using Exiled.API.Features;
using Tesla_General.MyNewtonsoft;
using UnityEngine;

namespace Tesla_General.Processing
{
    /// <summary>
    /// RU: Формирует единый JSON-пакет со всеми событиями, информацией о раунде, игроках и т.д.,
    ///     который затем уходит на менеджер-эндпоинт.
    ///     Рекомендуется расширять этот класс: добавлять ещё больше полей (включая DNT-метки, пинг, время на сервере).
    /// EN: Builds a single JSON package with all events, round info, player data, etc.,
    ///     which is then sent to the manager-endpoint.
    ///     Recommended to extend this class: add more fields (including DNT flags, ping, time on server, etc.).
    /// </summary>
    public static class PromptGenerator
    {
        /// <summary>
        /// RU: Генерирует полный JSON с событиями, списком игроков, временными метками раунда и т.д.
        ///     При необходимости добавляйте ещё поля: например, список предметов у каждого игрока, их уровень опыта, и т.п.
        /// EN: Generates a full JSON with events, player list, round timestamps, etc.
        ///     Add more fields as needed, e.g. each player's items, experience level, etc.
        /// </summary>
        public static string GenerateDataJson(TimeSpan? timeSinceLastMtf, TimeSpan? timeSinceLastChaos)
        {
            // RU: Без секретного ключа нет смысла отправлять.
            // EN: No sense in sending data if SecretKey is missing.
            if (string.IsNullOrWhiteSpace(MainPlugin.Singleton.Config.SecretKey))
                return null;

            var events = EventCollector.GetRecentEvents();
            if (events.Count == 0)
            {
                if (MainPlugin.Singleton.Config.Debug)
                    Log.Info("[PromptGenerator] No events to include, returning null.");
                return null;
            }

            // RU: Список игроков с максимальным количеством доступных данных.
            //     Добавляйте больше полей (DNT, пинг, время на сервере, и т.д.).
            // EN: Player list with maximum available data.
            //     Add more fields (DNT, ping, time on server, etc.).
            var playersInfo = Player.List.Select(p => new PlayerInfo
            {
                Nickname = p.Nickname,
                Role = p.Role.Type.ToString(),
                Health = p.Health,
                Team = p.Role.Team.ToString(),
                IsAlive = !p.IsDead,
                Position = new Vector3Wrapper
                {
                    X = p.Position.x,
                    Y = p.Position.y,
                    Z = p.Position.z
                },

                // RU: Временно ставим Ping = 0, TimeOnServer = 0, DoNotTrack = false.
                //     Нужно реализовать реальную логику получения пинга и времени на сервере. 
                //     Также обязательно нужно добавить проверку флага DNT (DoNotTrack) и выставлять его в true, 
                //     если игрок запросил не отслеживать (соответствующая логика должна быть в плагине).
                // EN: Temporarily set Ping = 0, TimeOnServer = 0, DoNotTrack = false.
                //     Actual logic for retrieving ping and time on server must be implemented.
                //     Also must add a real check for DNT (DoNotTrack) and set to true if the player opted out.
                Ping = 0,
                TimeOnServer = 0,
                DoNotTrack = false
            }).ToArray();

            // RU: Создаём payload и заполняем поля.
            // EN: Create payload and populate fields.
            var payload = new PromptPayload
            {
                secretKey = MainPlugin.Singleton.Config.SecretKey,
                context = "SCP:SL Game Events",
                generatedTimestamp = DateTime.UtcNow.ToString("o"),

                roundStartedAt = (MainPlugin.RoundStartTime == default) ? null : MainPlugin.RoundStartTime.ToString("o"),
                roundEndedAt = MainPlugin.RoundEndTime?.ToString("o"),

                roundDuration = Round.IsStarted
                    ? Round.ElapsedTime.ToString(@"hh\:mm\:ss")
                    : "Round not started",

                lastMtfSpawnDuration = timeSinceLastMtf.HasValue
                    ? timeSinceLastMtf.Value.ToString(@"hh\:mm\:ss")
                    : "Never spawned",

                lastChaosSpawnDuration = timeSinceLastChaos.HasValue
                    ? timeSinceLastChaos.Value.ToString(@"hh\:mm\:ss")
                    : "Never spawned",

                events = events,
                players = playersInfo
            };

            // RU: Сериализуем через мини-библиотеку MyJsonConvert.
            // EN: Serialize via our mini MyJsonConvert library.
            string json = MyJsonConvert.SerializeObject(payload);

            if (MainPlugin.Singleton.Config.Debug)
                Log.Info("[PromptGenerator] JSON content generated successfully.");

            return json;
        }
    }

    // ------------ Ниже вспомогательные классы --------------

    /// <summary>
    /// RU: Класс пэйлода для сериализации. Дополняйте новыми полями (например, версия плагина, уникальный идентификатор сервера и т.д.).
    /// EN: Payload class for serialization. Extend with new fields (e.g., plugin version, unique server identifier, etc.).
    /// </summary>
    public class PromptPayload
    {
        public string secretKey;
        public string context;
        public string generatedTimestamp;

        public string roundStartedAt;
        public string roundEndedAt;
        public string roundDuration;
        public string lastMtfSpawnDuration;
        public string lastChaosSpawnDuration;

        public System.Collections.Generic.List<GameEvent> events; // берётся из EventCollector
        public PlayerInfo[] players; // массив сведений о игроках
    }

    /// <summary>
    /// RU: Описывает одного игрока. Рекомендуется хранить как можно больше подробностей:
    ///     IP, UserID, пинг, роль, команда, здоровье, текущие предметы, время на сервере, метка о DNT, и т.д.
    /// EN: Describes a single player. Recommended to store as many details as possible:
    ///     IP, UserID, ping, role, team, health, items, time on server, DNT flag, etc.
    /// </summary>
    public class PlayerInfo
    {
        public string Nickname;
        public string Role;
        public float Health;
        public string Team;
        public bool IsAlive;
        public Vector3Wrapper Position;

        // RU: Добавляем поля Ping, TimeOnServer, DoNotTrack (DNT), но пока не реализована логика их заполнения.
        // EN: Added Ping, TimeOnServer, DoNotTrack (DNT) fields, but the logic for populating them is not implemented yet.
        public int Ping;
        public double TimeOnServer;
        public bool DoNotTrack;
    }

    /// <summary>
    /// RU: Упаковка для Position (x,y,z) - чтобы сериализовывать как объект.
    /// EN: A wrapper for Position (x,y,z) to serialize it as an object.
    /// </summary>
    public class Vector3Wrapper
    {
        public float X;
        public float Y;
        public float Z;
    }
}
