using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using System.Net;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows.Forms;
using Menu = LeagueSharp.Common.Menu;
using MenuItem = LeagueSharp.Common.MenuItem;


namespace ChatTranslator
{
    [PermissionSet(SecurityAction.Assert, Unrestricted = true)]
    internal class Program
    {
        public static Menu Config;

        public static String[] fromArray = new String[]
        {
            "auto", "af", "sq", "ar", "hy", "az", "eu", "be", "bs", "bg", "ca", "zh", "hr", "cs", "da", "nl", "en", "et",
            "fi", "fr", "gl", "ka", "de", "el", "ht", "hu", "is", "id", "ga", "it", "ja", "kk", "ko", "ky", "la", "lv",
            "lt", "mk", "mg", "ms", "mt", "mn", "no", "fa", "pl", "pt", "ro", "ru", "sr", "sk", "sl", "es", "sw", "sv",
            "tl", "tg", "tt", "th", "tr", "uk", "uz", "vi", "cy", "he"
        };

        public static String[] fromArrayMenu = new String[]
        {
            "auto", "Afrikaans", "Albanian", "Arabic", "Armenian", "Azerbaijan", "Basque", "Belarusian", "Bosnian",
            "Bulgarian", "Catalan", "Chinese", "Croatian", "Czech", "Danish", "Dutch", "English", "Estonian", "Finish",
            "French", "Galician", "Georgian", "German", "Greek", "Haitian", "Hungarian", "Icelandic", "Indonesian",
            "Irish", "Italian", "Japanese", "Kazakh", "Korean", "Kyrgyz", "Latin", "Latvian", "Lithuanian", "Macedonian",
            "Malagasy", "Malay", "Maltese", "Mongolian", "Norwegian", "Persian", "Polish", "Portuguese", "Romanian",
            "Russian", "Serbian", "Slovakian", "Slovenian", "Spanish", "Swahili", "Swedish", "Tagalog", "Tajik", "Tatar",
            "Thai", "Turkish", "Ukrainian", "Uzbek", "Vietnamese", "Welsh", "Yiddish"
        };

        public static String[] toArray = new String[]
        {
            "af", "sq", "ar", "hy", "az", "eu", "be", "bs", "bg", "ca", "zh", "hr", "cs", "da", "nl", "en", "et", "fi",
            "fr", "gl", "ka", "de", "el", "ht", "hu", "is", "id", "ga", "it", "ja", "kk", "ko", "ky", "la", "lv", "lt",
            "mk", "mg", "ms", "mt", "mn", "no", "fa", "pl", "pt", "ro", "ru", "sr", "sk", "sl", "es", "sw", "sv", "tl",
            "tg", "tt", "th", "tr", "uk", "uz", "vi", "cy", "he"
        };

        public static String[] toArrayMenu = new String[]
        {
            "Afrikaans", "Albanian", "Arabic", "Armenian", "Azerbaijan", "Basque", "Belarusian", "Bosnian", "Bulgarian",
            "Catalan", "Chinese", "Croatian", "Czech", "Danish", "Dutch", "English", "Estonian", "Finish", "French",
            "Galician", "Georgian", "German", "Greek", "Haitian", "Hungarian", "Icelandic", "Indonesian", "Irish",
            "Italian", "Japanese", "Kazakh", "Korean", "Kyrgyz", "Latin", "Latvian", "Lithuanian", "Macedonian",
            "Malagasy", "Malay", "Maltese", "Mongolian", "Norwegian", "Persian", "Polish", "Portuguese", "Romanian",
            "Russian", "Serbian", "Slovakian", "Slovenian", "Spanish", "Swahili", "Swedish", "Tagalog", "Tajik", "Tatar",
            "Thai", "Turkish", "Ukrainian", "Uzbek", "Vietnamese", "Welsh", "Yiddish"
        };

