# SemanticKernel_Agenda

Aquest projecte demostra una aplicació .NET que integra el **Semantic Kernel** amb el model de llenguatge **Google Gemini** i l'**API de Google Calendar** per crear un assistent d'agenda intel·ligent capaç de comprendre i respondre a sol·licituds de l'usuari per gestionar esdeveniments al calendari.

---

## 1. Visió General del Projecte

`SemanticKernel_Agenda` és una aplicació de consola que actua com un assistent personal. Utilitza la potència dels Large Language Models (LLMs) per interpretar les intencions de l'usuari en llenguatge natural i interactuar amb un calendari de Google.

Les funcionalitats principals inclouen:
*   **Interacció amb un LLM (Google Gemini):** Processa preguntes i peticions de l'usuari.
*   **Function Calling / Tool Use:** El LLM és capaç de "decidir" quan i quina funció ha de cridar (en el nostre cas, funcions de calendari) per complir la petició de l'usuari.
*   **Gestió d'Esdeveniments al Calendari:** Comprova la disponibilitat en franges horàries i crea noves cites o esdeveniments al Google Calendar.

---

## 2. Parts Crítiques i Arquitectura

L'arquitectura del projecte es basa en la modularitat i l'ús de la Injecció de Dependències (DI) per una millor organització, testabilitat i manteniment.

### Mòduls Clau:

*   **`Program.cs`:** El punt d'entrada i orquestrador de la configuració inicial i la injecció de dependències.
*   **`GEMINI_C/GeminiChatService.cs`:** Encapsula la lògica d'interacció amb el model Gemini i la gestió de la conversa.
*   **`CALENDAR/Calendari.cs`:** Conté la lògica de baix nivell per interactuar directament amb l'API de Google Calendar.
*   **`Plugins/CalendarPlugin.cs`:** Actua com un "pont" entre Semantic Kernel i la lògica de `Calendari`, exposant les funcionalitats del calendari com a "Kernel Functions" (eines) per al LLM.
*   **`configs.json`:** Fitxer de configuració per a credencials i altres paràmetres, que es manté fora del control de versions (`.gitignore`).

---

## 3. `CalendarPlugin` vs. `Calendari`: La Separació de Responsabilitats

Una de les pedres angulars d'aquest projecte és la clara separació de responsabilitats entre les classes `Calendari.cs` i `CalendarPlugin.cs`. Aquesta distinció és fonamental per a la modularitat, testabilitat i manteniment del codi, i és clau per entendre com Semantic Kernel integra les "eines" amb els models de llenguatge.

### L'Analogia del Restaurant 🍽️

Per entendre-ho millor, imaginem un restaurant:

