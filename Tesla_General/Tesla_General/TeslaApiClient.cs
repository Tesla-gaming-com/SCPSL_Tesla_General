using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Exiled.API.Features;
using Tesla_General.Networking; // сам класс там лежит, но namespace может быть тот же
using Tesla_General.MyNewtonsoft; // <-- Наша библиотека

namespace Tesla_General.Networking
{
    /// <summary>
    /// HTTP-клиент, отправляющий собранные данные (eventsJson) в лямбду
    /// и возвращающий (и исполняющий) команды.
    /// Теперь используем MyJsonConvert для сериализации/десериализации.
    /// </summary>
    public static class TeslaApiClient
    {
        private const string ManagerLambdaUrl = "https://ixonnvoo3fuzipzq7a4iixji6u0zvxao.lambda-url.eu-north-1.on.aws/";

        private static readonly HttpClientHandler Handler = new HttpClientHandler();
        private static readonly HttpClient Client;

        private static bool _stopSendingData = false;

        static TeslaApiClient()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Client = new HttpClient(Handler);
        }

        public static void AllowSendingDataAgain()
        {
            _stopSendingData = false;
        }

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
                Log.Info("[TeslaApiClient] Sending events data JSON to manager-lambda...");

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, ManagerLambdaUrl)
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
                    Log.Error($"[TeslaApiClient] Lambda error response: {responseContent}");
                    return;
                }

                if (MainPlugin.Singleton.Config.Debug)
                    Log.Info($"[TeslaApiClient] Manager-lambda response: {responseContent}");

                ProcessLambdaResponse(responseContent);
            }
            catch (Exception ex)
            {
                Log.Error($"[TeslaApiClient] Failed to send events data: {ex.Message}");
            }
        }

        public static async Task<string> SendUserPrompt(string userPrompt)
        {
            if (string.IsNullOrWhiteSpace(MainPlugin.Singleton.Config.SecretKey))
                return "(no secret key set)";

            if (_stopSendingData)
                return "(secret key invalid; data sending stopped)";

            if (MainPlugin.Singleton.Config.Debug)
                Log.Info("[TeslaApiClient] Sending user prompt to manager-lambda.");

            // Сформируем объект:
            var payload = new
            {
                secretKey = MainPlugin.Singleton.Config.SecretKey,
                context = "UserPrompt",
                userPrompt = userPrompt
            };

            // Сериализуем в JSON
            string requestBody = MyJsonConvert.SerializeObject(payload);

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, ManagerLambdaUrl)
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
                    Log.Error($"[TeslaApiClient] Lambda response: {responseContent}");
                    return "(error sending user prompt)";
                }

                if (MainPlugin.Singleton.Config.Debug)
                    Log.Info($"[TeslaApiClient] Manager-lambda raw response: {responseContent}");

                return ProcessLambdaResponse(responseContent);
            }
            catch (Exception ex)
            {
                Log.Error($"[TeslaApiClient] Error while sending user prompt: {ex.Message}");
                return "(exception occurred)";
            }
        }

        private static string ProcessLambdaResponse(string responseJson)
        {
            if (string.IsNullOrEmpty(responseJson))
                return "";

            // Десериализуем в ManagerResponse (см. класс ниже)
            ManagerResponse mgrResponse = null;
            try
            {
                mgrResponse = MyJsonConvert.DeserializeObject<ManagerResponse>(responseJson);
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
                Log.Error("[TeslaApiClient] The manager-lambda reported that our secret key is invalid. Stopping data sends.");
            }

            // Если есть команды, передаём их на выполнение
            if (mgrResponse.Commands != null && mgrResponse.Commands.Length > 0)
            {
                TeslaCommandProcessor.ProcessActions(mgrResponse.Commands);
            }

            return mgrResponse.ChatReply ?? "";
        }
    }

    /// <summary>
    /// Модель ответа лямбды (keyStatus, chatReply, commands).
    /// </summary>
    internal class ManagerResponse
    {
        public string KeyStatus { get; set; }
        public string ChatReply { get; set; }
        public GameAction[] Commands { get; set; }
    }
}