        public static String[] SpecChars = new String[] { "bg", "zh-CN", "zh-TW", "ru", "ko", "uk" };
        public static ObservableCollection<Message> lastMessages = new ObservableCollection<Message>();

        public const string yandexUrl = "https://translate.yandex.net/api/v1.5/tr.json/translate";

        public static List<string> yandexApiKeys =
            new List<string>(
                new string[]
                {
                    "?key=trnsl.1.1.20151027T151706Z.16f6a75f2f1b2aa4.d670690d98ed95429c11e25d871b7c2e05e81cbb",
                    "?key=trnsl.1.1.20160104T204216Z.fafe170e32096852.9e3884fe5a4c00881dbf6781534fee74664c68ec",
                    "?key=trnsl.1.1.20160104T204235Z.bbf140ab21cf34a8.7b5b3ff936297932f10250966e112f963370e675",
                    "?key=trnsl.1.1.20160104T204435Z.4ee06ec4b7bf42ed.a6408bbb10e00b0480d516d7c8b2368d8f369710",
                    "?key=trnsl.1.1.20160104T204502Z.776f8a7e2c4d9d42.b1ee11db59b64410c6402df651cb1e156e0fe1d3",
                    "?key=trnsl.1.1.20160104T204457Z.986a06123cc06620.f114139f32732c33ba127de92e8a14961a080edd",
                    "?key=trnsl.1.1.20160104T204437Z.d766324c28c39ddb.25569303b37b7132212831048bbc0db2476eebbb",
                });

        public static string yandexApiKey;
        public static bool ShowMessages, sent, copied, translate;
        public static string path, fileName, clipBoard, lastInput;
        public static List<string> clipBoardLines;
        public static float gamestart;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            CreateMenu();

            Game.PrintChat("<font color='#9933FF'>Soresu </font><font color='#FFFFFF'>- ChatTranslator</font>");
            Game.OnUpdate += Game_OnUpdate;
            Game.OnInput += Game_GameInput;
            Game.OnChat += Game_OnChat;
            Drawing.OnDraw += Drawing_OnDraw;
            lastMessages.CollectionChanged += OnMessage;

            path = string.Format(
                @"{0}\ChatLogs\{1}\{2}\{3}\{4}\", LeagueSharp.Common.Config.AppDataDirectory,
                DateTime.Now.ToString("yyyy"), DateTime.Now.ToString("MMMM"), DateTime.Now.ToString("dd"),
                Utility.Map.GetMap().ShortName);
            fileName = ObjectManager.Player.SkinName + "_" + Game.Id + ".txt";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            SetApiKey();

            if (Config.Item("EnabledLog").GetValue<bool>())
            {
                InitText();
            }
            lastInput = "null";
        }

        private static void SetApiKey()
        {
            var apiKeyPath = LeagueSharp.Common.Config.AppDataDirectory + @"\yandexApiKey.txt";
            if (!File.Exists(apiKeyPath))
            {
                File.Create(apiKeyPath);
            }

            Utility.DelayAction.Add(
                100, () =>
                {
                    yandexApiKey = File.ReadLines(apiKeyPath).FirstOrDefault();
                    if (yandexApiKey != null && yandexApiKey.Contains("trns"))
                    {
                        yandexApiKey = "?key=" + yandexApiKey.Trim();
                    }
                    else
                    {
                        yandexApiKey = "";
                    }
                    test("hello");
                });
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            if (Config.Item("Check").GetValue<KeyBind>().Active || ShowMessages)
            {
                var posX = Config.Item("Horizontal").GetValue<Slider>().Value;
                var posY = Config.Item("Vertical").GetValue<Slider>().Value;
                var line = 0;
                foreach (var message in lastMessages)
                {
                    Size textSize = TextRenderer.MeasureText(
                        message.sender.Name + ":", new Font(FontFamily.GenericSansSerif, 10));

                    if (!message.sender.IsAlly)
                    {
                        Drawing.DrawText(posX, posY + line, Color.Red, message.sender.Name + ":");
                    }
                    else
                    {
                        if (message.sender.IsMe)
                        {
                            Drawing.DrawText(posX, posY + line, Color.Goldenrod, message.sender.Name + ":");
                        }
                        else
                        {
                            Drawing.DrawText(posX, posY + line, Color.DeepSkyBlue, message.sender.Name + ":");
                        }
                    }
                    Drawing.DrawText(
                        posX + textSize.Width + message.sender.Name.Length, posY + line, Color.White,
                        (message.translated != message.original ? message.original : "") + " " + message.translated);
                    line += 15;
                }
            }
        }

