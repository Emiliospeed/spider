using System;
using System.Windows;
using System.Windows.Controls;

namespace SPIGAIICode
{
    public partial class MotorControl : UserControl
    {
        public MotorControl() => InitializeComponent();

        private void MoveCCW_Click(object sender, RoutedEventArgs e) => SendMoveCommand(-1);
        private void MoveCW_Click(object sender, RoutedEventArgs e) => SendMoveCommand(1);

        private void SendMoveCommand(int direction)
        {
            var vm = DataContext as MotorViewModel;
            var main = Application.Current.MainWindow as MainWindow;
            if (vm == null || main == null) return;

            try
            {
                int dist = int.Parse(vm.DistanceInput) * direction;
                ushort speed = ushort.Parse(vm.SpeedInput);
                ushort accel = ushort.Parse(vm.AccelInput);

                // Send commands in background
                main.SendCommand((byte)vm.SlaveId, 0x8002, speed);
                main.SendCommand((byte)vm.SlaveId, 0x8003, accel);

                ushort lo = (ushort)(dist & 0xFFFF);
                ushort hi = (ushort)((dist >> 16) & 0xFFFF);
                main.SendCommand((byte)vm.SlaveId, 0x8004, lo);
                main.SendCommand((byte)vm.SlaveId, 0x8005, hi);

                main.Log($"[M{vm.SlaveId}] Move triggered: {dist}");
            }
            catch (Exception ex) { main.Log("Input Error: " + ex.Message); }
        }

        private void Stop_Click(object sender, RoutedEventArgs e) => GetMain()?.SendCommand(GetSlave(), 0x8000, 0);
        private void Enable_Click(object sender, RoutedEventArgs e) => GetMain()?.SendCommand(GetSlave(), 0x8000, 1);
        private void Disable_Click(object sender, RoutedEventArgs e) => GetMain()?.SendCommand(GetSlave(), 0x8000, 0);
        private void ResetPos_Click(object sender, RoutedEventArgs e) => GetMain()?.SendCommand(GetSlave(), 0x800C, 0);
        private void ClearAlarm_Click(object sender, RoutedEventArgs e) => GetMain()?.SendCommand(GetSlave(), 0x8009, 0);

        private MainWindow GetMain() => Application.Current.MainWindow as MainWindow;
        private byte GetSlave() => (byte)((MotorViewModel)DataContext).SlaveId;
    }
}