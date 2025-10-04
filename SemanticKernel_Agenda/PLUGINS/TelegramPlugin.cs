using Microsoft.Extensions.Configuration; // Necessari per IConfiguration
using Microsoft.SemanticKernel; // Necessari per Kernel
using SemanticKernel_Agenda.GEMINI_C;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions; // Per gestionar errors específics de Telegram
using Telegram.Bot.Polling; // Necessari per ReceiverOptions
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums; // Necessari per UpdateType

namespace SemanticKernel_Agenda.PLUGINS
{
    public class TelegramPlugin
    {
        private readonly GeminiChatService _geminiChatService;
        private readonly string _telegramBotToken;

        // Constructor que accepta un Kernel i IConfiguration
        public TelegramPlugin(Kernel kernel, IConfiguration configuration)
        {
            _geminiChatService = new GeminiChatService(kernel);
            _telegramBotToken = configuration["Telegram:BotToken"] ??
                                throw new ArgumentNullException("Telegram:BotToken", "El token del bot de Telegram no està configurat a appsettings.json.");
        }

        public async Task LlegirMissatge()
        {
            // Utilitzem el token obtingut de la configuració
            string token = _telegramBotToken;

            var botClient = new TelegramBotClient(token);

            // Comprova connexió
            try
            {
                // Utilitzem GetMeAsync per comprovar la connexió
                var me = await botClient.GetMe(); // <-- Corregit aquí
                Console.WriteLine($"✅ Connectat com a bot @{me.Username} (ID: {me.Id}) amb el token: {token.Substring(0, 5)}... (mostrant només els primers 5 caràcters per seguretat)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ No s'ha pogut connectar amb l'API de Telegram. Error: {ex.Message}");
                return; // Surt si no es pot connectar
            }

            // CancellationToken per poder aturar el bot de forma controlada
            using var cts = new CancellationTokenSource();

            // Configuració de les opcions del receptor (quins tipus d'actualitzacions volem rebre)
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>() // Rebre tots els tipus d'actualitzacions
            };

            // Escolta missatges
            botClient.StartReceiving(
                updateHandler: async (ITelegramBotClient client, Update update, CancellationToken cancellationToken) =>
                {
                    // Només processem missatges de text
                    if (update.Message is not { } message) return;
                    if (message.Text is not { } text) return;

                    Console.WriteLine($"Missatge de @{message.From?.Username ?? message.From.FirstName} (ID: {message.From.Id}): {text}");

                    // Aquí és on cridem al teu GeminiChatService per processar el missatge de l'usuari.
                    // El GeminiChatService, si el Tool Calling està habilitat, intentarà utilitzar les KernelFunctions
                    // com 'CreateCalendarEvent' si el missatge de l'usuari ho requereix.
                   // string geminiResponse = await _geminiChatService.ProcessarPregunta(text, enableToolCalling: true);

                    string geminiResponse = await _geminiChatService.GetTelegramChatResponseAsync(text, true);

                    // Enviem la resposta obtinguda de Gemini de tornada a l'usuari de Telegram
                    await client.SendMessage( // <-- Corregit aquí
                        chatId: message.Chat.Id,
                        text: geminiResponse,
                        cancellationToken: cancellationToken);
                },
                errorHandler: async (ITelegramBotClient client, Exception exception, CancellationToken cancellationToken) =>
                {
                    // Gestió d'errors més robusta
                    var ErrorMessage = exception switch
                    {
                        ApiRequestException apiRequestException
                            => $"Error de l'API de Telegram:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                        _ => exception.ToString()
                    };

                    Console.WriteLine($"❌ Error en rebre o processar missatge: {ErrorMessage}");
                    await Task.CompletedTask; // Retorna un Task completat
                },
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token // Passa el CancellationToken
            );

            Console.WriteLine("📨 Escoltant missatges... Premeu Ctrl+C per aturar.");

            // Manté l'aplicació en execució fins que es premeu Ctrl+C
            await Task.Delay(-1, cts.Token);
        }
    }
}