        private static void Game_OnUpdate(EventArgs args)
        {
            if (Config.Item("DisablePaste").GetValue<bool>())
            {
                return;
            }
            if (sent)
            {
                return;
            }
            var delay = Config.Item("Delay").GetValue<Slider>().Value;
            if (Config.Item("Paste").GetValue<KeyBind>().Active)
            {
                SetClipBoardData();
                if (!clipBoard.Contains("\n"))
                {
                    Game.Say(clipBoard);
                    sent = true;
                    Utility.DelayAction.Add(delay, () => sent = false);
                }
                else
                {
                    foreach (string s in clipBoardLines)
                    {
                        Game.Say(s);
                    }
                    sent = true;
                    Utility.DelayAction.Add(delay, () => sent = false);
                    clipBoardLines.Clear();
                }
            }
            if (Config.Item("PasteForAll").GetValue<KeyBind>().Active)
            {
                SetClipBoardData();
                if (!clipBoard.Contains("\n"))
                {
                    Game.Say("/all " + clipBoard);
                    sent = true;
                    Utility.DelayAction.Add(delay, () => sent = false);
                }
                else
                {
                    foreach (string s in clipBoardLines)
                    {
                        Game.Say("/all " + s);
                    }
                    sent = true;
                    Utility.DelayAction.Add(delay, () => sent = false);
                    clipBoardLines.Clear();
                }
            }
        }

        private static void SetClipBoardData()
        {
            if (Clipboard.ContainsText())
            {
                clipBoard = setEncodingDefault(Clipboard.GetText());
                if (clipBoard.Contains("\n"))
                {
                    clipBoardLines = clipBoard.Split('\n').ToList();
                }
            }
        }

        private static void OnMessage(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (Config.Item("AutoShow").GetValue<bool>() && translate)
            {
                ShowMessages = true;
                Utility.DelayAction.Add(Config.Item("Duration").GetValue<Slider>().Value, () => ShowMessages = false);
            }
        }

        private static string setEncodingUTF8(string data)
        {
            byte[] bytes = Encoding.Default.GetBytes(data);
            return Encoding.UTF8.GetString(bytes);
        }

        private static string setEncodingDefault(string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            return Encoding.Default.GetString(bytes);
        }

        private static void InitText()
        {
            if (!File.Exists(path + fileName))
            {
                string initText = "";
                var team = "";
                foreach (var hero in ObjectManager.Get<Obj_AI_Hero>())
                {
                    if (team != hero.Team.ToString())
                    {
                        initText += "\t" + hero.Team.ToString() + "\n";
                        team = hero.Team.ToString();
                    }
                    initText += hero.Name + " (" + hero.ChampionName + ")\n";
                }
                initText += "------------------------\n";
                File.AppendAllText(path + fileName, initText, Encoding.Default);
            }
        }

        private static void AddToLog(string message, Obj_AI_Hero sender)
        {
            if (sender == null || message == null)
            {
                return;
            }
            InitText();
            string line = sender.Name + " (" + sender.ChampionName + ")" + ": " + message + "\n";
            File.AppendAllText(path + fileName, line, Encoding.Default);
        }