*   **El Director d'Orquestra (Kernel de Semantic Kernel):** És qui coordina tot i parla amb els cambrers i els cuiners.
*   **El Cambrer Intel·ligent (Google Gemini, a través del Kernel):** És el qui interactua amb el client (l'usuari), entén la seva comanda en llenguatge natural i decideix quins plats oferir o quines recomanacions fer.
*   **El Menú del Restaurant (`Plugins/CalendarPlugin.cs`):** Aquesta és la interfície que el cambrer (Gemini) utilitza per saber quins "plats" (funcions) es poden oferir als clients (usuaris). Descriu els plats de manera que el cambrer i el client els puguin entendre, amb un nom clar i una descripció (`[KernelFunction]` i `[Description]`).
*   **La Cuina amb les Receptes (`CALENDAR/Calendari.cs`):** Aquesta és la implementació tècnica i detallada de com es preparen els plats. Conté les "receptes" (mètodes) exactes per fer cada cosa (parlar amb l'API de Google, gestionar l'autenticació, etc.). El cambrer i el client no veuen la cuina ni les receptes.

### Funcions i Responsabilitats Específiques:

#### 3.1. `CALENDAR/Calendari.cs` (La Cuina / L'Expert Tècnic)

*   **Responsabilitat Principal:** La seva única responsabilitat és saber *com* interactuar amb l'API de Google Calendar a un nivell baix.
*   **Què fa:**
    *   Gestiona l'**autenticació OAuth2** amb Google.
    *   Construeix les **peticions HTTP** a l'API de Google Calendar.
    *   Maneja les respostes tècniques de l'API i els objectes específics de Google (com `Event`, `EventDateTime`).
    *   Implementa les operacions de calendari pures: `ObtenirUltimsEvents()`, `CheckAvailability(startTime, endTime)`, `CreateEvent(summary, startTime, endTime)`, `ObtenirEventsEntreDates(startDate, endDate)`.
*   **Per què és important:** És la capa de servei **purament tècnica**. És agnòstica a la IA o a Semantic Kernel. Si Google canvia la seva API de Calendar, només hauríies de modificar aquesta classe. Permet la reutilització de la lògica de calendari en altres parts de l'aplicació que no necessiten IA.

#### 3.2. `Plugins/CalendarPlugin.cs` (El Menú del Restaurant / L'Adaptador per a l'IA)

*   **Responsabilitat Principal:** Actuar com la interfície que **presenta** les funcionalitats del calendari a Semantic Kernel (i per extensió, a Google Gemini) de manera que l'IA les pugui entendre i invocar.
*   **Què fa:**
    *   Conté mètodes públics marcats amb `[KernelFunction]` i `[Description]`, com `CreateCalendarEvent` i `GetEventsBetweenDates`. Aquests atributs són el que el Kernel utilitza per construir el "catàleg d'eines" per a Gemini.
    *   **Tradueix i valida els paràmetres:** Rep els arguments que el LLM li passa (que són cadenes de text interpretades per l'IA) i els converteix als tipus de dades (`DateTimeOffset`, etc.) que `Calendari.cs` espera.
    *   **Delega la tasca:** Un cop els paràmetres estan preparats, **crida els mètodes corresponents de la instància de `Calendari`** que se li ha injectat al constructor.
*   **Per què és important:** Aquesta classe és l'adaptador. El model Gemini (el cambrer) només "veu" el menú (`CalendarPlugin`) i les seves descripcions. No sap ni li importa com la cuina (`Calendari`) prepara els plats. Si el LLM necessita una funció, la demana al `CalendarPlugin`, i aquest últim s'encarrega d'orquestrar l'execució amb la lògica tècnica de `Calendari`.

### Flux d'Execució amb la Separació:

1.  **Usuari:** "Genera una cita per demà a les 10:00 per anar al dentista."
2.  **`GeminiChatService`:** Envia la petició i el "catàleg d'eines" del `CalendarPlugin` al model Gemini.
3.  **Gemini (LLM):** Analitza la petició i el "menú" del `CalendarPlugin`. Decideix que la funció `CreateCalendarEvent` és l'adequada i extreu els paràmetres (`summary="anar al dentista"`, `dateString="[data de demà]"`, `timeString="10:00"`).
4.  **Semantic Kernel:** Intercepta la intenció de Gemini de cridar la funció i **invoca el mètode C# `CalendarPlugin.CreateCalendarEvent()`**.
5.  **`CalendarPlugin.CreateCalendarEvent()`:**
    *   Rep els arguments de Gemini.
    *   Converteix `dateString` i `timeString` a `DateTimeOffset`.
    *   **Crida a `_calendari.CheckAvailability()` i `_calendari.CreateEvent()`** per realitzar les operacions reals amb Google Calendar.
6.  **`Calendari.CheckAvailability()` / `Calendari.CreateEvent()`:** Executa les operacions directes amb l'API de Google Calendar.
7.  **Resultat:** L'operació de `Calendari` retorna el seu resultat a `CalendarPlugin`, que al seu torn el retorna a Semantic Kernel. Semantic Kernel llavors presenta aquest resultat a Gemini per generar la resposta final per a l'usuari.

Aquesta arquitectura garanteix una aplicació clara, mantenible, extensible i que aprofita al màxim les capacitats del "Function Calling" de Semantic Kernel.

---

## 4. Rol de `GEMINI_C/GeminiChatService.cs`

Aquesta classe és la responsable de tota la interacció amb el model Gemini:

*   **Gestió de la Conversa:** Manté un `ChatHistory` per assegurar que Gemini recordi el context de les interaccions prèvies de l'usuari.
*   **Bucle Interactiu:** Conté la lògica principal del bucle `while (true)` que pren l'entrada de l'usuari, l'envia a Gemini i mostra la resposta.
*   **Configuració de "Tool Calling":** Estableix els `GeminiPromptExecutionSettings` amb `ToolCallBehavior = GeminiToolCallBehavior.AutoInvokeKernelFunctions`. Aquesta configuració és crucial, ja que li diu a Semantic Kernel que, si Gemini suggereix una crida a una "Kernel Function" (eina), l'ha d'executar automàticament i injectar el resultat de nou a la conversa.
*   **System Prompt:** Al constructor, s'inicialitza `ChatHistory` amb un `System Prompt` que instruix Gemini sobre el seu rol d'assistent d'agenda i les capacitats del `CalendarPlugin`, millorant la seva capacitat de cridar les funcions correctament.
*   **Dependències:** Injecta el `Kernel` i en resol l'`IChatCompletionService` per interactuar amb Gemini.

---

## 5. Funcionament de la Injecció de Dependències (DI) a `Program.cs`

`Program.cs` utilitza el patró d'Injecció de Dependències (DI) proporcionat per `Microsoft.Extensions.DependencyInjection`, que és comú en aplicacions .NET modernes.

*   **`ServiceCollection`:** És un contenidor on es registren els serveis. Cada `services.AddSingleton<T>()` o `services.AddSingleton<T>(sp => { /* ... */ })` indica que quan alguna classe necessiti una instància de `T`, el contenidor haurà de proporcionar una única instància (singleton) d'aquesta classe durant la vida de l'aplicació.
*   **Resolució de Dependències:**
    *   Quan registrem `services.AddSingleton<Calendari>()`, el DI sap com crear una instància de `Calendari` perquè el seu constructor necessita `IConfiguration`, que ja hem registrat com a singleton.
    *   El mateix passa amb `services.AddSingleton<CalendarPlugin>()`, el seu constructor necessita `Calendari`, que el DI ja pot proporcionar.
    *   Quan finalment fem `var serviceProvider = services.BuildServiceProvider();`, es crea un objecte capaç de resoldre totes aquestes dependències.
    *   Després, quan fem `serviceProvider.GetRequiredService<Kernel>()`, el `serviceProvider` sap exactament com construir el `Kernel` (passant-li la `GeminiApiKey` obtinguda de `IConfiguration`) i totes les seves dependències.
*   **`kernel.ImportPluginFromObject(...)`:** Aquesta línia és on el `CalendarPlugin` (que ja ha estat resolt pel DI amb la seva dependència `Calendari`) s'injecta en el `Kernel` ja existent, fent les seves funcions accessibles al LLM.

La DI fa el codi més modular, més fàcil de testar (podem substituir implementacions per mocks) i més escalable.

---

## 6. Classes Finalment Utilitzades

Les classes principals que componen aquest projecte són:

*   `Program.cs`: Punt d'entrada i configuració del DI.
*   `GEMINI_C/GeminiChatService.cs`: Lògica d'interacció amb Gemini i bucle de xat.
*   `CALENDAR/Calendari.cs`: Interacció de baix nivell amb Google Calendar API.
*   `Plugins/CalendarPlugin.cs`: Wrapper de `Calendari` per a Semantic Kernel, exposant les `KernelFunctions`.

---

## 7. `KernelFunction` en Semantic Kernel vs. "Tools" en LangChain

La teva observació és molt pertinent! Existeixen conceptes equivalents en diferents frameworks de LLM:

*   **`[KernelFunction]` (Semantic Kernel):**
    *   És un atribut C# que marques en un mètode d'una classe.
    *   Indica a Semantic Kernel que aquest mètode ha de ser exposat com una "eina" o "capacitat" que el LLM pot utilitzar.
    *   S'acompanya de l'atribut `[Description("...")]` per proporcionar al LLM una explicació en llenguatge natural de què fa la funció i quins són els seus paràmetres.
    *   El Kernel construeix un "catàleg" d'aquestes funcions i el presenta al LLM. Si el LLM decideix invocar una funció, Semantic Kernel s'encarrega d'executar el mètode C# corresponent.

*   **"Tools" (LangChain):**
    *   En LangChain, les "Tools" són abstraccions per a accions que un agent (LLM) pot realitzar.
    *   Són objectes que tenen un nom, una descripció i una lògica d'execució.
    *   L'agent rep una llista de les eines disponibles i decideix quina utilitzar basant-se en la seva descripció.

**Similituds:**
*   Ambdós conceptes serveixen per dotar els LLMs de la capacitat d'interactuar amb el món exterior, anar més enllà de la generació de text i realitzar accions concretes (com consultar un calendari, buscar informació, enviar un correu, etc.).
*   Tots dos es basen en una descripció en llenguatge natural per permetre al LLM decidir quan i com invocar-los.
*   Permeten als LLMs actuar com a "agents" intel·ligents.

**Diferències (més aviat d'implementació):**
*   **Implementació:** `[KernelFunction]` és un atribut de C#/.NET, mentre que les "Tools" de LangChain s'implementen normalment com a classes o funcions Python que segueixen una interfície específica.
*   **Ecosistema:** Cadascun està integrat profundament en el seu propi ecosistema de framework (Semantic Kernel per a .NET/Python/Java, LangChain principalment per a Python/JavaScript).

---

## 8. Estructura del fitxer `configs.json`

Aquest fitxer conté les credencials sensibles i configuracions que no s'han de versionar (`.gitignore`). Aquí teniu l'estructura esperada:

```json
{
  // Clau API per al model de Google Gemini.
  // Aconseguiu-la des de Google AI Studio o la Google Cloud Console.
  "GeminiApiKey": "EL_TEU_GEMINI_API_KEY_AQUI",

  // Configuració per a la integració amb Google Calendar.
  "Calendar": {
    // Zona horària per defecte per a la creació d'esdeveniments.
    "DefaultTimeZone": "Europe/Madrid",
    // ID del calendari a utilitzar (normalment "primary" per al calendari principal de l'usuari).
    "CalendarId": "primary",
    // Client ID per a l'autenticació OAuth 2.0 de Google.
    // Aconseguiu-lo de la Google Cloud Console (creant credencials de "aplicación de escritorio").
    "ClientId": "EL_TEU_CALENDAR_CLIENT_ID_AQUI",
    // Client Secret per a l'autenticació OAuth 2.0 de Google.
    "ClientSecret": "EL_TEU_CALENDAR_CLIENT_SECRET_AQUI",
    // Carpeta (o fitxer) on s'emmagatzemarà el token d'autenticació de Google.
    // Si no s'especifica una ruta, es crearà a la carpeta d'execució de l'aplicació.
    "TokenFolder": "token.json"
  },

  // Configuració general de l'aplicació.
  "AppSettings": {
    "LogFilePath": "/var/log/calendar_app.log",
    "MaxReservationDurationMinutes": 120
  },

  // Patrons per a fitxers i directoris a excloure, típic en configuracions d'IDE o projecte.
  "exclude": [
    "**/bin",
    "**/bower_components",
    "**/jspm_packages",
    "**/node_modules",
    "**/obj",
    "**/platforms"
  ]
}
```

## Configuració i Execució

### Obtenir Credencials
- **Gemini API Key**: Visita **Google AI Studio** per generar la teva clau API de Gemini.  
- **Google Calendar API Credentials**:  
  1. Vés a la **Google Cloud Console**.  
  2. Crea un nou projecte si no en tens un.  
  3. Habilita l'API de **Google Calendar**.  
  4. Vés a **Credencials** i crea unes noves credencials de **ID de cliente de OAuth**.  
     - Selecciona *Aplicación de escritorio*.  
  5. Descarrega el **JSON** amb el teu `client_id` i `client_secret`.

### Actualitzar `configs.json`
- Copia les teves credencials al fitxer `configs.json` segons l'estructura proporcionada.

### Instal·lar Paquets NuGet
- Assegura't que els paquets següents estan actualitzats a les darreres versions **pre-release** per garantir la compatibilitat de les API:  
  ```bash
  dotnet add package Microsoft.SemanticKernel --prerelease
  dotnet add package Microsoft.SemanticKernel.Connectors.Google --prerelease
