using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data; // Necessari per al tipus Event
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticKernel_Agenda.CALENDAR
{
    public class Calendari
    {
        // El scope ha de ser de lectura i escriptura per crear esdeveniments.
        // El constructor ja fa servir 'CalendarService.Scope.Calendar'
        // que inclou escriptura, així que podem mantenir-ho o fins i tot eliminar aquesta línia
        // si no s'utilitza explícitament més endavant.
        // static string[] Scopes = { CalendarService.Scope.CalendarReadonly }; // Aquesta línia pot ser enganyosa, millor comentar-la o eliminar-la.
        static string ApplicationName = "SemanticKernel Agenda";
        string clientId = "";
        string clientSecret = "";
        string tokenFolder = "";

        private readonly IConfiguration _configuration;
        private CalendarService _calendarService; // Afegim una instància del servei de calendari

        public Calendari(IConfiguration configuration)
        {
            _configuration = configuration;

            clientId = _configuration["Calendar:ClientId"] ?? throw new InvalidOperationException("Falta Calendar:ClientId");
            clientSecret = _configuration["Calendar:ClientSecret"] ?? throw new InvalidOperationException("Falta Calendar:ClientSecret");
            tokenFolder = _configuration["Calendar:TokenFolder"] ?? "token.json";
        }

        // Mètode per inicialitzar el servei de Google Calendar i obtenir credencials
        private async Task<CalendarService> GetCalendarService()
        {
            if (_calendarService == null)
            {
                UserCredential credential;

                var clientSecrets = new ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                };

                // Utilitzem el scope complet de calendari per a lectura i escriptura
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    clientSecrets,
                    new[] { CalendarService.Scope.Calendar }, // S'assegura que es demanen permisos d'escriptura
                    "user",
                    CancellationToken.None,
                    new FileDataStore(tokenFolder, true)
                );

                _calendarService = new CalendarService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName,
                });
            }
            return _calendarService;
        }


        public async Task ObtenirUltimsEvents()
        {
            // 1. Obtenir el Servei de Calendari (amb autenticació)
            var service = await GetCalendarService();
            // Aquesta línia crida al mètode privat 'GetCalendarService()' que s'encarrega de:
            //   - Realitzar el procés d'autenticació OAuth2 amb Google (si no s'ha fet abans o el token ha caducat).
            //   - Utilitzar les credencials (ClientId, ClientSecret) de 'configs.json'.
            //   - Emmagatzemar o carregar el token d'accés des de 'token.json'.
            //   - Finalment, retorna una instància inicialitzada de 'CalendarService' que ja està autenticada
            //     i llesta per fer crides a l'API de Google Calendar.

            // 2. Crear una Sol·licitud per Llistar Esdeveniments
            EventsResource.ListRequest request = service.Events.List("primary");
            // Es crea un objecte 'ListRequest' que representa la sol·licitud per obtenir una llista d'esdeveniments.
            // 'service.Events.List' és el mètode per iniciar aquesta sol·licitud.
            // "primary" indica que volem obtenir esdeveniments del calendari principal de l'usuari (el seu calendari per defecte).

            // 3. Configurar els Paràmetres de la Sol·licitud
            request.TimeMinDateTimeOffset = DateTime.Now;
            // Estableix el filtre perquè només es retornin esdeveniments que comencen A PARTIR de la data i hora actual.
            // 'DateTime.Now' obté la data i hora actual del sistema.
            // 'TimeMinDateTimeOffset' és la propietat preferida per a la gestió de dates/hores amb zones horàries.

            request.ShowDeleted = false;
            // Indica a l'API que NO inclogui els esdeveniments que l'usuari hagi marcat com a eliminats.

            request.SingleEvents = true;
            // Si hi ha esdeveniments recurrents (per exemple, una reunió cada dilluns),
            // aquesta propietat fa que l'API retorni cada ocurrència de l'esdeveniment recurrent per separat
            // com a esdeveniments individuals en lloc de l'esdeveniment recurrent mestre.

            request.MaxResults = 10;
            // Limita el nombre màxim d'esdeveniments que es retornaran en la resposta a 10.
            // Això evita carregar massa dades innecessàriament.

            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
            // Ordena els esdeveniments retornats per la seva data i hora d'inici.
            // 'StartTime' significa que els esdeveniments més propers en el temps apareixeran primer.

            // 4. Executar la Sol·licitud a l'API de Google Calendar
            var events = await request.ExecuteAsync();
            // Envia la sol·licitud configurada a l'API de Google Calendar de manera asíncrona.
            // La resposta, que conté els esdeveniments, s'emmagatzema a la variable 'events'.

            // 5. Processar i Mostrar els Resultats
            Console.WriteLine("Propers esdeveniments:");
            if (events.Items == null || events.Items.Count == 0)
            {
                // Si no hi ha cap esdeveniment retornat o la llista d'elements és buida.
                Console.WriteLine("No hi ha esdeveniments.");
            }
            else
            {
                // Si s'han trobat esdeveniments, es recorre la llista.
                foreach (var eventItem in events.Items)
                {
                    // Per cada esdeveniment:
                    // S'intenta obtenir la data i hora d'inici amb 'DateTime.HasValue'.
                    // Si té un valor de data i hora concret, es formatada amb "g" (format general de data i hora).
                    // Si no (p. ex., és un esdeveniment de tot el dia), s'utilitza només la propietat 'Date'.
                    string when = eventItem.Start.DateTime.HasValue ? eventItem.Start.DateTime.Value.ToString("g") : eventItem.Start.Date;
                    // S'imprimeix el resum (títol) de l'esdeveniment i la seva data/hora d'inici a la consola.
                    Console.WriteLine($"- {eventItem.Summary} ({when})");
                }
            }
        }

        /// <summary>
        /// Obté una llista dels esdeveniments del calendari principal entre una data d'inici i una data de fi.
        /// </summary>
        /// <param name="startDate">La data i hora d'inici del rang (inclusiu).</param>
        /// <param name="endDate">La data i hora de fi del rang (exclusiu).</param>
        /// <returns>Una llista d'objectes Event de Google Calendar.</returns>
        public async Task<IList<Google.Apis.Calendar.v3.Data.Event>> ObtenirEventsEntreDates(DateTimeOffset startDate, DateTimeOffset endDate)
        {
            var service = await GetCalendarService();

            EventsResource.ListRequest request = service.Events.List("primary");
            request.TimeMinDateTimeOffset = startDate; // Estableix la data i hora mínima
            request.TimeMaxDateTimeOffset = endDate;   // Estableix la data i hora màxima
            request.ShowDeleted = false;
            request.SingleEvents = true;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
            // No limitem MaxResults aquí per defecte, per obtenir tots els de la franja.
            // Si la franja pot ser molt gran, potser voldríem afegir paginació o un límit.

            var events = await request.ExecuteAsync();

            // Retornem la llista d'esdeveniments directament.
            // La impressió a consola es farà des d'on es cridi aquest mètode,
            // o bé en un mètode wrapper del CalendarPlugin.
            return events.Items ?? new List<Google.Apis.Calendar.v3.Data.Event>();
        }

        /// <summary>
        /// Comprova la disponibilitat per a una franja horària específica.
        /// </summary>
        /// <param name="startTime">Hora d'inici de la franja a comprovar.</param>
        /// <param name="endTime">Hora de finalització de la franja a comprovar.</param>
        /// <returns>True si està disponible, false si hi ha un conflicte.</returns>
        public async Task<bool> CheckAvailability(DateTimeOffset startTime, DateTimeOffset endTime)
        {
            var service = await GetCalendarService();

            EventsResource.ListRequest request = service.Events.List("primary");
            request.TimeMinDateTimeOffset = startTime;
            request.TimeMaxDateTimeOffset = endTime;
            request.ShowDeleted = false;
            request.SingleEvents = true;

            var events = await request.ExecuteAsync();

            // Si no hi ha esdeveniments en la franja, està disponible.
            return events.Items == null || events.Items.Count == 0;
        }

        /// <summary>
        /// Crea un nou esdeveniment al calendari.
        /// </summary>
        /// <param name="summary">Resum/títol de l'esdeveniment.</param>
        /// <param name="startTime">Hora d'inici de l'esdeveniment.</param>
        /// <param name="endTime">Hora de finalització de l'esdeveniment.</param>
        /// <returns>L'esdeveniment creat.</returns>
        public async Task<Event> CreateEvent(string summary, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            var service = await GetCalendarService();

            Event newEvent = new Event()
            {
                Summary = summary,
                Start = new EventDateTime()
                {
                    DateTimeDateTimeOffset = startTime,
                    TimeZone = _configuration["Calendar:DefaultTimeZone"] // Utilitzem el TimeZone de la configuració
                },
                End = new EventDateTime()
                {
                    DateTimeDateTimeOffset = endTime,
                    TimeZone = _configuration["Calendar:DefaultTimeZone"]
                }
            };

            EventsResource.InsertRequest request = service.Events.Insert(newEvent, "primary");
            Event createdEvent = await request.ExecuteAsync();

            return createdEvent;
        }
    }
}