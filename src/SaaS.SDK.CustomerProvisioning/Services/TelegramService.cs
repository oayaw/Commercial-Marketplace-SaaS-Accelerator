using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace SaaS.SDK.CustomerProvisioning.Services
{
    public static class TelegramService
    {
        static readonly string token = "1979617440:AAE_q-2irwpi5IdoKIersDaruxOa81uIkFk";
        static readonly string chatId = "65294787";

        public static string SendMessage(string message)
        {
            string retval = string.Empty;
            string url = $"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text={message}";

            using (var webClient = new WebClient())
            {
                retval = webClient.DownloadString(url);
            }

            return retval;
        }
    }
}
