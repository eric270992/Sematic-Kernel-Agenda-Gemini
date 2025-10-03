using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data; // Necessari per al tipus Event
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
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
            var service = await GetCalendarService();

            EventsResource.ListRequest request = service.Events.List("primary");
            request.TimeMinDateTimeOffset = DateTime.Now;
            request.ShowDeleted = false;
            request.SingleEvents = true;
            request.MaxResults = 10;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            var events = await request.ExecuteAsync();
            Console.WriteLine("Propers esdeveniments:");
            if (events.Items == null || events.Items.Count == 0)
            {
                Console.WriteLine("No hi ha esdeveniments.");
            }
            else
            {
                foreach (var eventItem in events.Items)
                {
                    string when = eventItem.Start.DateTime.HasValue ? eventItem.Start.DateTime.Value.ToString("g") : eventItem.Start.Date;
                    Console.WriteLine($"- {eventItem.Summary} ({when})");
                }
            }
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