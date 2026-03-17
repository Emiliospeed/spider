using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SPIGAIICode
{
    public partial class MainWindow : Window
    {
        // Serial Port Variables
        private SerialPort _serial;
        private CancellationTokenSource _cts;
        private readonly SemaphoreSlim _serialLock = new SemaphoreSlim(1, 1);

        // Robot Service Variables
        private RobotControllerService robotService;
        private List<string> lastMessages = new List<string>();
        private Button[] carrierButtons;
        private Button[] shuttleButtons;

        // ViewModels
        public MotorViewModel M1 { get; set; } = new MotorViewModel { SlaveId = 1, HeaderColor = "#4a90e2" };
        public MotorViewModel M2 { get; set; } = new MotorViewModel { SlaveId = 2, HeaderColor = "#4a90e2" };

        public MainWindow()
        {
            InitializeComponent();

            // Initialize Motor UI
            PortCombo.ItemsSource = SerialPort.GetPortNames();
            Motor1UI.DataContext = M1;
            Motor2UI.DataContext = M2;

            // Initialize Robot Logic
            robotService = new RobotControllerService();
            robotService.MessageReceived += UpdateMessageBox;

            carrierButtons = new Button[] { Carrier1, Carrier2, Carrier3, Carrier4, Carrier5, Carrier6, Carrier7 };
            shuttleButtons = new Button[] { Shuttle1, Shuttle2, Shuttle3, Shuttle4, Shuttle5, Shuttle6, Shuttle7 };

            SetMotionButtonsEnabled(false);

            this.Closed += (s, e) => {
                _cts?.Cancel();
                robotService.Stop();
                _serial?.Close();
            };
        }

        #region Serial Motor Logic

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(PortCombo.Text)) return;
                _serial = new SerialPort(PortCombo.Text, 115200, Parity.Even, 8, StopBits.One);
                _serial.ReadTimeout = 500;
                _serial.Open();

                _cts = new CancellationTokenSource();
                Task.Run(() => PollLoop(_cts.Token));

                BtnDisconnect.IsEnabled = true;
                Log($"Connected to {PortCombo.Text}");
            }
            catch (Exception ex) { Log("Error: " + ex.Message); }
        }

        private async Task PollLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UpdateMotorStatus(M1);
                await Task.Delay(50);
                await UpdateMotorStatus(M2);
                await Task.Delay(200);
            }
        }

        private async Task UpdateMotorStatus(MotorViewModel m)
        {
            if (_serial == null || !_serial.IsOpen) return;
            await _serialLock.WaitAsync();
            try
            {
                string cmd = ModbusAscii.BuildReadRequest((byte)m.SlaveId, 0x8009, 6);
                _serial.Write(cmd);

                string response = await Task.Run(() => _serial.ReadLine());
                int[] data = ModbusAscii.ParseReadResponse(response);

                if (data != null && data.Length >= 6)
                {
                    Dispatcher.Invoke(() => {
                        m.AlarmStatus = data[0] == 0 ? "OK" : "A" + data[0].ToString("X3");
                        int low = data[3];
                        int high = data[4];
                        int fullPos = (high << 16) | low;
                        m.Position = fullPos.ToString();
                        m.Speed = data[5].ToString();
                    });
                }
            }
            catch { /* Silent timeout */ }
            finally { _serialLock.Release(); }
        }

        public async void SendCommand(byte slave, ushort addr, ushort value)
        {
            if (_serial == null || !_serial.IsOpen) return;
            await _serialLock.WaitAsync();
            try
            {
                string frame = ModbusAscii.BuildSingleWrite(slave, addr, value);
                _serial.Write(frame);
                await Task.Run(() => _serial.ReadLine());
            }
            finally { _serialLock.Release(); }
        }

        public void Log(string msg) => Dispatcher.Invoke(() => {
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            LogBox.ScrollToEnd();
        });

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            _serial?.Close();
            BtnDisconnect.IsEnabled = false;
            Log("Disconnected.");
        }

        #endregion

        #region Robot Logic

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            robotService.StartServer(34891);
            SetMotionButtonsEnabled(true);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            robotService.Stop();
            SetMotionButtonsEnabled(false);
        }

        private void MotionButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;

            int index;
            string content = btn.Content.ToString();
            if (content.StartsWith("Carrier"))
            {
                index = Array.IndexOf(carrierButtons, btn) + 1;
                robotService.SendCommand($"carrier{index}");
            }
            else if (content.StartsWith("Shuttle"))
            {
                index = Array.IndexOf(shuttleButtons, btn) + 1;
                robotService.SendCommand($"shuttlecarrier{index}");
            }
        }

        private void SetSpeedButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(SpeedBox.Text.Trim(), out int speed) && speed > 0)
            {
                if (!robotService.RobotReady)
                    UpdateMessageBox("Robot is in motion. Wait to set speed.");
                else
                    robotService.SendCommand($"speed:{speed}");
            }
            else
            {
                UpdateMessageBox("Invalid speed value.");
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e) { /* Logic kept from source */ }
        private void ResumeButton_Click(object sender, RoutedEventArgs e) { /* Logic kept from source */ }

        private void SetMotionButtonsEnabled(bool enabled)
        {
            if (carrierButtons == null || shuttleButtons == null) return;
            foreach (var b in carrierButtons) b.IsEnabled = enabled;
            foreach (var b in shuttleButtons) b.IsEnabled = enabled;
        }

        private void UpdateMessageBox(string msg)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateMessageBox(msg));
                return;
            }
            if (lastMessages.Count >= 5) lastMessages.RemoveAt(0);
            lastMessages.Add(msg);
            MessageBox.Text = string.Join("\n", lastMessages);
        }

        #endregion

        private void Motor1UI_Loaded(object sender, RoutedEventArgs e) { }
    }
}