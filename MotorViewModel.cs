using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SPIGAIICode
{
    public class MotorViewModel : INotifyPropertyChanged
    {
        private string _pos = "---", _spd = "---", _en = "---", _alarm = "---";
        public int SlaveId { get; set; }
        public string HeaderColor { get; set; }

        public string Position { get => _pos; set { _pos = value; OnProp(); } }
        public string Speed { get => _spd; set { _spd = value; OnProp(); } }
        public string EnabledStatus { get => _en; set { _en = value; OnProp(); } }
        public string AlarmStatus { get => _alarm; set { _alarm = value; OnProp(); } }

        public string DistanceInput { get; set; } = "10000";
        public string SpeedInput { get; set; } = "50";
        public string AccelInput { get; set; } = "500";
        // Add this inside the MotorViewModel class in MotorViewModel.cs
        public string HeaderTitle { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnProp([CallerMemberName] string p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}