        private static async void addMessage(string message, Obj_AI_Hero sender, bool shouldTranslate = true)
        {
            string from = fromArray[Config.Item("From").GetValue<StringList>().SelectedIndex];
            string to = toArray[Config.Item("To").GetValue<StringList>().SelectedIndex];
            string translated = message;
            if (shouldTranslate)
            {
                translated = await TranslateYandex(message, from, to, true);
            }
            if (from != to && !sender.IsMe && translated != message)
            {
                translate = true;
                Utility.DelayAction.Add(500, () => translate = false);
                lastMessages.Add(new Message(translated, sender, message));
                if (Config.Item("ShowInChat").GetValue<bool>())
                {
                    Game.PrintChat(translated);
                }
            }
            else
            {
                var last = new TestPerAll(lastInput);
                if (from != to && sender.IsMe && last.Output != message)
                {
                    lastMessages.Add(
                        new Message(String.Format("({0} => {1}) {2}", from, to, message), sender, last.Output));
                    if (Config.Item("ShowInChat").GetValue<bool>() && translated != message)
                    {
                        Game.PrintChat(translated);
                    }
                }
                else
                {
                    lastMessages.Add(new Message(message, sender, message));
                    if (Config.Item("ShowInChat").GetValue<bool>() && translated != message)
                    {
                        Game.PrintChat(translated);
                    }
                }
            }
            if (lastMessages.Count > 8)
            {
                lastMessages.RemoveAt(0);
            }
        }

        private static void Game_GameInput(GameInputEventArgs args)
        {
            var message = "";
            message += args.Input;
            if (Config.Item("EnabledOut").GetValue<bool>() &&
                toArray[Config.Item("OutFrom").GetValue<StringList>().SelectedIndex] !=
                toArray[Config.Item("OutTo").GetValue<StringList>().SelectedIndex])
            {
                TranslateAndSend(message);
                args.Process = false;
            }
            lastInput = message;
        }

        private static void Game_OnChat(GameChatEventArgs args)
        {
            if (args.Message.Contains("font color"))
            {
                return;
            }
            if (!(args.Sender is Obj_AI_Hero))
            {
                return;
            }
            if (Config.Item("EnabledLog").GetValue<bool>())
            {
                try
                {
                    AddToLog(args.Message, args.Sender);
                }
                catch (Exception)
                {
                    Console.WriteLine("Error at adding log");
                }
            }
            if (Config.Item("Enabled").GetValue<bool>() && !args.Sender.IsMe)
            {
                addMessage(args.Message, args.Sender);
            }
            else
            {
                addMessage(args.Message, args.Sender, false);
            }
        }

        private static async void test(string text)
        {
            if (text.Length > 1)
            {
                string from = toArray[Config.Item("OutFrom").GetValue<StringList>().SelectedIndex];
                string to = toArray[Config.Item("OutTo").GetValue<StringList>().SelectedIndex];
                string x = "";
                x += await TranslateYandex(text, from, to, false);
                Console.WriteLine(x);
            }
        }

        private static async void TranslateAndSend(string text)
        {
            if (text.Length > 1)
            {
                bool all = false;
                var test = new TestPerAll(text);
                if (test.ContainsPerAll)
                {
                    text = test.Output;
                    all = true;
                }
                string from = toArray[Config.Item("OutFrom").GetValue<StringList>().SelectedIndex];
                string to = toArray[Config.Item("OutTo").GetValue<StringList>().SelectedIndex];
                string x = "";
                x += await TranslateYandex(text, from, to, false);
                x = setEncodingDefault(x);
                if (all)
                {
                    Game.Say("/all " + x);
                }
                else
                {
                    Game.Say(x);
                }
            }
        }

