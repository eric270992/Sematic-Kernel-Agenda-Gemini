using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion; // Per a ToolCallBehavior
using Microsoft.SemanticKernel.Connectors.Google; // Per a GeminiPromptExecutionSettings
// using Microsoft.SemanticKernel.PromptExecutionSettings; // Aquesta ja NO seria necessària per a GeminiPromptExecutionSettings
using System;
using System.Threading.Tasks;

namespace SemanticKernel_Agenda.GEMINI_C
{
    /// <summary>
    /// Gestiona la interacció amb el model Gemini a través de Semantic Kernel.
    /// Aquesta classe s'encarrega del bucle de xat interactiu amb l'usuari,
    /// de la gestió de l'historial de la conversa i de la invocació automàtica de funcions (tools)
    /// definides en els plugins del Kernel.
    /// </summary>
    public class GeminiChatService // Reanomenada de GeminiTool per reflectir millor la seva responsabilitat
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatCompletionService;
        private readonly ChatHistory _chatHistory; // Historial de xat per al bucle interactiu
        private readonly ChatHistory _telegramChatHistory; // Historial de xat dedicat per a la interacció amb Telegram

        /// <summary>
        /// Constructor de GeminiChatService.
        /// S'injecta el Kernel de Semantic Kernel, i des d'ell es resol el servei de completació de xat.
        /// </summary>
        /// <param name="kernel">La instància de Kernel de Semantic Kernel.</param>
        public GeminiChatService(Kernel kernel)
        {
            _kernel = kernel;
            // Resolem el servei de xat directament des del kernel durant la construcció.
            // Això assegura que el servei de xat està disponible per a totes les operacions d'aquesta classe.
            _chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            _chatHistory = new ChatHistory(); // Inicialitzem l'historial de xat per a la conversa interactiva.
            _telegramChatHistory = new ChatHistory(); // Inicialitzem l'historial de xat per a Telegram.
            var systemPrompt = @"Ets un assistent d'agenda intel·ligent. Pots ajudar a gestionar esdeveniments al calendari.
Per fer-ho, pots utilitzar les funcions del plugin 'CalendarPlugin'.
Aquest plugin té funcions com 'CreateCalendarEvent' per crear cites i 'GetUpcomingEvents' per veure les futures cites.
Sempre que l'usuari demani una tasca relacionada amb el calendari, intenta utilitzar les eines de 'CalendarPlugin'.
Les cites que creïs per defecte seran d'1 hora de durada.
Per crear una cita, necessito el resum, la data (en format YYYY-MM-DD) i l'hora d'inici (en format HH:mm).
Si necessites més informació per utilitzar una funció, pregunta a l'usuari.
**Important:** Si la pregunta de l'usuari no està relacionada amb la gestió de cites o esdeveniments al calendari, 
respon directament amb informació general o amb la millor resposta que puguis generar, 
sense intentar utilitzar cap eina de calendari. ";


            _chatHistory.AddSystemMessage(systemPrompt);
            _telegramChatHistory.AddSystemMessage(systemPrompt);
        }

        /// <summary>
        /// Processa una pregunta puntual amb Gemini, sense utilitzar l'historial de xat intern de la classe.
        /// Pot suportar Tool Calling si el 'kernel' es passa com a argument i la configuració és apropiada.
        /// </summary>
        /// <param name="pregunta">La pregunta a fer a Gemini.</param>
        /// <param name="enableToolCalling">Si es vol habilitar Tool Calling per a aquesta pregunta específica.</param>
        public async Task<string> ProcessarPregunta(string pregunta, bool enableToolCalling = false)
        {
            Console.WriteLine($"Pregunta a Gemini: '{pregunta}'");

            ChatHistory tempChatHistory = new ChatHistory();
            tempChatHistory.AddUserMessage(pregunta);

            PromptExecutionSettings executionSettings = null;
            if (enableToolCalling)
            {
                executionSettings = new GeminiPromptExecutionSettings
                {
                    // CORRECCIÓ CLAU: Utilitzem l'enum específic del connector de Google.
                    ToolCallBehavior = Microsoft.SemanticKernel.Connectors.Google.GeminiToolCallBehavior.AutoInvokeKernelFunctions
                };
            }

            // Realitzem la crida. Si enableToolCalling és true, passem el kernel.
            var response = await _chatCompletionService.GetChatMessageContentAsync(
                tempChatHistory, // Utilitzem un historial temporal per a aquesta crida puntual
                executionSettings: executionSettings,
                kernel: enableToolCalling ? _kernel : null // Passar el kernel només si Tool Calling està habilitat
            );

            Console.WriteLine("Resposta de Gemini:");
            Console.WriteLine(response.Content);

            return $"Resposta de Gemini: {response.Content}";
        }

