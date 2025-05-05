using System;
using System.Linq;
using Exiled.API.Features;
using Tesla_General.MyNewtonsoft;  // <-- Подключаем нашу библиотеку
using UnityEngine;

namespace Tesla_General.Processing
{
    /// <summary>
    /// Генерация JSON-строки со всеми нужными данными
    /// (secretKey, context, events, players, timestamps).
    /// Теперь используем MyJsonConvert.SerializeObject(...) вместо ручной сборки.
    /// </summary>
    public static class PromptGenerator
    {
        public static string GenerateDataJson(TimeSpan? timeSinceLastMtf, TimeSpan? timeSinceLastChaos)
        {
            // Если нет секретного ключа, ничего не делаем
            if (string.IsNullOrWhiteSpace(MainPlugin.Singleton.Config.SecretKey))
                return null;

            var events = EventCollector.GetRecentEvents();
            if (events.Count == 0)
            {
                if (MainPlugin.Singleton.Config.Debug)
                    Log.Info("[PromptGenerator] No events to include, returning null.");
                return null;
            }

            // Список игроков
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
                }
            }).ToArray();

            // Создаём payload
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

                events = events,       // список GameEvent
                players = playersInfo  // массив PlayerInfo
            };

            // Сериализуем через нашу библиотеку
            string json = MyJsonConvert.SerializeObject(payload);

            if (MainPlugin.Singleton.Config.Debug)
                Log.Info("[PromptGenerator] JSON content generated successfully.");

            return json;
        }
    }

    // ------------ Ниже вспомогательные классы --------------

    /// <summary>
    /// Класс пэйлода для сериализации
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
    /// Описывает одного игрока
    /// </summary>
    public class PlayerInfo
    {
        public string Nickname;
        public string Role;
        public float Health;
        public string Team;
        public bool IsAlive;
        public Vector3Wrapper Position;
    }

    /// <summary>
    /// Упаковка для Position (x,y,z) - чтобы сериализовывать как объект
    /// </summary>
    public class Vector3Wrapper
    {
        public float X;
        public float Y;
        public float Z;
    }
}
