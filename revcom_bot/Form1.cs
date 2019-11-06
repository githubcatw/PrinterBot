using RawPrint;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PrinterBot
{
    public partial class Form1 : Form
    {
        BackgroundWorker bw;

        private static Random random = new Random();
        private string printText;
        private string printer = "\\\\pi\\HP_P1102_Office";
        private string printPhoto;

        static IEnumerable<string> Compact(string str, int chunkSize) {
            return Enumerable.Range(0, str.Length / chunkSize)
                .Select(i => str.Substring(i * chunkSize, (i * chunkSize + chunkSize <= str.Length) ? chunkSize : str.Length - i * chunkSize));
        }

        public static string RandomString(int length) {
            const string chars = "abcdefghijklmnopqrstuvwxyz";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        

        public Form1()
        {
            //
            // The InitializeComponent() call is required for Windows Forms designer support.
            //
            InitializeComponent();

            //
            // TODO: Add constructor code after the InitializeComponent() call.
            //

            this.bw = new BackgroundWorker();
            this.bw.DoWork += bw_DoWork;
        }

        async void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = sender as BackgroundWorker;
            var key = e.Argument as String; // получаем ключ из аргументов
            try
            {
                var Bot = new Telegram.Bot.TelegramBotClient(key); // инициализируем API
                await Bot.SetWebhookAsync(""); // Обязательно! убираем старую привязку к вебхуку для бота

                Bot.OnUpdate += async (object su, Telegram.Bot.Args.UpdateEventArgs evu) =>
                {
                    if (evu.Update.CallbackQuery != null || evu.Update.InlineQuery != null) return; // в этом блоке нам келлбэки и инлайны не нужны
                    var update = evu.Update;
                    var message = update.Message;
                    if (message == null) return;
                    if (message.Type == Telegram.Bot.Types.Enums.MessageType.Text)
                    {
                        if (message.Text == "/start") {
                            string hi = "Hello!\nI'm PrintBot!\nType or forward any text or PDF document and I'll print it on " + printer + "."; 
                            await Bot.SendTextMessageAsync(message.Chat.Id, hi, replyToMessageId: message.MessageId);
                        }
                        else {
                            await Bot.SendTextMessageAsync(message.Chat.Id, "Printing", replyToMessageId: message.MessageId);
                            printText = message.Text;
                            //Print("HP_P1102_Office on pi", "dfd");
                            Print(printer);
                            await Bot.SendTextMessageAsync(message.Chat.Id, "Printed", replyToMessageId: message.MessageId);
                        }
                    } else if (message.Type == Telegram.Bot.Types.Enums.MessageType.Document) {
                        if (message.Document.FileName.Contains("pdf")) {
                            await Bot.SendTextMessageAsync(message.Chat.Id, "Downloading", replyToMessageId: message.MessageId);
                            var outp = "";
                            DownloadFile(message.Document.FileId, Environment.GetEnvironmentVariable("appdata"), Bot, key, out outp);
                            await Bot.SendTextMessageAsync(message.Chat.Id, "Printing", replyToMessageId: message.MessageId);
                            // Create an instance of the Printer
                            IPrinter iprinter = new Printer();
                            // Print the file
                            iprinter.PrintRawFile(printer, outp, "PrintBot job");
                            await Bot.SendTextMessageAsync(message.Chat.Id, "Printed", replyToMessageId: message.MessageId);
                        } else {
                            await Bot.SendTextMessageAsync(message.Chat.Id, "That... is... not... a PDF.", replyToMessageId: message.MessageId);
                        }
                    } else if (message.Type == Telegram.Bot.Types.Enums.MessageType.Photo) {
                        await Bot.SendTextMessageAsync(message.Chat.Id, "Downloading", replyToMessageId: message.MessageId);
                        var outp = "";
                        DownloadFile(message.Photo[message.Photo.Length - 1].FileId, Environment.GetEnvironmentVariable("appdata"), Bot, key, out outp);
                        printPhoto = outp;
                        await Bot.SendTextMessageAsync(message.Chat.Id, "Printing", replyToMessageId: message.MessageId);
                        PrintPhoto();
                        await Bot.SendTextMessageAsync(message.Chat.Id, "Printed", replyToMessageId: message.MessageId);
                    }
                };

                // запускаем прием обновлений
                Bot.StartReceiving();
            }
            catch (Telegram.Bot.Exceptions.ApiRequestException ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        private void BtnRun_Click(object sender, EventArgs e)
        {
            var text = @txtKey.Text; // получаем содержимое текстового поля txtKey в переменную text
            if (text != "" && this.bw.IsBusy != true)
            {
                this.bw.RunWorkerAsync(text); // передаем эту переменную в виде аргумента методу bw_DoWork
                BtnRun.Text = "Bot is running...";
            }
        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        public void Print(string PrinterName) {
            PrintDocument doc = new PrintDocument();
            doc.PrinterSettings.PrinterName = PrinterName;
            doc.PrintPage += new PrintPageEventHandler(PrintHandler);
            doc.Print();
        }

        private void PrintHandler(object sender, PrintPageEventArgs ppeArgs) {
            Font FontNormal = new Font("Verdana", 12);
            Graphics g = ppeArgs.Graphics;
            string outp = "";
            g.DrawString(printText, FontNormal, Brushes.Black, 0, 20, new StringFormat());
        }

        private static void DownloadFile(string fileId, string path, Telegram.Bot.TelegramBotClient bot, string key, out string destination) {
            var test = bot.GetFileAsync(fileId);
            var download_url = @"https://api.telegram.org/file/bot" + key + "//" + test.Result.FilePath;
            var localPath = path + "\\tgprint\\" + test.Result.FilePath;

            if (!Directory.Exists(localPath + "TMP"))
                Directory.CreateDirectory(localPath + "TMP");
            Directory.Delete(localPath + "TMP", false);

            using (StreamWriter file =
            new StreamWriter(localPath)) {
                file.WriteLine("");
            }
            using (WebClient client = new WebClient()) {
                client.DownloadFile(new Uri(download_url), localPath);
            }
            destination = localPath;
        }

        private void Form1_Load(object sender, EventArgs e) {

        }

        private void PrintPhoto () {
            PrintDocument pd = new PrintDocument();
            pd.PrintPage += PrintPhotoPage;
            pd.Print();
        }

        private void PrintPhotoPage(object o, PrintPageEventArgs e) {
            Image img = Image.FromFile(printPhoto);
            Point loc = new Point(100, 100);
            e.Graphics.DrawImage(img, e.MarginBounds);
        }
    }

}
