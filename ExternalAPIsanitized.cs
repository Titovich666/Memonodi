using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Script.Serialization;
using Uzelkinapamyat.Models;

namespace Uzelkinapamyat
{
    public class ExternalAPI
    {
        public static string TranslateText(string englishText, string sourceLanguageCode, string targetLanguageCode)
        {
            // Обеззараживание всяких попыток инъекций
            englishText = Utils.Str2SQLTrans(englishText);
            // Получение или обновление (если срок действия истек) Яндекс.Токена (YandexSpeechToken)
            string iamToken = GetIAM();
            if (iamToken == null)
            {
                return "I couldn't get translator attributes...";
            }
            //Подключаюсь к каталогу облака
            string folderId = = WebConfigurationManager.AppSettings["yandexFolderId"];
            // Взаимодействие с Яндекс API происходит в соответствии с архитектурным стилем REST (Representational State Transfer)
            // Документацию можно почитать здесь https://cloud.yandex.ru/docs/translate/api-ref/Translation/translate
            string jurl = "https://translate.api.cloud.yandex.net/translate/v2/translate";
            HttpClient hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Authorization", "Bearer " + iamToken);
            string json = "{\"folder_id\": \"" + folderId + "\", \"texts\": [\"" + englishText + "\"], \"targetLanguageCode\": \"" + targetLanguageCode + "\", \"sourceLanguageCode\": \"" + sourceLanguageCode + "\"}";
            HttpRequestMessage hrm = new HttpRequestMessage(HttpMethod.Post, jurl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            //и отправляю запрос
            HttpResponseMessage response = hc.SendAsync(hrm).Result;
            // проверяю статус ответа
            int status = (int)response.StatusCode;
            if (status != 200)
            {
                return "I couldn't translate your phrase...";
            }
            // если все в порядке - возвращаю перевод
            byte[] bresponse = response.Content.ReadAsByteArrayAsync().Result;
            string jsonResponse = Encoding.UTF8.GetString(bresponse);
            JavaScriptSerializer jss = new JavaScriptSerializer();
            // Структура ответов определена в разделе Models
            YTranslate resp = jss.Deserialize<YTranslate>(jsonResponse);
            string retval = resp.translations[0].text;
            return retval;
        }
		// обновление токена
        public static string GetIAM()
        {
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            string iam = "no";
            string rootPath = HttpRuntime.AppDomainAppPath;
            string mediaFolder = WebConfigurationManager.AppSettings["mediaFolder"];
            mediaFolder = rootPath + mediaFolder;
            string fileName = "Iam.json";
            string jsonResponse;
            JavaScriptSerializer jss = new JavaScriptSerializer();
            YandexSpeechToken resp;
            if (System.IO.File.Exists(mediaFolder + fileName))
            {
                jsonResponse = System.IO.File.ReadAllText(mediaFolder + fileName, Encoding.UTF8);
                resp = jss.Deserialize<YandexSpeechToken>(jsonResponse);
                if (resp.expiresAt != null)
                {
                    resp.expiresAt = resp.expiresAt.Replace("T", " ");
                    resp.expiresAt = resp.expiresAt.Substring(0, 16);
                    DateTime expAt = DateTime.ParseExact(resp.expiresAt, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                    double hours = (DateTime.Now - expAt).TotalHours;
                    if (hours < 0)
                    {
                        iam = resp.iamToken;
                        return iam;
                    }
                }
            }
            string strURL = "https://iam.api.cloud.yandex.net/iam/v1/tokens";
            string yandexPassportOauthToken = WebConfigurationManager.AppSettings["yandexPassportOauthToken"];
            string json = "{'yandexPassportOauthToken': '" + yandexPassportOauthToken + "'}";
            HttpClient hc = new HttpClient
            {
                BaseAddress = new Uri(strURL)
            };
            hc.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpRequestMessage hrm = new HttpRequestMessage(HttpMethod.Post, strURL)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            HttpResponseMessage response = hc.SendAsync(hrm).Result;

            byte[] result = response.Content.ReadAsByteArrayAsync().Result;

            jsonResponse = Encoding.UTF8.GetString(result);
            System.IO.File.WriteAllText(mediaFolder + fileName, jsonResponse);
            resp = jss.Deserialize<YandexSpeechToken>(jsonResponse);
            iam = resp.iamToken;
            return iam;
        }
		
		// создание (синтез) звукового файла из английской фразы
        public static string CreateYandexMedia(string Filename, string Text4Speech)
        {
            string mediaFolder = HttpRuntime.AppDomainAppPath + WebConfigurationManager.AppSettings["mediaFolder"];
            if (System.IO.File.Exists(mediaFolder + Filename))
                return Filename;
            string iamToken = GetIAM();
            string folderId = WebConfigurationManager.AppSettings["yandexFolderId"];

            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            HttpClient hc = new HttpClient();
            Text4Speech = Utils.Str4Text2Speech(Text4Speech);

            hc.DefaultRequestHeaders.Add("Authorization", "Bearer " + iamToken);
            Dictionary<string, string> values = new Dictionary<string, string>{
                { "text", Text4Speech },
                { "lang", "en-US" },
                { "speed", "0.9" },
                { "voice", "ermil" },
                { "emotion", "good" },
                { "folderId", folderId }
              };
            var content = new FormUrlEncodedContent(values);
            var response = hc.PostAsync("https://tts.api.cloud.yandex.net/speech/v1/tts:synthesize", content).Result;
            var responseBytes = response.Content.ReadAsByteArrayAsync().Result;
            if (responseBytes[0] == 123)
                Filename = "Error.aac";
            else
                System.IO.File.WriteAllBytes(mediaFolder + Filename, responseBytes);
            return Filename;
        }

        public static string TranslateText(string englishText)
        {
            return TranslateText(englishText, "en", "ru");
        }
        public static string TranslateTextRuEn(string rusText)
        {
            return TranslateText(rusText, "ru", "en");
        }
		
		// создание (синтез) звукового файла из английской фразы у гугла получается лучше, чем у яндекса
        public static string CreateGoogleMedia(string Filename, string Text4Speech)
        {
            // создает звуковой файл по тексту, если такого файла еще нет
            string rootPath = HttpRuntime.AppDomainAppPath;
            string mediaFolder = rootPath + WebConfigurationManager.AppSettings["mediaFolder"];
            if (System.IO.File.Exists(mediaFolder + Filename))
                return Filename;
            HttpClient hc = new HttpClient();

            hc.DefaultRequestHeaders.Add("X-Goog-Api-Key", WebConfigurationManager.AppSettings["GoogleAPIkey"];);

            string json = "{\r\n  \"input\":{\r\n    \"text\":\"@Text4Speech\"\r\n  },\r\n" +
                "\"voice\":{\r\n    \"languageCode\":\"en-US\",\r\n    \"name\":\"en-US-Wavenet-D\",\r\n    \"ssmlGender\":\"FEMALE\"\r\n  },\r\n" +
                "\"audioConfig\":{\r\n    \"audioEncoding\":\"OGG_OPUS\", \"pitch\": 0, \"speakingRate\": 1, \"effectsProfileId\": [\"headphone-class-device\"],  \r\n  }\r\n}";
            json = json.Replace("@Text4Speech", Utils.Str4Text2Speech(Text4Speech));
            var stringContent = new StringContent(json);
            HttpResponseMessage response = null;
            try
            {
                response = hc.PostAsync("https://texttospeech.googleapis.com/v1/text:synthesize", stringContent).Result;
            }
            catch (WebException wex)
            {
                Utils.ErrorLog($"From CreateGoogleMedia: {wex.Message}");
            }
            var contents = response.Content.ReadAsStringAsync().Result;
            JavaScriptSerializer jss = new JavaScriptSerializer();
            GoogleT2SAudio gaudio = jss.Deserialize<GoogleT2SAudio>(contents);
            var responseBytes = Convert.FromBase64String(gaudio.audioContent); // System.Text.Encoding.ASCII.GetBytes(gaudio.audioContent);
            System.IO.File.WriteAllBytes(mediaFolder + Filename, responseBytes);
            return Filename;
        }
		
		// чтобы не писать свое мобильное приложение, воспользуюсь google календарем
        public static string CreateGoogleCalendarTask(string taskName, string taskPlanDoneDate, string accessToken)
        {

            // создает задачу в календаре

            // Сначала получаю id списка задач
            string endpoint = "https://tasks.googleapis.com/tasks/v1/users/@me/lists"; // какие списки задач у меня есть
            string responseString = SendTokenViaBearer(accessToken, endpoint);
            JObject json = JObject.Parse(responseString);
            if (json.ContainsKey("error"))
            {
                Utils.ErrorLog($"Error while triyng to get id of task list {responseString}");
                return "error";
            }
            string taskList = (string)json["items"][0]["id"];
            // Utils.DebugLog($"Got id of task list {taskList}");


            var url = $"https://tasks.googleapis.com/tasks/v1/lists/{taskList}/tasks";
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            GoogleTask data = new GoogleTask()
            {
                title = taskName,
                due = taskPlanDoneDate + "T13:00:00.000Z"
            };

            StringContent content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");

            HttpResponseMessage response = client.PostAsync(url, content).Result;
            string responseContent = response.Content.ReadAsStringAsync().Result;
            return responseContent;
        }
		
        private static string SendTokenViaBearer(string token, string mailerURL)
        {
            string responseString = "";
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                HttpResponseMessage response = client.GetAsync(mailerURL).Result;
                responseString = response.Content.ReadAsStringAsync().Result;
            }
            return responseString;
        }

        internal static (string, string) GetGoogleToken(string googleCode)
        {
            // The code parameter obtained from the Google Authorization Server == googleCode
            // The client ID and secret obtained from the Google API Console
            string googleClientId = WebConfigurationManager.AppSettings["googleClientId"];
            string googleClientSecret = WebConfigurationManager.AppSettings["googleClientSecret"];

            // The URI of the Google OAuth 2.0 token endpoint
            string uri = "https://oauth2.googleapis.com/token";

            // The redirect URI registered with the Google API Console
            string redirectUri = "https://mysite.ru/authorize";

            // Create an HTTP client
            var httpClient = new HttpClient();

            // Create a dictionary of key-value pairs for the request body
            var data = new Dictionary<string, string>
            {
                { "client_id", googleClientId },
                { "client_secret", googleClientSecret },
                { "grant_type", "authorization_code" },
                { "redirect_uri", redirectUri },
                { "code", googleCode }
            };

            // Create a URL-encoded content from the data
            var content = new FormUrlEncodedContent(data);

            // Send a POST request to the token endpoint
            var response = httpClient.PostAsync(uri, content).Result;

            // Check if the request was successful
            if (response.IsSuccessStatusCode)
            {
                // Read the response content as a string
                var responseContent = response.Content.ReadAsStringAsync().Result;

                // Parse the response content as a JSON object
                JObject json = JObject.Parse(responseContent);

                // Extract the access token and the refresh token from the JSON object
                var accessToken = json["access_token"].ToString();
                string refreshToken = string.Empty;
                if (json.ContainsKey("refresh_token"))
                {
                    refreshToken = json["refresh_token"].ToString();
                }
                // Utils.DebugLog($"Successfully refreshed tokens, access token: {accessToken}, refresh token: {refreshToken}");
                return (accessToken, refreshToken);

            }
            else
            {
                // Print the status code and the reason phrase to the console
                string responseString = response.Content.ReadAsStringAsync().Result;
                Utils.ErrorLog($"The problem while refreshing tokens: {responseString}");
                return (response.StatusCode.ToString(), responseString);
            }
        }
    }
}