using System.Collections.Generic;

namespace Tesla_General
{
    /// <summary>
    /// RU: Модель одного собранного события. Сериализуется в JSON и отправляется на менеджер-эндпоинт для анализа ИИ агентом или другим сервисом.
    ///     При расширении плагина вы можете добавлять сюда больше полей и данных для более детальной аналитики (например, пинг атакующего и жертвы).
    /// EN: A data model for a single collected event. Serialized to JSON and sent to the manager-endpoint for analysis by the AI agent or other service.
    ///     When extending, add more fields here for richer analytics (e.g., attacker’s and victim’s ping).
    /// </summary>
    public class GameEvent
    {
        public string EventType { get; set; }
        public string Description { get; set; }
        public string PlayerName { get; set; }
        public string TargetName { get; set; }
        public string Timestamp { get; set; }
        public Dictionary<string, string> AdditionalData { get; set; }
    }
}
