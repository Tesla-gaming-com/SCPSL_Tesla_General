using System.Collections.Generic;

namespace Tesla_General
{
    /// <summary>
    /// Описывает единичное событие, которое мы собираем.
    /// EventType: "PlayerAction" или "SystemEvent".
    /// Description: описание события.
    /// PlayerName / TargetName: кто инициатор и кто «цель».
    /// Timestamp: UTC-время в формате ISO.
    /// AdditionalData: любые дополнительные поля при желании (необязательно).
    /// </summary>
    public class GameEvent
    {
        public string EventType { get; set; }    // "PlayerAction" или "SystemEvent"
        public string Description { get; set; }
        public string PlayerName { get; set; }
        public string TargetName { get; set; }
        public string Timestamp { get; set; }
        public Dictionary<string, string> AdditionalData { get; set; }
    }
}
