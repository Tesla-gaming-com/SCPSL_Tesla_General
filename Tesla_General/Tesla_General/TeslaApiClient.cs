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
    /// EN: HTTP client that sends JSON data (events, players, etc.) to your manager-endpoint and handles the response (commands/chat).
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
        /// Allow re-sending data if previously stopped due to invalid key.
        /// </summary>
        public static void AllowSendingDataAgain()
        {
            _stopSendingData = false;
        }

        /// <summary>
        /// Sends the events JSON to the manager-endpoint.
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
                {
                    Log.Info($"[TeslaApiClient] Manager-endpoint response:");
                    Log.Info(responseContent);
                }

                ProcessManagerEndpointResponse(responseContent);
            }
            catch (Exception ex)
            {
                Log.Error($"[TeslaApiClient] Failed to send events data: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a user prompt (the .op command) to the manager-endpoint.
        /// </summary>
        public static async Task<string> SendUserPrompt(string userPrompt)
        {
            if (string.IsNullOrWhiteSpace(MainPlugin.Singleton.Config.SecretKey))
                return "(no secret key set)";

            if (_stopSendingData)
                return "(secret key invalid; data sending stopped)";

            if (MainPlugin.Singleton.Config.Debug)
                Log.Info("[TeslaApiClient] Sending user prompt to manager-endpoint.");

            var payload = new
            {
                secretKey = MainPlugin.Singleton.Config.SecretKey,
                context = "UserPrompt",
                userPrompt = userPrompt
            };

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
                {
                    Log.Info("[TeslaApiClient] Manager-endpoint raw response:");
                    Log.Info(responseContent);
                }

                return ProcessManagerEndpointResponse(responseContent);
            }
            catch (Exception ex)
            {
                Log.Error($"[TeslaApiClient] Error while sending user prompt: {ex.Message}");
                return "(exception occurred)";
            }
        }

        private static string ProcessManagerEndpointResponse(string responseJson)
        {
            if (string.IsNullOrEmpty(responseJson))
                return "";

            // 1) Если лямбда вернула HTML (например, <!DOCTYPE html>), мы не хотим парсить это как JSON
            var trimmed = responseJson.TrimStart();
            if (trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warn("[TeslaApiClient] Manager-endpoint returned HTML instead of JSON. Skipping parse...");
                return "";
            }

            // 2) Парсим JSON в JToken
            JObject rootJToken;
            try
            {
                rootJToken = (JObject)MyJsonConvert.Parse(responseJson);
            }
            catch (Exception ex)
            {
                Log.Error($"[TeslaApiClient] Invalid JSON format: {ex.Message}");
                return "";
            }

            // 3) Извлекаем поля вручную
            string keyStatus = rootJToken["keyStatus"]?.ToObject<string>() ?? "";
            string chatReply = rootJToken["chatReply"]?.ToObject<string>() ?? "";

            // 4) Извлекаем commands
            JToken commandsToken = rootJToken["commands"];
            GameAction[] commands = Array.Empty<GameAction>();
            if (commandsToken is JArray arr)
            {
                try
                {
                    commands = arr.ToObject<GameAction[]>();
                }
                catch (Exception ex)
                {
                    Log.Error($"[TeslaApiClient] ManagerResponse parse error (commands array): {ex.Message}");
                }
            }
            else
            {
                if (MainPlugin.Singleton.Config.Debug)
                    Log.Warn("[TeslaApiClient] 'commands' is missing or not an array in the manager-endpoint response.");
            }

            // 5) Если ключ недействителен
            if (!string.IsNullOrEmpty(keyStatus) && keyStatus.Equals("invalid", StringComparison.OrdinalIgnoreCase))
            {
                _stopSendingData = true;
                Log.Error("[TeslaApiClient] The manager-endpoint reported that our secret key is invalid. Stopping data sends.");
            }

            // 6) Выполняем команды, если есть
            if (commands.Length > 0)
            {
                TeslaCommandProcessor.ProcessActions(commands);
            }

            // 7) Возвращаем chatReply (используется в .op команде)
            return chatReply;
        }
    }
}
