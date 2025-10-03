using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using SemanticKernel_Agenda.CALENDAR;
using SemanticKernel_Agenda.GEMINI_C;
using SemanticKernel_Agenda.PLUGINS;

namespace SemanticKernel_Agenda
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            // 1. Configuració (configs.json)
            // Es crea un ConfigurationBuilder per carregar la configuració des d'un fitxer JSON.
            // SetBasePath estableix el directori base per buscar el fitxer de configuració (el directori actual de l'aplicació).
            // AddJsonFile afegeix el fitxer configs.json com a font de configuració.
            // 'optional: false' significa que el fitxer és obligatori.
            // 'reloadOnChange: true' permet recarregar la configuració si el fitxer canvia durant l'execució.
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("configs.json", optional: false, reloadOnChange: true);

            // Es construeix l'objecte de configuració a partir del builder, fent-la accessible.
            IConfiguration configuration = configBuilder.Build();

            // 2. Crear col·lecció de serveis per a la Injecció de Dependències (DI)
            // La injecció de dependències facilita la gestió de les dependències entre components del programari,
            // millorant la modularitat i la testabilitat.
            var services = new ServiceCollection();

            // Afegim la configuració com a servei singleton.
            // Això permet que qualsevol classe que la necessiti la pugui obtenir via DI al llarg de tota la vida de l'aplicació.
            services.AddSingleton(configuration);

            // Afegim Semantic Kernel (amb Gemini) com a singleton.
            // S'inicialitza el Kernel, que és el motor principal de Semantic Kernel per orquestrar models de llenguatge
            // i plugins, utilitzant la clau API de Gemini obtinguda de la configuració.
            services.AddSingleton<Kernel>(sp =>
            {
                // S'obté la configuració des del ServiceProvider per accedir a la clau API de Gemini.
                var config = sp.GetRequiredService<IConfiguration>();
                string? geminiApiKey = config["GeminiApiKey"];

                // Es crea el builder del Kernel i s'afegeix el connector de Gemini.
                // Es recomana utilitzar un model 'flash' recent amb suport per a tool calling.
                var builder = Kernel.CreateBuilder();
                builder.AddGoogleAIGeminiChatCompletion(
                    modelId: "gemini-2.5-flash",
                    apiKey: geminiApiKey
                );
                // Es construeix i retorna la instància del Kernel.
                return builder.Build();
            });

            // Afegim el nostre servei Calendari com a singleton.
            // Aquesta classe conté la lògica de baix nivell per interactuar directament amb l'API de Google Calendar.
            services.AddSingleton<Calendari>();

            // Afegim el servei GeminiTool com a singleton.
            // Aquest servei encapsula la lògica per interactuar amb el model Gemini de forma directa.
            // Amb la implementació de Tool Calling a través de CalendarPlugin, la seva funció pot evolucionar
            // o ser menys central per a les noves interaccions.
            services.AddSingleton<GEMINI_C.GeminiChatService>();

            // NOU: Afegim el nostre CalendarPlugin com a singleton.
            // Aquesta classe exposa funcionalitats del calendari (com comprovar disponibilitat i crear esdeveniments)
            // com a "Kernel Functions" perquè Semantic Kernel i Gemini les puguin invocar de manera intel·ligent.
            services.AddSingleton<CalendarPlugin>();

            // 3. Construir el ServiceProvider
            // El ServiceProvider és l'objecte que resol les dependències i crea instàncies dels serveis registrats
            // quan són sol·licitats.
            var serviceProvider = services.BuildServiceProvider();

            //Importem plugins al Kernel
            var kernel = serviceProvider.GetRequiredService<Kernel>();
            var calendarPlugin = serviceProvider.GetRequiredService<CalendarPlugin>();
            kernel.ImportPluginFromObject(calendarPlugin, "CalendarPlugin"); // S'afegeix al Kernel ja construït

            // 4. Resol serveis i prepara el Kernel per a la interacció amb l'usuari

            var geminiChatService = serviceProvider.GetRequiredService<GEMINI_C.GeminiChatService>();

            // Només per provar que tot funciona, es pot eliminar després
            //geminiChatService.ProcessarPregunta("Quin dia som avui?").Wait();


            Console.WriteLine("Plugin de Calendari carregat amb èxit i disponible per a Gemini.");
            // Iniciar el xat interactiu
            await geminiChatService.StartInteractiveChatAsync();

        }
    }
}
