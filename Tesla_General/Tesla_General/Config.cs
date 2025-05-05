using Exiled.API.Interfaces;

namespace Tesla_General
{
    /// <summary>
    /// Минимальный набор настроек:
    /// - IsEnabled: включён ли плагин.
    /// - Debug: выводить ли отладочные сообщения в лог.
    /// - SecretKey: ключ, полученный на вашем сайте, чтобы лямбда-менеджер понимала, от какого сервера приходят данные.
    /// </summary>
    public class Config : IConfig
    {
        public bool IsEnabled { get; set; } = true;
        public bool Debug { get; set; } = false;

        public string SecretKey { get; set; } = "";
    }
}
