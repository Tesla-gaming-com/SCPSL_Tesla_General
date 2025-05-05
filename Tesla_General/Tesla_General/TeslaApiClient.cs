using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Exiled.API.Features;
using Tesla_General.MyNewtonsoft;

namespace Tesla_General.Networking
{
    /// <summary>
    /// RU: HTTP-клиент, который отправляет JSON-данные (события, игроков и т.д.) на ваш менеджер-эндпоинт и обрабатывает ответ (команды/чат).
    ///     Можно расширить, например, добавив систему ретраев, очередь запросов, прокси-поддержку, SSL-настройки и т.д.
    /// EN: HTTP client that sends JSON data (events, players, etc.) to your manager-endpoint and handles the response (commands/chat).
    ///     It can be extended with retries, request queuing, proxy support, SSL settings, etc.
    /// </summary>
    public static class TeslaApiClient
    {
        private const string ManagerEndpointUrl = "https://ixonnvoo3fuzipzq7a4iixji6u0zvxao.lambda-url.eu-north-1.on.aws/";

        private static readonly HttpClientHandler Handler = new HttpClientHandler();
        private static readonly HttpClient Client;

        private static bool _stopSendingData = false;

        static TeslaApiClient()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Client = new HttpClient(Handler);
        }

        /// <summary>
        /// RU: Разрешает повторную отправку данных, если ранее мы остановили из-за недействительного ключа.
        /// EN: Allows sending data again, in case we previously stopped due to an invalid key.
        /// </summary>
        public static void AllowSendingDataAgain()
        {
            _stopSendingData = false;
        }

        /// <summary>
        /// RU: Отправляет события в формате JSON на менеджер-эндпоинт.
        /// EN: Sends event data in JSON format to the manager-endpoint.
        /// </summary>
        public static async Task SendEventsData(string eventsJson)
        {
            if (string.IsNullOrWhiteSpace(eventsJson))
                return;

            if (_stopSendingData)
            {
                if (MainPlugin.Singleton.Config.Debug)
                    Log.Warn("[TeslaApiClient] Data sending is stopped due to invalid key. Skipping.");
                return;
            }

            if (MainPlugin.Singleton.Config.Debug)
                Log.Info("[TeslaApiClient] Sending events data JSON to manager-endpoint...");

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, ManagerEndpointUrl)
                {
                    Content = new StringContent(eventsJson, Encoding.UTF8, "application/json"),
                    Version = new Version(1, 1)
                };

                request.Headers.TryAddWithoutValidation("User-Agent", "Tesla_General-Plugin/1.0");

                var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Log.Error($"[TeslaApiClient] Response code: {(int)response.StatusCode} {response.StatusCode}");
                    Log.Error($"[TeslaApiClient] Manager-endpoint error response: {responseContent}");
                    return;
                }

                if (MainPlugin.Singleton.Config.Debug)
                    Log.Info($"[TeslaApiClient] Manager-endpoint response: {responseContent}");

                ProcessManagerEndpointResponse(responseContent);
            }
            catch (Exception ex)
            {
                Log.Error($"[TeslaApiClient] Failed to send events data: {ex.Message}");
            }
        }

        /// <summary>
        /// RU: Отправляет пользовательский запрос (команду .op) на менеджер-эндпоинт.
        /// EN: Sends a user prompt (the .op command) to the manager-endpoint.
        /// </summary>
        public static async Task<string> SendUserPrompt(string userPrompt)
        {
            if (string.IsNullOrWhiteSpace(MainPlugin.Singleton.Config.SecretKey))
                return "(no secret key set)";

            if (_stopSendingData)
                return "(secret key invalid; data sending stopped)";

            if (MainPlugin.Singleton.Config.Debug)
                Log.Info("[TeslaApiClient] Sending user prompt to manager-endpoint.");

            // RU: Формируем объект для отправки.
            // EN: Build the payload object.
            var payload = new
            {
                secretKey = MainPlugin.Singleton.Config.SecretKey,
                context = "UserPrompt",
                userPrompt = userPrompt
            };

            // RU: Сериализуем в JSON.
            // EN: Serialize to JSON.
            string requestBody = MyJsonConvert.SerializeObject(payload);

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, ManagerEndpointUrl)
                {
                    Content = new StringContent(requestBody, Encoding.UTF8, "application/json"),
                    Version = new Version(1, 1)
                };

                request.Headers.TryAddWithoutValidation("User-Agent", "Tesla_General-Plugin/1.0");

                var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Log.Error($"[TeslaApiClient] Error code: {(int)response.StatusCode} ({response.StatusCode})");
                    Log.Error($"[TeslaApiClient] Manager-endpoint response: {responseContent}");
                    return "(error sending user prompt)";
                }

                if (MainPlugin.Singleton.Config.Debug)
                    Log.Info($"[TeslaApiClient] Manager-endpoint raw response: {responseContent}");

                return ProcessManagerEndpointResponse(responseContent);
            }
            catch (Exception ex)
            {
                Log.Error($"[TeslaApiClient] Error while sending user prompt: {ex.Message}");
                return "(exception occurred)";
            }
        }

        /// <summary>
        /// RU: Обрабатывает ответ с менеджер-эндпоинта, парсит команды (GameAction[]) и, при необходимости, выполняет их.
        /// EN: Processes the manager-endpoint response, parses commands (GameAction[]), and executes them if needed.
        /// </summary>
        private static string ProcessManagerEndpointResponse(string responseJson)
        {
            if (string.IsNullOrEmpty(responseJson))
                return "";

            // RU: Десериализуем в ManagerResponse (см. класс ниже).
            // EN: Deserialize into ManagerResponse (see class below).
            ManagerResponse mgrResponse = null;
            try
            {
                mgrResponse = MyNewtonsoft.MyJsonConvert.DeserializeObject<ManagerResponse>(responseJson);
            }
            catch (Exception ex)
            {
                Log.Error($"[TeslaApiClient] ManagerResponse parse error: {ex.Message}");
                return "";
            }

            if (mgrResponse == null)
                return "";

            if (!string.IsNullOrEmpty(mgrResponse.KeyStatus) &&
                mgrResponse.KeyStatus.Equals("invalid", StringComparison.OrdinalIgnoreCase))
            {
                _stopSendingData = true;
                Log.Error("[TeslaApiClient] The manager-endpoint reported that our secret key is invalid. Stopping data sends.");
            }

            // RU: Если есть команды, выполняем их.
            // EN: If commands are present, we process them.
            if (mgrResponse.Commands != null && mgrResponse.Commands.Length > 0)
            {
                TeslaCommandProcessor.ProcessActions(mgrResponse.Commands);
            }

            return mgrResponse.ChatReply ?? "";
        }
    }

    /// <summary>
    /// RU: Модель ответа менеджера-эндпоинта (keyStatus, chatReply, commands).
    /// EN: Model for the manager-endpoint response (keyStatus, chatReply, commands).
    /// </summary>
    internal class ManagerResponse
    {
        public string KeyStatus { get; set; }
        public string ChatReply { get; set; }
        public GameAction[] Commands { get; set; }
    }
}