        /// <summary>
        /// Inicia un bucle de xat interactiu amb l'usuari, utilitzant el model Gemini
        /// per respondre preguntes i invocar Kernel Functions (eines) quan sigui pertinent.
        /// </summary>
        public async Task StartInteractiveChatAsync()
        {
            Console.WriteLine("\n--- Benvingut al planificador d'agenda amb Gemini i Semantic Kernel ---");
            Console.WriteLine("Pots demanar-me que consulti esdeveniments o que en crei de nous.");
            Console.WriteLine("Exemples: 'genera una cita per demà a les 10:00 per anar al dentista'");
            Console.WriteLine("          'Crea una reunió amb l'equip el 2025-12-25 a les 15:00'");
            Console.WriteLine("          'Quins esdeveniments tinc avui?'");
            Console.WriteLine("La durada de les cites és d'1 hora per defecte.");
            Console.WriteLine("Escriu 'sortir' per acabar.");

            while (true)
            {
                Console.Write("\nTu: ");
                string? userInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userInput) || userInput.Equals("sortir", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Adéu! Fins la pròxima.");
                    break;
                }

                // S'afegeix el missatge de l'usuari a l'historial de xat per mantenir el context.
                _chatHistory.AddUserMessage(userInput);

                // NOU: Configuració per permetre l'execució automàtica de funcions de Semantic Kernel.
                // Utilitzem GeminiPromptExecutionSettings
                var executionSettings = new GeminiPromptExecutionSettings
                {
                    // Habilita l'execució automàtica de funcions del Kernel
                    ToolCallBehavior = Microsoft.SemanticKernel.Connectors.Google.GeminiToolCallBehavior.AutoInvokeKernelFunctions
                };


                // S'obté la resposta de Gemini. Semantic Kernel gestionarà
                // les crides a les funcions (com CreateCalendarEvent) automàticament si Gemini les sol·licita
                // basant-se en la intenció de l'usuari i la descripció de les funcions del plugin.
                var result = await _chatCompletionService.GetChatMessageContentAsync(
                    _chatHistory,
                    executionSettings: executionSettings,
                    kernel: _kernel // Passar el kernel és crucial perquè l'API de Gemini pugui veure les eines i SK les pugui invocar.
                );

                // S'afegeix la resposta de Gemini (o el resultat de la crida a funció processada) a l'historial.
                _chatHistory.Add(result);

                // Es mostra la resposta de Gemini a l'usuari.
                Console.WriteLine("Gemini: " + result.Content);
            }
        }

        /// <summary>
        /// Processa un missatge de l'usuari amb Gemini, utilitzant un historial de xat proporcionat externament.
        /// Permet mantenir el context per a múltiples usuaris, ja que l'historial es passa com a paràmetre.
        /// </summary>
        /// <param name="chatHistory">L'historial de xat específic per a la conversa actual.</param>
        /// <param name="userMessage">El missatge de l'usuari a processar.</param>
        /// <param name="enableToolCalling">Si es vol habilitar Tool Calling.</param>
        /// <returns>La resposta de Gemini com a string.</returns>
        public async Task<string> GetChatResponseAsync(
            ChatHistory chatHistory,
            string userMessage,
            bool enableToolCalling = true)
        {
            Console.WriteLine($"Processant missatge de xat: '{userMessage}'");

            chatHistory.AddUserMessage(userMessage); // Afegim el missatge de l'usuari a l'historial proporcionat

            var executionSettings = new GeminiPromptExecutionSettings
            {
                ToolCallBehavior = Microsoft.SemanticKernel.Connectors.Google.GeminiToolCallBehavior.AutoInvokeKernelFunctions
            };

            var response = await _chatCompletionService.GetChatMessageContentAsync(
                chatHistory, // Utilitzem l'historial de xat passat com a paràmetre
                executionSettings: executionSettings,
                kernel: enableToolCalling ? _kernel : null
            );

            chatHistory.Add(response); // Afegim la resposta de Gemini a l'historial proporcionat

            return response.Content;
        }


        /// <summary>
        /// Processa un missatge de l'usuari de Telegram amb Gemini, utilitzant l'historial de xat intern de la classe.
        /// Aquesta funció està dissenyada per a un únic usuari/xat de Telegram.
        /// L'habilitació del Tool Calling és opcional.
        /// </summary>
        /// <param name="userMessage">El missatge de l'usuari de Telegram a processar.</param>
        /// <param name="enableToolCalling">Si es vol habilitar Tool Calling (per defecte a true).</param>
        /// <returns>La resposta de Gemini com a string.</returns>
        public async Task<string> GetTelegramChatResponseAsync(string userMessage, bool enableToolCalling = true)
        {
            Console.WriteLine($"Processant missatge de Telegram: '{userMessage}' (Tool Calling: {enableToolCalling})");

            _telegramChatHistory.AddUserMessage(userMessage);

            PromptExecutionSettings? executionSettings = null; // Inicialitzem a null
            Kernel? kernelToUse = null; // Inicialitzem a null

            if (enableToolCalling)
            {
                executionSettings = new GeminiPromptExecutionSettings
                {
                    ToolCallBehavior = Microsoft.SemanticKernel.Connectors.Google.GeminiToolCallBehavior.AutoInvokeKernelFunctions
                };
                kernelToUse = _kernel; // Només passem el kernel si Tool Calling està habilitat
            }

            var response = await _chatCompletionService.GetChatMessageContentAsync(
                _telegramChatHistory,
                executionSettings: executionSettings, // Pot ser null si Tool Calling no està habilitat
                kernel: kernelToUse // Pot ser null si Tool Calling no està habilitat
            );

            _telegramChatHistory.Add(response);

            return response.Content;
        }
    }
}