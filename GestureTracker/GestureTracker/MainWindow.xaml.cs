
namespace GestureTracker
{
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
    using Kinect2KitAPI;
    using System.IO;
    using Microsoft.Win32;
    using System.Xml.Linq;

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

        private Dictionary<string, string> kinectClientsDict = new Dictionary<string, string>();

        public MainWindow()
        {
            this.kinectImageDrawingGroup = new DrawingGroup();
            this.kinectImageSource = new DrawingImage(this.kinectImageDrawingGroup);

            this.DataContext = this;

            this.InitializeComponent();

            this.StatusText = "Running...";
        }

        private void ToolBar_Loaded(object sender, RoutedEventArgs e)
        {
            ToolBar toolBar = sender as ToolBar;
            var overflowGrid = toolBar.Template.FindName("OverflowGrid", toolBar) as FrameworkElement;
            if (overflowGrid != null)
            {
                overflowGrid.Visibility = Visibility.Collapsed;
            }
            var mainPanelBorder = toolBar.Template.FindName("MainPanelBorder", toolBar) as FrameworkElement;
            if (mainPanelBorder != null)
            {
                mainPanelBorder.Margin = new Thickness();
            }
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

        private void File_Open_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                string setupFile = openFileDialog.FileName;
                Kinect2KitAPI.LoadSetup(setupFile);
                // menu
                this.Menu_Track.IsEnabled = true;
                this.MenuItem_Track_Start.IsEnabled = true;
                this.MenuItem_Track_Start.Visibility = Visibility.Visible;
                // toolbar
                this.Button_Track_Start.IsEnabled = true;
                this.Button_Track_Start.Visibility = Visibility.Visible;

                this.StatusText = String.Format("Kinect2Kit setup loaded from: {0}", setupFile);
            }
            else
            {
                this.StatusText = "Kinect2Kit setup not loaded";
            }
        }

        private void Start_Track_Click(object sender, RoutedEventArgs e)
        {
            SetupSessionDialog setupSession = new SetupSessionDialog();
            if (setupSession.ShowDialog() == true)
            {
                string sessionName = setupSession.entryName.Text;
                Kinect2KitAPI.Response resp = Kinect2KitAPI.StartSession(sessionName);
                if (resp.IsSuccessful)
                {
                    //this.MenuItem_Start_Calibration.IsEnabled = true;
                    this.StatusText = String.Format("Starting Kinect2Kit session {0} @ {1}", sessionName, Kinect2KitAPI.ServerEndpoint);
                }
                else
                {
                    this.StatusText = String.Format("Kinect2Kit session not started. Message: ", resp.ServerMessage);
                }
            }
            else
            {
                this.StatusText = "Kinect2Kit session not started";
            }
        }

        private void Pause_Track_Click(object sender, RoutedEventArgs e)
        {
            //dynamic response = Kinect2KitAPI.GetResponse(Kinect2KitAPI.API_AcquireCalibration);
            //this.StatusText = "Start acquiring calibration..." + response.message;
        }

        private void Resume_Track_Click(object sender, RoutedEventArgs e)
        {
            //dynamic response = Kinect2KitAPI.GetResponse(Kinect2KitAPI.API_AcquireCalibration);
            //this.StatusText = "Start acquiring calibration..." + response.message;
        }

        private void Stop_Session_Click(object sender, RoutedEventArgs e)
        {
            //dynamic response = Kinect2KitAPI.GetResponse(Kinect2KitAPI.API_AcquireCalibration);
            //this.StatusText = "Start acquiring calibration..." + response.message;
        }

        private void Calibration_Resolve_Click(object sender, RoutedEventArgs e)
        {
            //dynamic response = Kinect2KitAPI.GetResponse(Kinect2KitAPI.API_ResolveCalibration);
            //this.StatusText = "Start resolving calibration..." + response.message;
        }

        private void Start_Track_Click(object sender, RoutedEventArgs e)
        {
            //dynamic response = Kinect2KitAPI.GetResponse(Kinect2KitAPI.API_StartTracking);
            //this.StatusText = "Start Tracking..." + response.message;
        }

        private void Stop_Track_Click(object sender, RoutedEventArgs e)
        {
            //dynamic response = Kinect2KitAPI.GetResponse(Kinect2KitAPI.API_StartTracking);
            //this.StatusText = "Start Tracking..." + response.message;
        }

        private void MenuItem_Track_Pause_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
