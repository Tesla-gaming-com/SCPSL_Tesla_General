using Exiled.API.Interfaces;

namespace Tesla_General
{
    /// <summary>
    /// RU: Конфигурационный класс плагина. Хранит глобальные настройки: IsEnabled (включён ли плагин), Debug (режим отладки) и SecretKey (секретный ключ).
    ///     При дальнейшем расширении плагина здесь можно хранить и другие глобальные параметры, например уровень логирования, настройку оповещений, DNT-стратегию и т.д.
    /// EN: Configuration class for the plugin. Stores global settings: IsEnabled (whether plugin is on), Debug mode, and SecretKey.
    ///     For future expansions, other global parameters can be placed here, such as logging level, notifications, DNT (Do Not Track) strategy, etc.
    /// </summary>
    public class Config : IConfig
    {
        /// <summary>
        /// RU: Управляет включением плагина целиком.
        /// EN: Controls whether the plugin is enabled entirely.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// RU: Включает/выключает подробный режим отладки. Выводит дополнительную информацию в логи.
        /// EN: Enables or disables detailed debug mode. Outputs extra info to logs.
        /// </summary>
        public bool Debug { get; set; } = false;

        /// <summary>
        /// RU: Секретный ключ, необходимый для идентификации при отправке данных на менеджер-эндпоинт.
        /// EN: Secret key for authentication when sending data to the manager-endpoint.
        /// </summary>
        public string SecretKey { get; set; } = "tok_svJRL-o7ECH9wlHmzgfJgfV29mJagwK7";
    }
}
