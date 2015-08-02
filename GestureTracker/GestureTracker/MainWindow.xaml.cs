using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.IO;
using Microsoft.Win32;
using System.Xml.Linq;
using Microsoft.Kinect;
using Kinect2KitAPI;

namespace GestureTracker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // Drawing
        private DrawingGroup trackingImageDrawingGroup;
        private ImageSource trackingImageSource;
        private List<Pen> bodyColors;
        private Brush trackedJointBrush;
        private Brush inferredJointBrush;
        private Pen inferredBonePen;
        private double jointThickness;

        private KinectSensor kinectSensor;
        private CoordinateMapper coordinateMapper;
        private int displayWidth, displayHeight;

        private string statusText;
        public event PropertyChangedEventHandler PropertyChanged;

        private Task trackingTask;
        private CancellationTokenSource trackingTaskTokenSource;
        private bool trackingTaskPaused;

        /// <summary>
        /// All or only average skeletons
        /// </summary>
        private bool viewAll;
        /// <summary>
        /// The name of the Kinect providing
        /// </summary>
        private string selectedViewName;

        public MainWindow()
        {
            this.trackingImageDrawingGroup = new DrawingGroup();
            this.trackingImageSource = new DrawingImage(this.trackingImageDrawingGroup);
            this.bodyColors = new List<Pen>();
            this.bodyColors.Add(new Pen(Brushes.Red, 6));
            this.bodyColors.Add(new Pen(Brushes.Orange, 6));
            this.bodyColors.Add(new Pen(Brushes.Green, 6));
            this.bodyColors.Add(new Pen(Brushes.Blue, 6));
            this.bodyColors.Add(new Pen(Brushes.Indigo, 6));
            this.bodyColors.Add(new Pen(Brushes.Violet, 6));
            this.trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));
            this.inferredJointBrush = Brushes.Yellow;
            this.inferredBonePen = new Pen(Brushes.Gray, 1);
            this.jointThickness = 3;

            this.kinectSensor = KinectSensor.GetDefault();
            this.kinectSensor.Open();
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;
            FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;

            this.DataContext = this;

            this.InitializeComponent();

            this.StatusText = "Running...";
        }

        // Collapse toolbar overflow arrow
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

        // Release Kinect resources
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.trackingTask != null)
            {
                this.StopTrackingTask();
            }
            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        // Tracking (skeletons) view
        public ImageSource TrackingImageSource
        {
            get
            {
                return this.trackingImageSource;
            }
        }

        // Status text at the bottom of the window
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

        #region FILE
        private void File_Open_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                this.StatusText = "Loading Kinect2Kit setup...";

                string setupFile = openFileDialog.FileName;
                if (!Kinect2Kit.TryLoadSetup(setupFile))
                {
                    MessageBox.Show(this, "The server is not available. Is it running?", "Kinect2Kit");
                    this.StatusText = "Kinect2Kit setup not loaded. The server is not available";
                }

                // open file
                this.MenuItem_File_Open.IsEnabled = false;
                this.Button_File_Open.Visibility = Visibility.Collapsed;

                // track
                this.Menu_Track.IsEnabled = true;
                this.MenuItem_Track_Start.Visibility = Visibility.Visible;
                this.Button_Track_Start.Visibility = Visibility.Visible;

                // view
                foreach (Kinect2KitClientInfo view in Kinect2Kit.KinectClients)
                {
                    MenuItem viewMenuitem = new MenuItem();
                    viewMenuitem.Header = view.Name;
                    viewMenuitem.Click += this.ViewMenuitem_Click;
                    this.Menu_View.Items.Add(viewMenuitem);
                }
                // check first view
                MenuItem firstView = (MenuItem)this.Menu_View.Items.GetItemAt(0);
                firstView.IsChecked = true;
                this.selectedViewName = (string)firstView.Header;

                this.StatusText = String.Format("Kinect2Kit setup loaded from {0}.", setupFile);
            }
            else
            {
                this.StatusText = "Kinect2Kit setup not loaded.";
            }
        }

        private void File_Exit_Click(object sender, RoutedEventArgs e)
        {
            // Exit application
        }
        #endregion

        #region VIEW
        /// <summary>
        /// Check the selected view
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ViewMenuitem_Click(object sender, RoutedEventArgs e)
        {
            foreach (MenuItem view in this.Menu_View.Items)
            {
                view.IsChecked = false;
            }
            MenuItem clicked = sender as MenuItem;
            clicked.IsChecked = true;
        }

        #endregion

        #region TRACK
        private async void Track_Start_Click(object sender, RoutedEventArgs e)
        {
            SetupSessionDialog setupSession = new SetupSessionDialog();
            setupSession.Owner = Application.Current.MainWindow;
            setupSession.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (setupSession.ShowDialog() == true)
            {
                // disable Start while waiting from the sever
                this.MenuItem_Track_Start.IsEnabled = false;
                this.Button_Track_Start.Visibility = Visibility.Collapsed;

                string sessionName = setupSession.entryName.Text;

                this.StatusText = String.Format("Kinect2Kit server @ {1} was starting session {1}.", Kinect2Kit.ServerEndpoint, sessionName);

                Kinect2KitSimpleResponse resp = await Kinect2Kit.StartSessionAsync(sessionName);
                if (resp.IsSuccessful)
                {
                    this.MenuItem_Track_Pause.IsEnabled = true;
                    this.Button_Track_Pause.Visibility = Visibility.Visible;

                    this.MenuItem_Track_Stop.IsEnabled = true;
                    this.Button_Track_Stop.Visibility = Visibility.Visible;

                    this.StatusText = String.Format("Kinect2Kit server @ {0} started session {1}.", Kinect2Kit.ServerEndpoint, sessionName);

                    // begin tracking task
                    this.trackingTaskTokenSource = new CancellationTokenSource();
                    this.trackingTask = Task.Run(() => this.Track(this.trackingTaskTokenSource.Token), this.trackingTaskTokenSource.Token);
                }
                else
                {
                    // re-enable Start
                    this.MenuItem_Track_Start.IsEnabled = true;
                    this.Button_Track_Start.Visibility = Visibility.Visible;

                    this.StatusText = String.Format("Kinect2Kit session {0} not started. Message: {1}.", sessionName, resp.ServerMessage);
                }
            }
            else
            {
                this.StatusText = "Kinect2Kit session not started.";
            }
        }

        private async void Track(CancellationToken ct)
        {
            // calibration
            Kinect2KitSimpleResponse resp = await Kinect2Kit.StartCalibrationAsync();
            if (resp.IsSuccessful)
            {
                this.StatusText = String.Format("Kinect2Kit server @ {0} started calibration.", Kinect2Kit.ServerEndpoint);
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    Kinect2KitCalibrationResponse calibrationResp = await Kinect2Kit.GetCalibrationStatus();
                    if (calibrationResp.Finished)
                    {
                        this.StatusText = String.Format("Kinect2Kit server @ {0} finished calibration.", Kinect2Kit.ServerEndpoint);
                        break;
                    }
                    else if (calibrationResp.AcquiringFrames)
                    {
                        this.StatusText = String.Format("Kinect2Kit server @ {0} was acquring frames. Required: {1}, Remained: {2}.", Kinect2Kit.ServerEndpoint, calibrationResp.RequiredFrames, calibrationResp.RemainedFrames);
                    }
                    else if (calibrationResp.ResolvingFrames)
                    {
                        this.StatusText = String.Format("Kinect2Kit server @ {0} was resolving frames.", Kinect2Kit.ServerEndpoint);
                    }

                    await Task.Delay(30); // slow down
                }
            }

            // tracking
            resp = await Kinect2Kit.StartTrackingAsync();
            if (resp.IsSuccessful)
            {
                this.StatusText = String.Format("Kinect2Kit server @ {0} started tracking.", Kinect2Kit.ServerEndpoint);
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!this.trackingTaskPaused)
                    {
                        Kinect2KitTrackingResponse trackingResp = await Kinect2Kit.GetTrackingResult();
                        if (trackingResp.IsSuccessful)
                        {
                            this.Tracking_ResultArrived(trackingResp.Timestamp, trackingResp.Perspectives);
                        }
                    }
                }
            }
        }

        private void StopTrackingTask()
        {
            this.trackingTaskTokenSource.Cancel();
            try
            {
                this.trackingTask.Wait();
            }
            catch (Exception)
            {
                System.Diagnostics.Debug.WriteLine("exception when stopping task");
            }
            finally
            {
                this.trackingTaskTokenSource.Dispose();
            }
        }

        private void Tracking_ResultArrived(double timestamp, Dictionary<string, Kinect2KitPerspective> perspectives)
        {
            // Update status
            this.StatusText = String.Format("Tracking result @ {0}.", timestamp);

            using (DrawingContext dc = this.trackingImageDrawingGroup.Open())
            {
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));

                Kinect2KitPerspective viewingPerspective = perspectives.First(p => p.Key.Equals(this.selectedViewName)).Value;

                int penIndex = 0;

                foreach (Kinect2KitPerson person in viewingPerspective.People)
                {
                    Pen drawPen = this.bodyColors[penIndex++];

                    // TODO Kinect2Kit doesn't send back this data
                    //this.DrawClippedEdges(body, dc);

                    foreach (Kinect2KitSkeleton skeleton in person.Skeletons.Values)
                    {
                        IReadOnlyDictionary<JointType, Joint> joints = skeleton.Joints;
                        Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

                        foreach (JointType jointType in joints.Keys)
                        {
                            CameraSpacePoint position = joints[jointType].Position;
                            if (position.Z < 0)
                            {
                                position.Z = 0.1f;
                            }
                            DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                            jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                        }

                        this.DrawBody(joints, jointPoints, dc, drawPen);
                    }
                }

                this.trackingImageDrawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
            }
        }

        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {
            // Draw the bones
            foreach (var bone in Kinect2Bones.All)
            {
                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);
            }

            // Draw the joints
            foreach (JointType jointType in joints.Keys)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (trackingState == TrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], this.jointThickness, this.jointThickness);
                }
            }
        }

        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                drawPen = drawingPen;
            }

            drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
        }

        private void Track_Pause_Click(object sender, RoutedEventArgs e)
        {
            this.trackingTaskPaused = true;

            this.MenuItem_Track_Pause.IsEnabled = false;
            this.Button_Track_Pause.Visibility = Visibility.Collapsed;

            this.MenuItem_Track_Resume.IsEnabled = true;
            this.Button_Track_Resume.Visibility = Visibility.Visible;

            this.StatusText = "Tracking paused.";
        }

        private void Track_Resume_Click(object sender, RoutedEventArgs e)
        {
            this.trackingTaskPaused = false;

            this.MenuItem_Track_Pause.IsEnabled = true;
            this.Button_Track_Pause.Visibility = Visibility.Visible;

            this.MenuItem_Track_Resume.IsEnabled = false;
            this.Button_Track_Resume.Visibility = Visibility.Collapsed;

            this.StatusText = "Tracking resumed.";
        }

        private void Track_Stop_Click(object sender, RoutedEventArgs e)
        {
            this.MenuItem_Track_Pause.IsEnabled = false;
            this.Button_Track_Pause.Visibility = Visibility.Collapsed;

            this.MenuItem_Track_Resume.IsEnabled = false;
            this.MenuItem_Track_Resume.Visibility = Visibility.Collapsed;

            this.StopTrackingTask();

            this.MenuItem_Track_Start.IsEnabled = true;
            this.Button_Track_Start.Visibility = Visibility.Visible;

            this.StatusText = "Tracking stopped.";
        }

        #endregion
    }
}
