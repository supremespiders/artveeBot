using artveeBot.Models;
using artveeBot.Services;
using MetroFramework.Controls;
using MetroFramework.Forms;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace artveeBot
{
    public partial class MainForm : MetroForm
    {
        public bool LogToUi = true;
        public bool LogToFile = true;

        private readonly string _path = Application.StartupPath;
        private int _maxConcurrency;
        private Dictionary<string, string> _config;
        private CancellationTokenSource _cancellationTokenSource;

        public MainForm()
        {
            InitializeComponent();
        }


        private async Task MainWork()
        {
            await Task.Delay(3000);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ServicePointManager.DefaultConnectionLimit = 65000;
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            inputI.Text = _path + @"\input.txt";
            outputI.Text = _path + @"\output.csv";
            Directory.CreateDirectory("data");
            LoadConfig();
            Notifier.OnDisplay += OnDisplay;
            Notifier.OnLog += OnLog;
            Notifier.OnError += OnError;
            Notifier.OnProgress += OnProgress;
        }

        private void OnProgress(object sender, (int x, int total) e)
        {
            SetProgress(e.x * 100 / e.total);
        }

        private void OnError(object sender, string e)
        {
            ErrorLog(e);
        }

        private void OnLog(object sender, string e)
        {
            NormalLog(e);
        }

        private void OnDisplay(object sender, string e)
        {
            Display(e);
        }

        void InitControls(Control parent)
        {
            try
            {
                foreach (Control x in parent.Controls)
                {
                    try
                    {
                        if (x.Name.EndsWith("I"))
                        {
                            switch (x)
                            {
                                case MetroCheckBox _:
                                case CheckBox _:
                                    ((CheckBox)x).Checked = bool.Parse(_config[((CheckBox)x).Name]);
                                    break;
                                case RadioButton radioButton:
                                    radioButton.Checked = bool.Parse(_config[radioButton.Name]);
                                    break;
                                case TextBox _:
                                case RichTextBox _:
                                case MetroTextBox _:
                                    x.Text = _config[x.Name];
                                    break;
                                case NumericUpDown numericUpDown:
                                    numericUpDown.Value = int.Parse(_config[numericUpDown.Name]);
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }

                    InitControls(x);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public void SaveControls(Control parent)
        {
            try
            {
                foreach (Control x in parent.Controls)
                {
                    #region Add key value to disctionarry

                    if (x.Name.EndsWith("I"))
                    {
                        switch (x)
                        {
                            case MetroCheckBox _:
                            case RadioButton _:
                            case CheckBox _:
                                _config.Add(x.Name, ((CheckBox)x).Checked + "");
                                break;
                            case TextBox _:
                            case RichTextBox _:
                            case MetroTextBox _:
                                _config.Add(x.Name, x.Text);
                                break;
                            case NumericUpDown _:
                                _config.Add(x.Name, ((NumericUpDown)x).Value + "");
                                break;
                            default:
                                Console.WriteLine(@"could not find a type for " + x.Name);
                                break;
                        }
                    }

                    #endregion

                    SaveControls(x);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void SaveConfig()
        {
            _config = new Dictionary<string, string>();
            SaveControls(this);
            try
            {
                File.WriteAllText("config.txt", JsonConvert.SerializeObject(_config, Formatting.Indented));
            }
            catch (Exception e)
            {
                ErrorLog(e.ToString());
            }
        }

        private void LoadConfig()
        {
            try
            {
                _config = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("config.txt"));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return;
            }

            InitControls(this);
        }

        static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.ToString(), @"Unhandled Thread Exception");
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show((e.ExceptionObject as Exception)?.ToString(), @"Unhandled UI Exception");
        }

        #region UIFunctions

        public delegate void WriteToLogD(string s, Color c);

        public void WriteToLog(string s, Color c)
        {
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new WriteToLogD(WriteToLog), s, c);
                    return;
                }

                if (LogToUi)
                {
                    if (DebugT.Lines.Length > 5000)
                    {
                        DebugT.Text = "";
                    }

                    DebugT.SelectionStart = DebugT.Text.Length;
                    DebugT.SelectionColor = c;
                    DebugT.AppendText(DateTime.Now.ToString(Utility.SimpleDateFormat) + " : " + s + Environment.NewLine);
                }

                Console.WriteLine(DateTime.Now.ToString(Utility.SimpleDateFormat) + @" : " + s);
                if (LogToFile)
                {
                    File.AppendAllText(_path + "/data/log.txt", DateTime.Now.ToString(Utility.SimpleDateFormat) + @" : " + s + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public void NormalLog(string s)
        {
            WriteToLog(s, Color.Black);
        }

        public void ErrorLog(string s)
        {
            WriteToLog(s, Color.Red);
        }

        public void SuccessLog(string s)
        {
            WriteToLog(s, Color.Green);
        }

        public void CommandLog(string s)
        {
            WriteToLog(s, Color.Blue);
        }

        public delegate void SetProgressD(int x);

        public void SetProgress(int x)
        {
            if (InvokeRequired)
            {
                Invoke(new SetProgressD(SetProgress), x);
                return;
            }

            if ((x <= 100))
            {
                ProgressB.Value = x;
            }
        }

        public delegate void DisplayD(string s);

        public void Display(string s)
        {
            if (InvokeRequired)
            {
                Invoke(new DisplayD(Display), s);
                return;
            }

            displayT.Text = s;
        }

        #endregion

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveConfig();
        }

        private void loadInputB_Click_1(object sender, EventArgs e)
        {
            OpenFileDialog o = new OpenFileDialog { Filter = @"TXT|*.txt", InitialDirectory = _path };
            if (o.ShowDialog() == DialogResult.OK)
            {
                inputI.Text = o.FileName;
            }
        }

        private void openInputB_Click_1(object sender, EventArgs e)
        {
            try
            {
                Process.Start(inputI.Text);
            }
            catch (Exception ex)
            {
                ErrorLog(ex.ToString());
            }
        }

        private void openOutputB_Click_1(object sender, EventArgs e)
        {
            try
            {
                Process.Start(outputI.Text);
            }
            catch (Exception ex)
            {
                ErrorLog(ex.ToString());
            }
        }

        private void loadOutputB_Click_1(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog
            {
                Filter = @"csv file|*.csv",
                Title = @"Select the output location"
            };
            saveFileDialog1.ShowDialog();
            if (saveFileDialog1.FileName != "")
            {
                outputI.Text = saveFileDialog1.FileName;
            }
        }

        private async void startB_Click_1(object sender, EventArgs e)
        {
            SaveConfig();
            LogToUi = logToUII.Checked;
            LogToFile = logToFileI.Checked;
            _maxConcurrency = (int)threadsI.Value;
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                var scraper = new Scraper((int)threadsI.Value);
                await scraper.MainWork(_cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                CommandLog("Canceled by user");
            }
            catch (KnownException ex)
            {
                ErrorLog(ex.Message);
            }
            catch (Exception ex)
            {
                ErrorLog(ex.ToString());
            }
            Display("---");
        }

        private void stopB_Click(object sender, EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
        }
    }
}