        private static async Task<string> TranslateYandex(string text, string fromCulture, string toCulture, bool langs)
        {
            string url;
            string strServerURL;
            var lang = fromCulture == "auto" ? toCulture : fromCulture + "-" + toCulture;
            var key = "";
            tryAgain:
            var keyIndex = new Random().Next(0, yandexApiKeys.Count - 1);
            if (yandexApiKey != "")
            {
                key = yandexApiKey;
            }
            else
            {
                key = yandexApiKeys[keyIndex];
            }
            byte[] bytessss = Encoding.Default.GetBytes(text);
            text = Encoding.UTF8.GetString(bytessss);
            text = text.Replace("\"", "");
            text = text.Replace("\'", "");
            text = HttpUtility.UrlEncode(text);
            strServerURL = yandexUrl + key + "&lang=" + lang + "&text=" + text;
            url = string.Format(strServerURL, fromCulture, toCulture, text.Replace(' ', '+'));
            string html = "";
            Uri uri = new Uri(url);
            try
            {
                html = await DownloadStringAsync(uri);
            }
            catch (Exception e)
            {
                if (yandexApiKey != "")
                {
                    yandexApiKey = "";
                }
                else
                {
                    if (keyIndex == 0)
                    {
                        return text;
                    }
                    yandexUrl.Remove(keyIndex);
                }
                if (yandexUrl.Any())
                {
                    Console.WriteLine("One of the API key is wrong1");
                    goto tryAgain;
                }
            }

            string result = "";
            if (langs)
            {
                result += "(" + fromCulture + " => " + toCulture + ") ";
            }
            string trans = "";
            var code = Regex.Matches(html, "([0-9])\\d+", RegexOptions.IgnoreCase)[0].ToString();
            switch (int.Parse(code))
            {
                case 200:
                    trans = Regex.Matches(html, "\\\".*?\\\"", RegexOptions.IgnoreCase)[4].ToString();
                    trans = trans.Substring(1, trans.Length - 2);
                    if (trans.Length == 0)
                    {
                        return "";
                    }
                    if (trans.Trim() == text.Trim())
                    {
                        return text;
                    }
                    result += trans;
                    return result;
                    break;
                case 401:
                    Console.WriteLine("Invalid API key");
                    break;
                case 402:
                    Console.WriteLine("Blocked API key");
                    break;
                case 403:
                    Console.WriteLine("Exceeded the daily limit on the number of requests");
                    break;
                case 404:
                    Console.WriteLine("Exceeded the daily limit on the amount of translated text");
                    break;
                case 413:
                    Console.WriteLine("Exceeded the maximum text size");
                    break;
                case 422:
                    Console.WriteLine("The text cannot be translated");
                    break;
                case 501:
                    Console.WriteLine("The specified translation direction is not supported");
                    break;
                default:
                    return text;
                    break;
            }
            if (yandexApiKey != "")
            {
                yandexApiKey = "";
            }
            else
            {
                if (keyIndex == 0)
                {
                    return text;
                }
                yandexUrl.Remove(keyIndex);
            }
            if (yandexUrl.Any())
            {
                Console.WriteLine("One of the API key is wrong2");
                goto tryAgain;
            }
            return text;
        }

        private static object GetApiKey()
        {
            throw new NotImplementedException();
        }

        public static byte[] FromHex(string hex)
        {
            hex = hex.Replace(" ", "");
            byte[] raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return raw;
        }

        public static Task<string> DownloadStringAsync(Uri url)
        {
            var tcs = new TaskCompletionSource<string>();
            var wc = new WebClient();
            wc.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0");
            wc.Headers.Add(HttpRequestHeader.AcceptCharset, "UTF-8");
            wc.Encoding = Encoding.UTF8;
            wc.DownloadStringCompleted += (s, e) =>
            {
                if (e.Error != null)
                {
                    tcs.TrySetException(e.Error);
                }
                else if (e.Cancelled)
                {
                    tcs.TrySetCanceled();
                }
                else
                {
                    tcs.TrySetResult(e.Result);
                }
            };
            wc.DownloadStringAsync(url);

            return tcs.Task;
        }

