using SemanticKernel_Agenda.GEMINI_C;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticKernel_Agenda.PLUGINS
{
    public class ChatPlugins
    {
        private readonly GeminiChatService _geminiChatService;
        public ChatPlugins(GeminiChatService geminiChatService)
        {
            _geminiChatService = geminiChatService;
        }

    }
}
