using System;
using System.Threading.Tasks;
using CommandSystem;
using Exiled.API.Features;
using Player = Exiled.API.Features.Player;
using Tesla_General.Networking;

namespace Tesla_General
{
    /// <summary>
    /// RU: Команда «.op» — любой введённый текст пересылается на менеджер-эндпоинт (где работает ваш ИИ агент или иной сервис),
    ///     и ответ выводится непосредственно в консоль игрока. Можно расширить этот функционал (например, разделять ответы по ролям, 
    ///     добавлять форматирование, хранение истории).
    /// EN: “.op” command — sends any typed text to the manager-endpoint (where your AI agent or other service runs),
    ///     and prints the response to the player's console. You can extend this (e.g., role-based answers, formatting, logging conversation history).
    /// </summary>
    [CommandHandler(typeof(ClientCommandHandler))]
    public class OperatorPromptCommand : ICommand
    {
        public string Command => "op";
        public string[] Aliases => Array.Empty<string>();

        // RU: Описание для пользователя в игре (команда .op)
        // EN: Description for the in-game user (the .op command)
        public string Description => "Sends a custom prompt to manager-endpoint (AI agent or other) and prints the response in your console.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count == 0)
            {
                response = "Usage: .op <your prompt here>";
                return false;
            }

            string userPrompt = string.Join(" ", arguments);

            Player player = Player.Get(sender);
            string playerName = player?.Nickname ?? "Unknown";

            if (MainPlugin.Singleton?.Config.Debug == true)
                Log.Info($"[OperatorPromptCommand] Player '{playerName}' asked: {userPrompt}");

            response = "Your prompt has been sent. The response will appear in your console shortly.";

            _ = Task.Run(async () =>
            {
                try
                {
                    // RU: Отправляем запрос на менеджер-эндпоинт
                    // EN: Send request to manager-endpoint
                    string endpointReply = await TeslaApiClient.SendUserPrompt(userPrompt);

                    // RU: Шлём ответ в консоль игрока (если он на сервере)
                    // EN: Send the answer to the player's console (if they are still on the server)
                    if (player != null && player.IsConnected)
                    {
                        player.SendConsoleMessage($"AI agent says:\n{endpointReply}", "yellow");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[OperatorPromptCommand] Error: {ex.Message}");
                    if (player != null && player.IsConnected)
                    {
                        player.SendConsoleMessage($"Error calling the AI agent: {ex.Message}", "red");
                    }
                }
            });

            return true;
        }
    }
}
