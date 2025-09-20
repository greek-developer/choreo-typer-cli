
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChoreoTyper
{
    public partial class MainForm : Form
    {
        private HttpListener? httpListener;
        private Task? httpListenerTask;
        private List<string> lines = new List<string>();
        private ListBox? listBoxEntries;
        private string? lastLoadedFilePath;
        private System.Threading.CancellationTokenSource? sendAllCts;

        public MainForm(string[] args)
        {
            InitializeComponent();

            if (args.Length > 0 && File.Exists(args[0]))
            {
                LoadLines(args[0]);
            }
            else
            {
                LoadText("##T: Hello World\n##T: http://host:5005/prev\n##T: http://host:5005/next");
            }

            // Add double-click event to reload file
            listBoxEntries!.MouseDoubleClick += (s, e) =>
            {
                var cindex = listBoxEntries!.SelectedIndex;

                if (!string.IsNullOrEmpty(lastLoadedFilePath) && File.Exists(lastLoadedFilePath))
                {
                    LoadLines(lastLoadedFilePath);
                    listBoxEntries.SelectedIndex = cindex;
                }
            };
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            StartHttpServer();
        }

        private void StartHttpServer()
        {
            httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://localhost:5005/");
            try
            {
                httpListener.Start();
                httpListenerTask = Task.Run(() => HandleRequests());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start HTTP server: {ex.Message}");
            }
        }


        private async Task HandleRequests()
        {
            while (httpListener != null && httpListener.IsListening)
            {
                try
                {
                    var context = await httpListener.GetContextAsync();
                    string path = context.Request.Url?.AbsolutePath ?? "";
                    if (path.Equals("/prev", StringComparison.OrdinalIgnoreCase))
                    {
                        this.Invoke(new Action(MovePrev));
                        context.Response.StatusCode = 200;
                        await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("OK"));
                    }
                    else if (path.Equals("/next", StringComparison.OrdinalIgnoreCase))
                    {
                        this.Invoke(new Action(MoveNext));
                        context.Response.StatusCode = 200;
                        await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("OK"));
                    }
                    else if (path.Equals("/type", StringComparison.OrdinalIgnoreCase))
                    {
                        this.Invoke(new Action(SendActive));
                        context.Response.StatusCode = 200;
                        await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("OK"));
                    }
                    else if (path.Equals("/play", StringComparison.OrdinalIgnoreCase))
                    {
                        this.Invoke(new Action(StartSendAll));
                        context.Response.StatusCode = 200;
                        await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("OK"));
                    }
                    else if (path.Equals("/stop", StringComparison.OrdinalIgnoreCase))
                    {
                        this.Invoke(new Action(StopSendAll));
                        context.Response.StatusCode = 200;
                        await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("OK"));
                    }
                    else
                    {
                        context.Response.StatusCode = 404;
                        await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("Not Found"));
                    }
                    context.Response.Close();
                }
                catch { /* ignore errors */ }
            }
        }

        private void LoadLines(string filePath)
        {
            lastLoadedFilePath = filePath;

            LoadText(File.ReadAllText(filePath));
        }

        private void LoadText(string text)
        {
            lines = text.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();

            listBoxEntries!.Items.Clear();
            foreach (var entry in lines)
            {
                listBoxEntries!.Items.Add(entry.Replace("\r\n", "⏎ ").Replace("\n", "⏎ ").Replace("\r", "⏎ "));
            }

            if (lines.Count > 0)
                listBoxEntries.SelectedIndex = 0;
        }

        private async void StartSendAll()
        {
            if (sendAllCts != null)
            {
                sendAllCts.Cancel();
                sendAllCts = null;
            }

            if (lines.Count == 0) return;

            sendAllCts = new System.Threading.CancellationTokenSource();

            var token = sendAllCts.Token;
            int startIdx = listBoxEntries!.SelectedIndex >= 0 ? listBoxEntries.SelectedIndex : 0;

            for (int i = startIdx; i < lines.Count; i++)
            {
                if (token.IsCancellationRequested) break;
                SendActive();
                await Task.Delay(500);
            }

            sendAllCts = null;
        }

        private void StopSendAll()
        {
            if (sendAllCts != null)
            {
                sendAllCts.Cancel();
                sendAllCts = null;
            }
        }         

        private void InitializeComponent()
        {
            this.Text = "Choreo Typer";
            this.Size = new Size(600, 800);

            this.StartPosition = FormStartPosition.CenterScreen;

            listBoxEntries = new ListBox { Dock = DockStyle.Fill, Font = new Font(FontFamily.GenericMonospace, 22), HorizontalScrollbar = true };

            var panel = new Panel { Dock = DockStyle.Fill };
            panel.Controls.Add(listBoxEntries!);
            this.Controls.Add(panel);
        }

        private void MovePrev()
        {
            if (lines.Count == 0) return;
            listBoxEntries!.SelectedIndex = (listBoxEntries.SelectedIndex - 1 + lines.Count) % lines.Count;
        }

        private void MoveNext()
        {
            if (lines.Count == 0) return;
            listBoxEntries!.SelectedIndex = (listBoxEntries.SelectedIndex + 1) % lines.Count;
        }

        private void SendActive()
        {
            if (lines.Count == 0) return;

            var line = lines[listBoxEntries!.SelectedIndex];

            MoveNext();

            var commands = line
                .Split("##")
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            foreach (var command in commands)
            {
                var commandType = command[0];
                var commandText = command.Length > 3 ? command.Substring(3).TrimEnd() : "";

                var textToSend = commandType switch
                {
                    'T' => ProcessTextCommand(commandText),
                    'L' => ProcessTextCommand(commandText),
                    'C' => ProcessSpecialCommand(commandText),
                    'W' => ProcessWaitCommand(commandText),
                    'R' => commandText,
                    'N' => "{ENTER}",
                    _ => "",
                };

                if (textToSend != "")
                {
                    SendKeys.SendWait(textToSend);
                    System.Threading.Thread.Sleep(100);
                    if (commandType == 'L')
                    {
                        SendKeys.SendWait("{ENTER}");
                        System.Threading.Thread.Sleep(100);
                    }
                }
            }
        }
        
        private string ProcessSpecialCommand(string commandText)
        {
            commandText = commandText.Replace(" ", "");

            var shortcodes = new Dictionary<string, string>
            {
                { "n", "{ENTER}" },
                { "h", "{HOME}" },
                { "e", "{END}" },
                { "l", "{LEFT}"},
                { "r", "{RIGHT}"},
                { "d", "{DOWN}" },
                { "u", "{UP}" },
                { "t", "{TAB}" }                
            };

            foreach (var shortcode in shortcodes)
            {
                commandText = commandText.Replace(shortcode.Key, shortcode.Value);
            }

            return commandText;
        }

        // Escapes special characters for SendKeys
        private static string ProcessTextCommand(string input)
        {
            // List of special characters for SendKeys
            char[] specialChars = new char[] { '+', '^', '%', '~', '(', ')', '{', '}', '[', ']' };
            var result = new System.Text.StringBuilder();
            foreach (char c in input)
            {
                if (specialChars.Contains(c))
                    result.Append($"{{{c}}}");
                else
                    result.Append(c);
            }
            return result.ToString();
        }

        private static string ProcessWaitCommand(string commandText)
        {
            var delay = int.Parse(commandText.Trim());
            System.Threading.Thread.Sleep(delay);
            return ""; // No text to send, just a delay
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (httpListener != null)
            {
                httpListener.Stop();
                httpListener.Close();
                httpListener = null;
            }
            base.OnFormClosed(e);
        }
    }
}