        private static double StringCompare(string a, string b)
        {
            if (a == b)
            {
                return 100;
            }
            if ((a.Length == 0) || (b.Length == 0))
            {
                return 0;
            }
            double maxLen = a.Length > b.Length ? a.Length : b.Length;
            int minLen = a.Length < b.Length ? a.Length : b.Length;
            int sameCharAtIndex = 0;
            for (int i = 0; i < minLen; i++)
            {
                if (a[i] == b[i])
                {
                    sameCharAtIndex++;
                }
            }
            return sameCharAtIndex / maxLen * 100;
        }

        private static void CreateMenu()
        {
            Config = new Menu("ChatTranslator", "ChatTranslator", true);
            Menu translator = new Menu("Translator", "Translator");
            Menu incomingText = new Menu("IncomingText", "IncomingText");
            incomingText.AddItem(new MenuItem("From", "From: ").SetValue(new StringList(fromArrayMenu)));
            incomingText.AddItem(new MenuItem("To", "To: ").SetValue(new StringList(toArrayMenu, 15)));
            incomingText.AddItem(new MenuItem("ShowInChat", "Show in chat").SetValue(false));
            incomingText.AddItem(new MenuItem("Enabled", "Enabled").SetValue(true));
            translator.AddSubMenu(incomingText);
            Menu outgoingText = new Menu("OutgoingText", "OutgoingText");
            outgoingText.AddItem(new MenuItem("OutFrom", "From: ").SetValue(new StringList(toArrayMenu, 15)));
            outgoingText.AddItem(new MenuItem("OutTo", "To: ").SetValue(new StringList(toArrayMenu)));
            outgoingText.AddItem(new MenuItem("EnabledOut", "Enabled").SetValue(false));
            translator.AddSubMenu(outgoingText);
            Menu position = new Menu("Position", "Position");
            position.AddItem(new MenuItem("Horizontal", "Horizontal").SetValue(new Slider(15, 1, 2000)));
            position.AddItem(new MenuItem("Vertical", "Vertical").SetValue(new Slider(500, 1, 2000)));
            position.AddItem(new MenuItem("AutoShow", "Show on message").SetValue(true));
            position.AddItem(new MenuItem("Duration", "   Duration").SetValue(new Slider(3000, 1000, 8000)));
            translator.AddSubMenu(position);
            translator.AddItem(new MenuItem("Check", "Check").SetValue(new KeyBind(32, KeyBindType.Press)));
            Config.AddSubMenu(translator);
            Menu logger = new Menu("Logger", "Logger");
            logger.AddItem(new MenuItem("EnabledLog", "Enable").SetValue(true));
            Config.AddSubMenu(logger);
            Menu copyPaste = new Menu("Paste", "Paste");
            copyPaste.AddItem(
                new MenuItem("Paste", "Paste").SetValue(new KeyBind("P".ToCharArray()[0], KeyBindType.Press)));
            copyPaste.AddItem(
                new MenuItem("PasteForAll", "Paste for all").SetValue(
                    new KeyBind("O".ToCharArray()[0], KeyBindType.Press)));
            copyPaste.AddItem(new MenuItem("Delay", "Spam delay").SetValue(new Slider(2000, 0, 2000)));
            copyPaste.AddItem(new MenuItem("DisablePaste", "Disable this section").SetValue(true));
            Config.AddSubMenu(copyPaste);
            Config.AddItem(new MenuItem("CThelp", "You can use your own API key"))
                .SetTooltip(
                    "AppData\\Roaming\\LSXXXXXXX\\yandexApiKey.txt, copy into the first line \"trnsl.1.1.201...\"");
            Config.AddToMainMenu();
        }
    }

    internal class TestPerAll
    {
        public string Input;
        public string Output;
        public bool ContainsPerAll;

        public TestPerAll(string input)
        {
            Regex regex = new Regex("/all", RegexOptions.IgnoreCase);
            var contains = regex.IsMatch(input);
            var replaced = regex.Replace(input, "");
            this.Input = input;
            Output = replaced.Trim();
            ContainsPerAll = contains;
        }
    }
}