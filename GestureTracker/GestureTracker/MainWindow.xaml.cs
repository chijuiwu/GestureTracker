using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.ComponentModel;
using System.Net.Http;
using System.IO;
using System.Globalization;
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
        private Pen averageBonePen;
        private double jointThickness;

        private KinectSensor kinectSensor;
        private ColorFrameReader colorFrameReader = null;
        private WriteableBitmap colorBitmap = null;
        private CoordinateMapper coordinateMapper;
        private int displayWidth, displayHeight;

        private string statusText;
        public event PropertyChangedEventHandler PropertyChanged;

        private Task trackingTask;
        private CancellationTokenSource trackingTaskTokenSource;
        private bool trackingTaskPaused;

        private event TrackingResultHandler TrackingResultArrived;
        private delegate void TrackingResultHandler(double timestamp, Dictionary<string, Kinect2KitPerspective> perspectives);

        private bool viewAll = true;
        private MenuItem selectedKinectFOV;
        private List<MenuItem> kinectFOVMenuItems;

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
            this.averageBonePen = new Pen(Brushes.White, 6);
            this.jointThickness = 3;

            this.kinectSensor = KinectSensor.GetDefault();
            this.kinectSensor.Open();

            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;

            this.colorFrameReader = this.kinectSensor.ColorFrameSource.OpenReader();
            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);
            this.colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

            this.ClearTrackingImage();

            this.TrackingResultArrived += this.OnTrackingResultArrived;

            this.kinectFOVMenuItems = new List<MenuItem>();

            this.DataContext = this;

            this.InitializeComponent();

            this.StatusText = "Application started.";
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.colorFrameReader != null)
            {
                this.colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;
            }
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

            if (this.colorFrameReader != null)
            {
                this.colorFrameReader.Dispose();
                this.colorFrameReader = null;
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


        public ImageSource CameraImageSource
        {
            get
            {
                return this.colorBitmap;
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
                    this.StatusText = "Kinect2Kit setup was not loaded. The server is not available.";
                    return;
                }

                // TRACK
                this.Menu_Track.IsEnabled = true;
                this.MenuItem_Track_Start.Visibility = Visibility.Visible;
                this.Button_Track_Start.Visibility = Visibility.Visible;

                // VIEW
                foreach (Kinect2KitClientSetup view in Kinect2Kit.KinectClients)
                {
                    MenuItem kinectFOVMenuItem = new MenuItem();
                    kinectFOVMenuItem.Header = view.Name;
                    kinectFOVMenuItem.Click += this.KinectFOVMenuitem_Click;
                    this.Menu_View.Items.Add(kinectFOVMenuItem);
                    this.kinectFOVMenuItems.Add(kinectFOVMenuItem);
                }
                // Select first view
                MenuItem firstKinectFOV = this.kinectFOVMenuItems.ElementAt(0);
                firstKinectFOV.IsChecked = true;
                this.selectedKinectFOV = firstKinectFOV;

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
        private void View_All_Click(object sender, RoutedEventArgs e)
        {
            this.viewAll = true;

            this.MenuItem_View_All.IsChecked = true;
            this.MenuItem_View_Average.IsChecked = false;
        }

        private void View_Average_Click(object sender, RoutedEventArgs e)
        {
            this.viewAll = false;

            this.MenuItem_View_All.IsChecked = false;
            this.MenuItem_View_Average.IsChecked = true;
        }

        private void KinectFOVMenuitem_Click(object sender, RoutedEventArgs e)
        {
            this.selectedKinectFOV.IsChecked = false;

            MenuItem selectedKinectFov = sender as MenuItem;
            selectedKinectFov.IsChecked = true;
            this.selectedKinectFOV = selectedKinectFov;
        }

        #endregion

        #region TRACK
        private async void Track_Start_Click(object sender, RoutedEventArgs e)
        {
            // Disable File
            this.MenuItem_File_Open.IsEnabled = false;
            this.Button_File_Open.Visibility = Visibility.Collapsed;

            SetupSessionDialog setupSession = new SetupSessionDialog();
            setupSession.Owner = Application.Current.MainWindow;
            setupSession.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (setupSession.ShowDialog() == true)
            {
                // Disable Start while waiting from the sever
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

                    // Start tracking task
                    this.trackingTaskTokenSource = new CancellationTokenSource();
                    this.trackingTask = Task.Run(() => this.Track(this.trackingTaskTokenSource.Token), this.trackingTaskTokenSource.Token);
                }
                else
                {
                    // Re-enable Start
                    this.MenuItem_Track_Start.IsEnabled = true;
                    this.Button_Track_Start.Visibility = Visibility.Visible;

                    // Re-enable File
                    this.MenuItem_File_Open.IsEnabled = true;
                    this.Button_File_Open.Visibility = Visibility.Visible;

                    this.StatusText = String.Format("Kinect2Kit session {0} not started. Message: {1}.", sessionName, resp.ServerMessage);
                }
            }
            else
            {
                // Re-enable File
                this.MenuItem_File_Open.IsEnabled = true;
                this.Button_File_Open.Visibility = Visibility.Visible;

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

                try
                {
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();

                        if (this.trackingTaskPaused)
                        {
                            this.StatusText = "Tracking paused.";
                            continue;
                        }

                        Kinect2KitCalibrationResponse calibrationResp = await Kinect2Kit.GetCalibrationStatusAsync();
                        if (calibrationResp.Finished)
                        {
                            this.StatusText = String.Format("Kinect2Kit server @ {0} finished calibration.", Kinect2Kit.ServerEndpoint);
                            break;
                        }
                        else if (calibrationResp.AcquiringFrames)
                        {
                            if (calibrationResp.HasError)
                            {
                                this.StatusText = String.Format("Kinect2Kit server @ {0} was acquring frames. Error: {1}.", Kinect2Kit.ServerEndpoint, calibrationResp.Error);
                            }
                            else
                            {
                                this.StatusText = String.Format("Kinect2Kit server @ {0} was acquring frames. Required: {1}, Remained: {2}.", Kinect2Kit.ServerEndpoint, calibrationResp.RequiredFrames, calibrationResp.RemainedFrames);
                            }
                        }
                        else if (calibrationResp.ResolvingFrames)
                        {
                            this.StatusText = String.Format("Kinect2Kit server @ {0} was resolving frames.", Kinect2Kit.ServerEndpoint);
                        }

                        await Task.Delay(100); // slow down
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            // tracking
            resp = await Kinect2Kit.StartTrackingAsync();
            if (resp.IsSuccessful)
            {
                this.StatusText = String.Format("Kinect2Kit server @ {0} started tracking.", Kinect2Kit.ServerEndpoint);

                try
                {
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();

                        if (this.trackingTaskPaused)
                        {
                            this.StatusText = "Tracking paused.";
                            continue;
                        }

                        Kinect2KitTrackingResponse trackingResp = await Kinect2Kit.GetTrackingResultAsync();
                        if (trackingResp.IsSuccessful)
                        {
                            this.TrackingResultArrived(trackingResp.Timestamp, trackingResp.Perspectives);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    this.StatusText = "Tracking stopped.";
                    this.ClearTrackingImage();
                    return;
                }
            }
        }

        private async void StopTrackingTask()
        {
            this.trackingTaskTokenSource.Cancel();
            try
            {
                this.trackingTask.Wait();
                Kinect2KitSimpleResponse stopResp = await Kinect2Kit.StopSessionAsync();
            }
            catch (Exception)
            {
                System.Diagnostics.Debug.WriteLine("Exception when stopping task.");
            }
            finally
            {
                this.trackingTaskTokenSource.Dispose();
            }
        }

        private void ClearTrackingImage()
        {
            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                this.SkeletonCanvas.Children.Clear();
            }));
        }

        private void OnTrackingResultArrived(double timestamp, Dictionary<string, Kinect2KitPerspective> perspectives)
        {
            // Update status
            this.StatusText = String.Format("Tracking result @ {0}.", timestamp);

            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                this.SkeletonCanvas.Children.Clear();

                Kinect2KitPerspective viewingPerspective = perspectives.First(p => p.Key.Equals(this.selectedKinectFOV.Header)).Value;

                int penIndex = 0;

                foreach (Kinect2KitPerson person in viewingPerspective.People)
                {
                    Pen personPen = this.bodyColors[penIndex++];

                    if (viewAll)
                    {
                        foreach (Kinect2KitSkeleton skeleton in person.Skeletons.Values)
                        {
                            IReadOnlyDictionary<JointType, Kinect2KitJoint> joints = skeleton.Joints;
                            Dictionary<JointType, Point> jointPoints = this.GetJointsPoints(joints);
                            this.DrawBody(joints, jointPoints, personPen);
                        }
                    }

                    IReadOnlyDictionary<JointType, Kinect2KitJoint> averageJoints = person.AverageSkeleton;
                    Dictionary<JointType, Point> averageJointPoints = this.GetJointsPoints(averageJoints);

                    Pen averageSkeletonPen;

                    if (viewAll)
                    {
                        averageSkeletonPen = this.averageBonePen;
                    }
                    else
                    {
                        // show person color in average skeleton view
                        averageSkeletonPen = personPen;
                    }

                    this.DrawBody(averageJoints, averageJointPoints, averageSkeletonPen);
                }

                this.trackingImageDrawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
            }));
        }

        private void Reader_ColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            // ColorFrame is IDisposable
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            {
                if (colorFrame != null)
                {
                    FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                    using (KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer())
                    {
                        this.colorBitmap.Lock();

                        // verify data and write the new color frame data to the display bitmap
                        if ((colorFrameDescription.Width == this.colorBitmap.PixelWidth) && (colorFrameDescription.Height == this.colorBitmap.PixelHeight))
                        {
                            colorFrame.CopyConvertedFrameDataToIntPtr(
                                this.colorBitmap.BackBuffer,
                                (uint)(colorFrameDescription.Width * colorFrameDescription.Height * 4),
                                ColorImageFormat.Bgra);

                            this.colorBitmap.AddDirtyRect(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight));
                        }

                        this.colorBitmap.Unlock();
                    }
                }
            }
        }

        // Returns color space point
        private Dictionary<JointType, Point> GetJointsPoints(IReadOnlyDictionary<JointType, Kinect2KitJoint> joints)
        {
            Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

            foreach (JointType jointType in joints.Keys)
            {
                CameraSpacePoint position = joints[jointType].CameraSpacePoint;
                if (position.Z < 0)
                {
                    position.Z = 0.1f;
                }

                Point point = new Point();

                DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                ColorSpacePoint colorSpacePoint = this.coordinateMapper.MapCameraPointToColorSpace(position);

                point.X = float.IsInfinity(colorSpacePoint.X) ? 0 : colorSpacePoint.X;
                point.Y = float.IsInfinity(colorSpacePoint.Y) ? 0 : colorSpacePoint.Y;

                if (point.X == 0 && point.Y == 0)
                {
                    continue;
                }

                jointPoints[jointType] = point;
            }

            return jointPoints;
        }

        private void DrawBody(IReadOnlyDictionary<JointType, Kinect2KitJoint> joints, IDictionary<JointType, Point> jointPoints, Pen drawingPen)
        {
            // Draw the bones
            foreach (var bone in Kinect2Bones.All)
            {
                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingPen);
            }

            // Draw the joints
            foreach (JointType jointType in joints.Keys)
            {
                if (!jointPoints.ContainsKey(jointType))
                {
                    continue;
                }

                // draw only tracked joints
                if (joints[jointType].TrackingState != TrackingState.Tracked)
                {
                    continue;
                }

                Ellipse joint = new Ellipse
                {
                    Fill = this.trackedJointBrush,
                    Width = 30,
                    Height = 30,
                };

                Point point = jointPoints[jointType];
                Canvas.SetLeft(joint, point.X - joint.Width / 2);
                Canvas.SetTop(joint, point.Y - joint.Height / 2);
                this.SkeletonCanvas.Children.Add(joint);
            }
        }

        private void DrawBone(IReadOnlyDictionary<JointType, Kinect2KitJoint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, Pen drawingPen)
        {
            if (!joints.ContainsKey(jointType0) || !joints.ContainsKey(jointType1))
            {
                return;
            }

            Kinect2KitJoint joint0 = joints[jointType0];
            Kinect2KitJoint joint1 = joints[jointType1];

            // draw only bones consisting of tracked joints
            if (joint0.TrackingState != TrackingState.Tracked || joint1.TrackingState != TrackingState.Tracked)
            {
                return;
            }

            if (!jointPoints.ContainsKey(jointType0) || !jointPoints.ContainsKey(jointType1))
            {
                return;
            }

            Point point1 = jointPoints[jointType0];
            Point point2 = jointPoints[jointType1];

            Line bone = new Line
            {
                Stroke = drawingPen.Brush,
                StrokeThickness = 10,
                X1 = point1.X,
                Y1 = point1.Y,
                X2 = point2.X,
                Y2 = point2.Y
            };
            this.SkeletonCanvas.Children.Add(bone);
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
            this.Button_Track_Resume.Visibility = Visibility.Collapsed;

            this.MenuItem_Track_Stop.IsEnabled = false;
            this.Button_Track_Stop.Visibility = Visibility.Collapsed;

            this.StopTrackingTask();

            this.MenuItem_Track_Start.IsEnabled = true;
            this.Button_Track_Start.Visibility = Visibility.Visible;

            this.MenuItem_File_Open.IsEnabled = true;
            this.Button_File_Open.Visibility = Visibility.Visible;

            this.StatusText = "Tracking stopped.";
        }

        #endregion

        #region Screenshot
        private void Screenshot_Click(object sender, RoutedEventArgs e)
        {
            Rect bounds = VisualTreeHelper.GetDescendantBounds(this.GestureTrackerViewbox);
            RenderTargetBitmap renderTarget = new RenderTargetBitmap((Int32)bounds.Width, (Int32)bounds.Height, 96, 96, PixelFormats.Pbgra32);

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                VisualBrush visualBrush = new VisualBrush(this.GestureTrackerViewbox);
                context.DrawRectangle(visualBrush, null, new Rect(new Point(), bounds.Size));
            }

            renderTarget.Render(visual);

            PngBitmapEncoder bitmapEncoder = new PngBitmapEncoder();
            bitmapEncoder.Frames.Add(BitmapFrame.Create(renderTarget));

            string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);
            string myPhotos = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string path = System.IO.Path.Combine(myPhotos, "GestureTracker-" + time + ".png");

            // write the new file to disk
            try
            {
                // FileStream is IDisposable
                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    bitmapEncoder.Save(fs);
                }

                this.StatusText = string.Format("Saved screenshot to {0}", path);
            }
            catch (IOException)
            {
                this.StatusText = string.Format("Failed to write screenshot to {0}", path);
            }
        }
        #endregion
    }
}
