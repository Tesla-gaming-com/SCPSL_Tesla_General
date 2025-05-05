namespace Tesla_General
{
    /// <summary>
    /// RU: DTO, описывающее «команду» от ИИ агента/менеджера-эндпоинта (убить игрока, выдать предмет, телепортировать и т.п.).
    ///     При расширении добавляйте новые поля (например, дополнительные настройки) — <see cref="TeslaCommandProcessor"/>
    ///     автоматически сможет их подхватить, если вы дополните логику в switch/case.
    /// EN: DTO representing a remote command from the AI agent/manager-endpoint (kill a player, give item, teleport, etc.).
    ///     When adding new commands, add new properties here as well (e.g., advanced settings). Then extend
    ///     <see cref="TeslaCommandProcessor"/> by adding switch/case logic for them.
    /// </summary>
    public class GameAction
    {
        // RU: Название самой команды (KillPlayer, Broadcast, GiveItem, итд).
        // EN: Name of the command (KillPlayer, Broadcast, GiveItem, etc.).
        public string Command { get; set; }

        // RU: Игрок, над которым совершаем действие (может быть отправителем или получателем).
        // EN: The player upon whom we perform the action (may be the sender or receiver).
        public string TargetPlayer { get; set; }

        // RU: Текстовое сообщение (для broadcast, CassieAnnouncement, Chat-сообщения и т.п.).
        // EN: Text message (for broadcast, CassieAnnouncement, chat messages, etc.).
        public string Message { get; set; }

        // RU: Продолжительность действия (например, длительность эффекта, мьют, время показа сообщения и т.д.).
        // EN: Duration of the action (e.g., effect duration, mute length, broadcast display time, etc.).
        public int Duration { get; set; }

        // RU: Пример дополнительного параметра (цвет, если это необходимо).
        // EN: Example additional parameter (color, if needed).
        public string Color { get; set; }

        // RU: Какой предмет выдать (ID предмета), если это команда GiveItem.
        // EN: The item to give (Item ID), if it is a GiveItem command.
        public string ItemId { get; set; }

        // RU: Какой эффект выдать игроку (ID эффекта), если это команда GiveEffect.
        // EN: The effect to give to the player (Effect ID), if it is a GiveEffect command.
        public string EffectId { get; set; }

        // RU: Целевой игрок для телепорта, если нужно телепортировать одного игрока к другому.
        // EN: The destination player for teleporting one player to another.
        public string DestinationPlayer { get; set; }

        // RU: Координаты (X, Y, Z), если это команда TeleportToCoordinates.
        // EN: Coordinates (X, Y, Z) if TeleportToCoordinates is the command.
        public float? X { get; set; }
        public float? Y { get; set; }
        public float? Z { get; set; }
    }
}
