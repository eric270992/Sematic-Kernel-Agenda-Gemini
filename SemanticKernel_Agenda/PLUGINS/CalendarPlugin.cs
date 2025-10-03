using Google.Apis.Calendar.v3.Data;
using Microsoft.SemanticKernel;
using SemanticKernel_Agenda.CALENDAR; // Assegura't que el namespace és correcte per a Calendari
using System;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace SemanticKernel_Agenda.PLUGINS
{
    /// <summary>
    /// Aquesta classe actuarà com un plugin per a Semantic Kernel,
    /// exposant funcionalitats del calendari com a "Kernel Functions".
    /// En el context de Semantic Kernel, una [KernelFunction] és l'equivalent a una "Tool" en frameworks com LangChain,
    /// permetent al model de llenguatge (LLM) invocar accions externes de manera estructurada.
    /// </summary>
    public class CalendarPlugin
    {
        private readonly Calendari _calendari;

        public CalendarPlugin(Calendari calendari)
        {
            _calendari = calendari;
        }

        /// <summary>
        /// Obté una llista dels propers esdeveniments del calendari de l'usuari.
        /// </summary>
        /// <returns>Una cadena amb la llista dels propers esdeveniments.</returns>
        [KernelFunction, Description("Obté una llista dels propers esdeveniments del calendari de l'usuari.")]
        public async Task<string> GetUpcomingEvents()
        {
            Console.WriteLine("Crida a la funció: GetUpcomingEvents");
            await _calendari.ObtenirUltimsEvents();
            return "S'han mostrat els propers esdeveniments a la consola."; // O podríem retornar una llista en format string si el LLM la necessita
        }

        /// <summary>
        /// Comprova la disponibilitat en una franja horària i crea un esdeveniment si la franja està lliure.
        /// La durada de l'esdeveniment es fixa a 1 hora.
        /// </summary>
        /// <param name="summary">El resum o títol de l'esdeveniment (ex: "Reunió", "Cita mèdica").</param>
        /// <param name="dateString">La data de l'esdeveniment en format "YYYY-MM-DD".</param>
        /// <param name="timeString">L'hora d'inici de l'esdeveniment en format "HH:mm".</param>
        /// <returns>Un missatge indicant si l'esdeveniment s'ha creat o si hi ha un conflicte.</returns>
        [KernelFunction, Description("Comprova la disponibilitat i crea un esdeveniment al calendari amb una durada d'1 hora.")]
        public async Task<string> CreateCalendarEvent(
            [Description("El resum o títol de l'esdeveniment (ex: \"Reunió\", \"Cita mèdica\").")] string summary,
            [Description("La data de l'esdeveniment en format 'YYYY-MM-DD'.")] string dateString,
            [Description("L'hora d'inici de l'esdeveniment en format 'HH:mm'.")] string timeString)
        {
            Console.WriteLine($"Crida a la funció: CreateCalendarEvent amb Summary: '{summary}', Data: '{dateString}', Hora: '{timeString}'");

            // Combinar data i hora en un DateTimeOffset
            if (!DateTime.TryParseExact($"{dateString} {timeString}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime startTimeDateTime))
            {
                return "Error: Format de data o hora invàlid. Utilitza YYYY-MM-DD i HH:mm.";
            }

            // Assumim que la data i hora d'entrada estan en la zona horària local o desitjada.
            // Si necessites una zona horària específica, hauries d'afegir-la o obtenir-la de la configuració.
            // Per ara, usem DateTimeOffset.SpecifyKind per assegurar que no és Unspecified i després to local.
            // O simplement tractar-ho com a DateTimeOffset local directament.
            DateTimeOffset startTime = new DateTimeOffset(startTimeDateTime, DateTimeOffset.Now.Offset); // Prenem el desplaçament actual

            // La durada de l'esdeveniment és sempre d'1 hora
            DateTimeOffset endTime = startTime.AddHours(1);

            // Comprovar disponibilitat
            bool isAvailable = await _calendari.CheckAvailability(startTime, endTime);

            if (isAvailable)
            {
                // Crear esdeveniment
                Event createdEvent = await _calendari.CreateEvent(summary, startTime, endTime);
                Console.WriteLine($"Esdeveniment creat: {createdEvent.Summary} a {createdEvent.Start.DateTimeDateTimeOffset?.ToString("g")}");
                return $"Esdeveniment '{createdEvent.Summary}' creat amb èxit per al {startTime.ToString("g")}.";
            }
            else
            {
                Console.WriteLine($"Conflicte d'horari: Ja hi ha un esdeveniment per al {startTime.ToString("g")}.");
                return $"Conflicte d'horari: Ja hi ha un esdeveniment per al {startTime.ToString("g")}. No s'ha pogut crear l'esdeveniment '{summary}'.";
            }
        }

        /// <summary>
        /// Obté i mostra esdeveniments del calendari entre dues dates especificades.
        /// </summary>
        /// <param name="startDateString">La data d'inici en format 'YYYY-MM-DD'.</param>
        /// <param name="endDateString">La data de fi en format 'YYYY-MM-DD'.</param>
        /// <returns>Una cadena amb la llista dels esdeveniments trobats, o un missatge si no n'hi ha.</returns>
        [KernelFunction, Description("Obté esdeveniments del calendari entre dues dates (YYYY-MM-DD).")]
        public async Task<string> ObtenirEventsEntreDates(
            [Description("La data d'inici de la cerca en format 'YYYY-MM-DD'.")] string startDateString,
            [Description("La data de fi de la cerca en format 'YYYY-MM-DD'.")] string endDateString)
        {
            Console.WriteLine($"Crida a la funció: GetEventsBetweenDates amb Data Inici: '{startDateString}', Data Fi: '{endDateString}'");

            if (!DateTime.TryParseExact(startDateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime startDate) ||
                !DateTime.TryParseExact(endDateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime endDate))
            {
                return "Error: Format de data invàlid. Utilitza YYYY-MM-DD.";
            }

            // Per assegurar que la endDate inclou tot el dia, l'avancem un dia menys 1 mil·lisegon o similar
            // Si no, endDate a les 00:00:00 no inclouria cap esdeveniment d'aquell dia.
            // O, més simple, fem la crida a GetEventsBetweenDates amb la data final + 1 dia i luego filtrem.
            // L'API de Google Calendar ja interpreta TimeMax com a exclusiu si és una data sense hora específica.
            // Així que si volem incloure tot el dia, hauria de ser l'endemà a les 00:00.
            DateTimeOffset startDateTimeOffset = new DateTimeOffset(startDate, DateTimeOffset.Now.Offset);
            DateTimeOffset endDateTimeOffset = new DateTimeOffset(endDate.AddDays(1), DateTimeOffset.Now.Offset); // Incloure fins al final del dia.

            var events = await _calendari.ObtenirEventsEntreDates(startDateTimeOffset, endDateTimeOffset);

            if (events.Count == 0)
            {
                return $"No s'han trobat esdeveniments entre el {startDateString} i el {endDateString}.";
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Esdeveniments entre el {startDateString} i el {endDateString}:");
                foreach (var eventItem in events)
                {
                    string when = eventItem.Start.DateTime.HasValue ? eventItem.Start.DateTime.Value.ToString("g") : eventItem.Start.Date;
                    sb.AppendLine($"- {eventItem.Summary} ({when})");
                }
                return sb.ToString();
            }
        }

        // ... (resta del codi existent) ...
    }
}