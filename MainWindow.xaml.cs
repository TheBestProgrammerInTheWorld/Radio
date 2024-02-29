using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NAudio.Wave;
using Newtonsoft.Json;
using static Radio.MainWindow;
using EchoPrintSharp;
using System.Threading;
using System.Windows.Threading;
using System.Timers;

namespace Radio
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer timer;
        private CodeGen codegen;
        private Thread detectionThread;

        bool displayFileList = true;
        bool displayStationList = true;

        private string jsonpath = @"..\..\AudioStuff\Stations\Stations.json";
        private string directoryPath = @"..\..\AudioStuff\Files";
        private string radioFiles = @"..\..\AudioStuff\Sataions\FileBasedStations";

        public MainWindow()
        {
            //timer = new DispatcherTimer();
            //timer.Interval = TimeSpan.FromSeconds(5);
            //timer.Tick += TimeElapsed;
            timer.Start();


            InitializeComponent();

            PopulateTreeView(audioTreeView, directoryPath);
            PopulateListView();

        }


// WINDOW MANIPULATION
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) { 
                DragMove();
            }
        }

        private void ButtonMinimize_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.MainWindow.WindowState = WindowState.Minimized;
        }

        private void ButtonMaximize_Click(Object sender, RoutedEventArgs e)
        {
            var winstate = Application.Current.MainWindow.WindowState;

            if (winstate != WindowState.Maximized)
            {
                winstate = WindowState.Maximized;
            } 
            else 
            {
                winstate = WindowState.Normal;
            }

            Application.Current.MainWindow.WindowState = winstate;
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }



// AUDIO CONTROLS
        private WaveOutEvent waveOut;
        private MediaFoundationReader audioStream;
        private bool isConnected = false;
        private string link;
        double literalVolume;

        private void PlayStream_Click(object sender, RoutedEventArgs e)
        {
            _Play();
        }

        private void StopStream_Click(object sender, RoutedEventArgs e)
        {
            _Stop();
        }

        private void Volume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            literalVolume = (double)e.NewValue;
            VolumeNumber.Content = (double)e.NewValue;
            AdjustVolume(e.NewValue);
        }

        private void AdjustVolume(double volume)
        {
            if (waveOut !=null)
                waveOut.Volume = (float)volume/100;
        }



// ECHOPRINT 
        private void TimeElapsed(object sender, ElapsedEventArgs e)
        {
            //string code = codegen.Generate();
        }




// GUI TOUCHUPS
        private void URL_GotFocus(object sender, RoutedEventArgs e)
        {
            if (URL.Text == "URL")
                URL.Text = "";
        }
        private void URL_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isConnected == true && StationName.Text != null)
            {
                AddStationBTN.Foreground = Brushes.White;
            }
        }
        private void StationName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isConnected == true && (URL.Text != null || URL.Text != "URL"))
            {
                AddStationBTN.Foreground = Brushes.White;
            }
        }



// TREEVIEW MANAGEMENT
        //open and close tabs
        private void OpenCloseFiles(object sender, RoutedEventArgs e)
        {
            if (displayFileList)
            {
                FileView.Height = new GridLength(0);
                showFiles.Content = "+ Audio Philes";
                displayFileList = false;
            }
            else
            {
                FileView.Height = GridLength.Auto;
                showFiles.Content = "- Audio Philes";
                displayFileList = true;
            }
        }
        private void OpenCloseStations(object sender, RoutedEventArgs e)
        {
            if (displayStationList)
            {
                StationView.Height = new GridLength(0);
                showStation.Content = "+ Radio Stations";
                displayStationList = false;
            }
            else
            {
                StationView.Height = GridLength.Auto;
                showStation.Content = "- Radio Stations";
                displayStationList = true;
            }
        }


        //Displaying audio tree view
        private void PopulateTreeView(TreeView treeView, string directoryPath)
        {
            DirectoryInfo rootDirectory = new DirectoryInfo(directoryPath);
            PopulateTreeViewItems(treeView, rootDirectory);
        }

        private void PopulateTreeViewItems(ItemsControl parentItem, DirectoryInfo directoryInfo)
        {
            foreach (var directory in directoryInfo.GetDirectories())
            {
                TreeViewItem subItem = new TreeViewItem();
                subItem.Header = directory.Name;
                parentItem.Items.Add(subItem);

                PopulateTreeViewItems(subItem, directory);
            }

            foreach (var file in directoryInfo.GetFiles())
            {
                TreeViewItem fileItem = new TreeViewItem(); fileItem.Header = file.Name;
                parentItem.Items.Add(fileItem);
            }
        }


        //Dropping files into audio tree view
        private void Dir_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("works");
        }


        //Adding
        private void AddStation_Click(object sender, RoutedEventArgs e)
        {
            if (isConnected == true && StationName.Text != null)
            {
                Station newStation = new Station
                {
                    Name = StationName.Text,
                    Volume = literalVolume,
                    Url = link,
                    Date = DateTime.Now
                };

                var stations = CallJSON();

                stations.Add(newStation);

                string updatedJson = JsonConvert.SerializeObject(stations, Formatting.Indented);

                File.WriteAllText(jsonpath, updatedJson);

                PopulateListView();

                AddStationBTN.Foreground = Brushes.Gray;
            }
        }

        private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Retrieve the selected station
            var selectedStation = (Station)FileListView.SelectedItem;

            URL.Text = selectedStation.Url;
            _Play();
        }


        //Modify existing station name
        private void EditStationName(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var stationName = FindVisualChild<TextBox>(button.Parent);

            if (stationName != null)
            {
                stationName.IsReadOnly = false;
                stationName.Focus();
            }
        }
        private void StationName_Focus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            textBox.SelectAll();
        }
        private void StationName_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            textBox.IsReadOnly = true;

            // Assuming you have a way to save changes back to the JSON, you can do that here
        }


        // Helper method to find child controls
        private childItem FindVisualChild<childItem>(DependencyObject obj)
            where childItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is childItem)
                    return (childItem)child;
                else
                {
                    childItem childOfChild = FindVisualChild<childItem>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }


// CALL BACKS
// everything i call multiple times i put here
        private void _Play()
        {
            link = URL.Text;

            _Stop();

            waveOut = new WaveOutEvent();

            try
            {
                audioStream = new MediaFoundationReader(link);

                waveOut.Init(audioStream);
                waveOut.Volume = (float)Volume.Value / 100;
                waveOut.Play();

                isConnected = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                isConnected = false;
            }
        }
        private void _Stop()
        {
            if (waveOut != null)
            {
                waveOut.Stop();
                waveOut.Dispose();
                waveOut = null;
                isConnected = false;
            }
        }
        private void PopulateListView()
        {
            var stations = CallJSON();
            FileListView.ItemsSource = stations;
        }
        private List<Station> CallJSON()
        {
            string jsonContent = File.ReadAllText(jsonpath);

            List<Station> stations = JsonConvert.DeserializeObject<List<Station>>(jsonContent);

            return stations;
        }

    // CLASSES
        public class Station
        {
            public string Name { get; set; }
            public double Volume { get; set; }
            public string Url { get; set; }
            public DateTime Date { get; set; }
        }

        
    }
}
