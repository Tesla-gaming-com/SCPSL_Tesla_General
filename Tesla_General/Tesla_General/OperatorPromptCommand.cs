using System;
using System.Threading.Tasks;
using CommandSystem;
using Exiled.API.Features;
using Player = Exiled.API.Features.Player;

// Обратите внимание на это пространство имён:
using Tesla_General.Networking;  // <- Для TeslaApiClient

namespace Tesla_General
{
    /// <summary>
    /// Команда ".op [текст]" — пример «кейса» для ChatGPT:
    /// - Вы отправляете произвольный «промпт» в лямбду-менеджер;
    /// - Лямбда при необходимости пересылает это в ChatGPT;
    /// - Ответ возвращается и выводится в консоль.
    /// </summary>
    [CommandHandler(typeof(ClientCommandHandler))]
    public class OperatorPromptCommand : ICommand
    {
        public string Command => "op";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Sends a custom prompt to manager-lambda (ChatGPT or other) and prints the response in your console.";

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

            // Выводим ответ сразу
            response = "Your prompt has been sent. The response will appear in your console shortly.";

            // Асинхронно отправляем запрос
            _ = Task.Run(async () =>
            {
                try
                {
                    string lambdaReply = await TeslaApiClient.SendUserPrompt(userPrompt);

                    // Шлём ответ в консоль игрока (если он на сервере)
                    if (player != null && player.IsConnected)
                    {
                        player.SendConsoleMessage($"Lambda/ChatGPT says:\n{lambdaReply}", "yellow");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[OperatorPromptCommand] Error: {ex.Message}");
                    if (player != null && player.IsConnected)
                    {
                        player.SendConsoleMessage($"Error calling ChatGPT: {ex.Message}", "red");
                    }
                }
            });

            return true;
        }
    }
}
