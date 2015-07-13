using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Net.Http;

namespace GestureTracker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        private DrawingGroup kinectImageDrawingGroup;
        private ImageSource kinectImageSource;

        private string statusText;
        public event PropertyChangedEventHandler PropertyChanged;

        private string serverAddress = "localhost";
        private int serverPort = 8000;
        private string sessionName = "Test"; 

        public MainWindow()
        {
            this.kinectImageDrawingGroup = new DrawingGroup();
            this.kinectImageSource = new DrawingImage(this.kinectImageDrawingGroup);

            this.DataContext = this;

            this.InitializeComponent();

            Kinect2KitAPI.Server_Address = "http://" + this.serverAddress + ":" + this.serverPort;

            this.StatusText = "Running Gesture Tracker...";
        }

        public ImageSource KinectImageSource
        {
            get
            {
                return this.kinectImageSource;
            }
        }

        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        private void Setup_Kinect2Kit_Server_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Setup_Kinects_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Setup_Session_Click(object sender, RoutedEventArgs e)
        {
            var values = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("name", this.sessionName)
            };
            dynamic response = Kinect2KitAPI.GetPOSTResponseJSON(Kinect2KitAPI.API_New_Session, values);
            this.StatusText = "Setup session..." + response.message;
        }

        private void Calibration_Acquire_Click(object sender, RoutedEventArgs e)
        {
            dynamic response = Kinect2KitAPI.GetPOSTResponseJSON(Kinect2KitAPI.API_Acquire_Calibration);
            this.StatusText = "Start acquiring calibration..." + response.message;
        }
        private void Calibration_Resolve_Click(object sender, RoutedEventArgs e)
        {
            dynamic response = Kinect2KitAPI.GetPOSTResponseJSON(Kinect2KitAPI.API_Resolve_Calibration);
            this.StatusText = "Start resolving calibration..." + response.message;
        }

        private void Start_Tracking_Click(object sender, RoutedEventArgs e)
        {
            dynamic response = Kinect2KitAPI.GetPOSTResponseJSON(Kinect2KitAPI.API_Start_Tracking);
            this.StatusText = "Start Tracking..." + response.message;
        }
    }
}
