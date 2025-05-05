namespace Tesla_General
{
    /// <summary>
    /// Модель данных, описывающая одну "команду" (действие), которую лямбда присылает.
    /// </summary>
    public class GameAction
    {
        public string Command { get; set; }
        public string TargetPlayer { get; set; }
        public string Message { get; set; }
        public int Duration { get; set; }
        public string Color { get; set; }
        public string ItemId { get; set; }
        public string EffectId { get; set; }
        public string DestinationPlayer { get; set; }
        public float? X { get; set; }
        public float? Y { get; set; }
        public float? Z { get; set; }
    }
}
