using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using EnumerableExtensions;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;
using ListBox = System.Windows.Controls.ListBox;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using NAudio.Wave;
using System.Windows.Ink;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;


//MAde waveOut made in cstrctr and siposed on wnd closed. now is only initialised with a new audio file stream

//TODO fancy image in middle? later.
//TODO reorder queue #####
//TODO miniplayer
//this is for v2. enjoy :D

//v3 performace upgrade

namespace NileBeats
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private bool shouldTryLog;

        public bool CanLog { get { return shouldTryLog; } }

        public StreamWriter logFile;

        private string[] audioFileExts = { ".mp3", ".wav" };

        private string openedFolderPath;

        public string OpenedFolderPath
        {
            get { return openedFolderPath; }
            set { settings.RecentFolder = value; openedFolderPath = value; OnPropertyChanged(); }
        }

        private List<Song> openedFolderFiles;

        public List<Song> OpenedFolderFiles
        {
            get
            {
                return openedFolderFiles;
            }
            set
            {
                openedFolderFiles = value;
                OnPropertyChanged();
            }
        }

        private AppSettings settings;

        private ObservableCollection<WrapSong> queuedSongs;

        public ObservableCollection<WrapSong> QueuedSongs { get { return queuedSongs; } set { queuedSongs = value; OnPropertyChanged(); } }

        private WrapSong currentSong;

        public WrapSong CurrentSong { get { return currentSong; } 
            set {
                
                currentSong = value; OnPropertyChanged();
            } }

        private Storyboard songTitleSb;

        private DoubleAnimation songTitleAnim;

        private WaveOutEvent waveOut;

        private AudioFileReader audioFile;

        private bool isPlaying;

        public bool IsPlaying { get { return isPlaying; } set { isPlaying = value; OnPropertyChanged(); MakePlayPauseButtonAnimate(); } }

        private DoubleAnimation opaqueAnim;

        private DoubleAnimation transAnim;

        private DispatcherTimer updateDurTimer;

        bool closing = false;

        bool draggingSlider = false;

        private string lastSearchText;

        private DispatcherTimer searchTimer;

        private bool loopSong;

        public bool LoopSong { get { return loopSong; } set { loopSong = value; OnPropertyChanged(); } }

        private bool loopQueue;

        public bool LoopQueue { get { return loopQueue; } set { loopQueue = value; OnPropertyChanged(); } }

        public float Volume { get { if (settings != null) { return settings.Volume; } else { return 0; } } set { settings.Volume = value; OnPropertyChanged(); } }

        private void SetPlaySong(WrapSong song)
        {
            if(waveOut != null && waveOut.PlaybackState == PlaybackState.Playing)
                waveOut.Stop();
            //waveOut?.Dispose();
            audioFile?.Dispose();

            if (CurrentSong != null)
                CurrentSong.IsPlaying = false;

            try
            {
                if(song != null)
                {
                    audioFile = new AudioFileReader(song.Song.Path);
                    //waveOut = new WaveOutEvent();
                    //waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
                    waveOut.Init(audioFile);
                    waveOut.Volume = settings.Volume;
                    if (IsPlaying)
                        waveOut.Play();
                    songDurSlider.IsEnabled = true;
                    songDurSlider.Value = 0;
                    updateDurTimer.Start();
                }
                else
                {
                    songDurSlider.IsEnabled = false;
                    songDurSlider.Value = 0;
                    updateDurTimer?.Stop();
                }

                CurrentSong = song;

                if(song != null)
                {
                    songTitleSb.Stop();

                    songTitleSb.Children.Remove(songTitleAnim);

                    songTitleAnim = new DoubleAnimation();
                    songTitleAnim.From = marqueeCv.ActualWidth;
                    songTitleAnim.To = -CurrentSong.Song.Name.Length * 18;
                    songTitleAnim.Duration = new Duration(new TimeSpan(0, 0, 10));
                    songTitleAnim.RepeatBehavior = RepeatBehavior.Forever;

                    currentSongTitle.BeginAnimation(Canvas.LeftProperty, songTitleAnim);

                    songTitleSb.Children.Add(songTitleAnim);

                    CurrentSong.IsPlaying = true;
                }
            }
            catch(Exception e)
            {
                Log(e.Message); Log(e.StackTrace);
                Info("Error beginning song playback: " + e.Message);
            }
        }

        private void WaveOut_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            if(e.Exception != null)
            {
                Log(e.Exception.Message);
                Log(e.Exception.StackTrace);
                Info("Audio playback failed. Reason: " + e.Exception.Message);
            }
            else if(!closing && audioFile != null && audioFile.CanRead)
            {
                try
                {
                    if (audioFile.Position == audioFile.Length)
                    {
                        if(LoopSong)
                        {
                            IsPlaying = true;
                            audioFile.SetPosition(0);
                            waveOut.Play();
                        }
                        else
                        {
                            var index = QueuedSongs.IndexOf(CurrentSong);
                            if (QueuedSongs.Count - 1 > index)
                            {
                                SetPlaySong(QueuedSongs[index + 1]);
                                IsPlaying = true;
                                waveOut.Play();
                            }
                            else if(LoopQueue)
                            {
                                SetPlaySong(QueuedSongs[0]);
                                IsPlaying = true;
                                waveOut.Play();
                            }
                            else
                            {
                                IsPlaying = false;
                            }
                        }
                    }
                }
                catch(NullReferenceException)
                {
                }
                catch(Exception ex)
                {
                    Log(ex.Message);
                    Log(ex.StackTrace);
                    Info(ex.Message);
                }
            }
        }

        private void Info(string message)
        {
            MessageBox.Show(message, "NileBeats Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private bool Log(string message)
        {
            if (shouldTryLog)
            {
                try
                {
                    logFile.WriteLine(message);
                    return true;
                }
                catch (Exception e)
                {
                    logFile?.Dispose();
                    logFile = null;
                    Info("Unable to log errors anymore. Reason: " + e.Message);
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private void MakePlayPauseButtonAnimate()
        {
            if(IsPlaying)
            {
                //arrow1.BeginAnimation(System.Windows.Shapes.Path.OpacityProperty, transAnim);
                //arrow2.BeginAnimation(System.Windows.Shapes.Path.OpacityProperty, transAnim);
                //arrow3.BeginAnimation(System.Windows.Shapes.Path.OpacityProperty, transAnim);
                line1.BeginAnimation(System.Windows.Shapes.Path.OpacityProperty, opaqueAnim);
                line2.BeginAnimation(System.Windows.Shapes.Path.OpacityProperty, opaqueAnim);
                triangle.BeginAnimation(System.Windows.Shapes.Polygon.OpacityProperty, transAnim);
            }
            else
            {
                //arrow1.BeginAnimation(System.Windows.Shapes.Path.OpacityProperty, opaqueAnim);
                //arrow2.BeginAnimation(System.Windows.Shapes.Path.OpacityProperty, opaqueAnim);
                //arrow3.BeginAnimation(System.Windows.Shapes.Path.OpacityProperty, opaqueAnim);
                line1.BeginAnimation(System.Windows.Shapes.Path.OpacityProperty, transAnim);
                line2.BeginAnimation(System.Windows.Shapes.Path.OpacityProperty, transAnim);

                triangle.BeginAnimation(System.Windows.Shapes.Polygon.OpacityProperty, opaqueAnim);
                
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = this;
            
            try
            {
                logFile = new StreamWriter("NileBeatsErrorLogs.txt", true);
                logFile.AutoFlush = true;
                shouldTryLog = true;
            }
            catch (Exception e)
            {
                shouldTryLog = false;
                logFile?.Dispose();
                logFile = null;
                Info("Unable to log errors. Reason: " + e.Message);
            }

            bool tryOpenRecentFolder = true;

            try
            {
                settings = new AppSettings("NileBeatsSettings.txt");
                if (settings.Volume > 1)
                    settings.Volume = 1;
            }
            catch (Exception e)
            {
                settings = new AppSettings();
                tryOpenRecentFolder = false;
                Log(e.Message);
                Log(e.StackTrace);
                Info("Could not read the settings from the file. Reset the settings. Reason: " + e.Message);
            }

            OnPropertyChanged(nameof(Volume));

            OpenedFolderPath = settings.RecentFolder;


            if (tryOpenRecentFolder)
            {
                try
                {
                    GetFilesInOpenedFolder();
                }
                catch (Exception e)
                {
                    Log(e.Message);
                    Log(e.StackTrace);
                }
            }

            QueuedSongs = new ObservableCollection<WrapSong>();
            songTitleSb = new Storyboard();
            Timeline.SetDesiredFrameRate(songTitleSb, 60);
            Canvas.SetLeft(currentSongTitle, -currentSongTitle.ActualWidth);

            opaqueAnim = new DoubleAnimation(1, new Duration(new TimeSpan(0, 0, 1)));

            transAnim = new DoubleAnimation(0 , new Duration(new TimeSpan(0, 0, 1)));

            IsPlaying = false;

            if(IsPlaying)
            {
                triangle.Opacity = 0;
                line1.Opacity = 1;
                line2.Opacity = 1;
            }
            else
            {
                triangle.Opacity = 1;
                line1.Opacity = 0;
                line2.Opacity = 0;
            }

            SetPlaySong(null);
            playEllipse.Fill = Brushes.Transparent;
            updateDurTimer = new DispatcherTimer();
            updateDurTimer.Interval = new TimeSpan(0, 0, 0, 0, 300);
            updateDurTimer.Tick += (s, e) =>
            {
                if (!draggingSlider)
                {
                    if (audioFile != null && audioFile.CanRead)
                    {
                        songDurTb.Text = audioFile.CurrentTime.ToString("%d\\.hh\\:mm\\:ss") + "/" + audioFile.TotalTime.ToString("%d\\.hh\\:mm\\:ss");
                        songDurSlider.Value = audioFile.CurrentTime.TotalSeconds / audioFile.TotalTime.TotalSeconds;
                    }
                    else if (songDurTb.Text != "")
                        songDurTb.Text = "";
                }
            };

            searchTimer = new DispatcherTimer();
            searchTimer.Interval = new TimeSpan(100 * TimeSpan.TicksPerMillisecond);
            searchTimer.Tick += (s, e)=>
                {
                if(!(songSearchBox.Text == lastSearchText))
                    {
                        lastSearchText = songSearchBox.Text;
                        GetFilesInOpenedFolder(lastSearchText);
                    }
            };
            searchTimer.Start();

            waveOut = new WaveOutEvent();
            waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
        }

        //does the search match?
        private bool searchMatch(string str, string text)
        {
            return text == "" || str.ToLower().Contains(text);
        }

        // attempts to fill the files in opened folder collection with the relevant data
        /// <summary>
        /// catch the exception whe ncalling this
        /// </summary>
        private void GetFilesInOpenedFolder(string query = "")
        {
            if(OpenedFolderPath != null)
            {
                query = query.ToLower();
                List<Song> songs;

                try
                {
                    var files = from file in Directory.EnumerateFiles(OpenedFolderPath)
                                where audioFileExts.Contains(Path.GetExtension(file)) && searchMatch(file, query)
                                select file;

                    songs = new List<Song>(files.Count());

                    foreach (var file in files)
                    {
                        try
                        {
                            Song s = new Song(file);
                            songs.Add(s);
                        }
                        catch (Exception e)
                        {
                            Log(e.Message);
                            Log(e.StackTrace);
                        }
                    }
                }
                catch (Exception ex)
                {
                    songs = new List<Song>();
                    Log(ex.Message);
                }

                OpenedFolderFiles = songs;
            }
            else
            {
                OpenedFolderFiles = new List<Song>();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void SongFinished(object sender, EventArgs e)
        {

        }

        private void mainWnd_Closed(object sender, EventArgs e)
        {
            closing = true;

            waveOut?.Stop();
            audioFile?.Dispose();
            waveOut?.Dispose();

            try
            {
                settings.SaveSettings("NileBeatsSettings.txt");
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                Log(ex.StackTrace);
                Info("Could not save the settings. Reason: " + ex.Message);
            }
            logFile?.Dispose();
        }

        //open folder btn
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            var res = dialog.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK)
            {
                OpenedFolderPath = dialog.SelectedPath;
                try
                {
                    songSearchBox.Text = "";
                    await Task.Run(()=>GetFilesInOpenedFolder());
                }
                catch (AggregateException ae)
                {
                    var ex = ae.Flatten();
                    Log("Aggregate exception in getting folder files:");
                    foreach (var exx in ex.InnerExceptions)
                    {
                        Log(exx.Message);
                        Log(exx.StackTrace);
                    }

                    Info("Getting the files in the fodler failed. See the error log for details.");
                }
                catch (Exception ex)
                {
                    Log(ex.Message);
                    Log(ex.StackTrace);
                    Info("The operation failed. Reason: " + ex.Message);
                }
            }
        }

        //clear queue
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            WrapSong song = CurrentSong;

            QueuedSongs.Clear();

            if(song != null)
            {
                QueuedSongs.Add(song);
            }
            else
            {
                SetPlaySong(null);
                songDurTb.Text = "";
            }
        }

        //shuffle and add songs to queue
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var songs = new List<Song>(OpenedFolderFiles);
            songs.Shuffle();
            if (CurrentSong != null)
                SetPlaySong(null);
            QueuedSongs = new ObservableCollection<WrapSong>();
            foreach (var item in songs)
            {
                QueuedSongs.Add(new WrapSong(item));
            }

            if(QueuedSongs.Count > 0)
                SetPlaySong(QueuedSongs[0]);
        }

        //opened folder songs item d click?
        private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt))
            {
                ListBoxItem item = sender as ListBoxItem;
                item.IsSelected = !item.IsSelected;
            }
        }

        //list double click
        private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var lb = sender as ListBox;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                var prevSelection = new List<object>(lb.SelectedItems.Cast<object>());

                lb.SelectAll();

                foreach (var item in prevSelection)
                {
                    lb.SelectedItems.Remove(item);
                }
            }
        }

        //lb item mosue enter
        private void ListBox_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
        }

        //queue selected click
        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            if(openedFolderSongsLb.SelectedItem != null)
            {
                var selected = openedFolderSongsLb.SelectedItems.Cast<Song>();

                foreach (var item in selected)
                {
                    QueuedSongs.Add(new WrapSong(item));
                }
            }
        }

        //unqueeu selection
        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            if(queuedSongsLb.SelectedItem != null)
            {
                var items = new List<WrapSong>(queuedSongsLb.SelectedItems.Cast<WrapSong>());
                foreach (var item in items)
                {
                    QueuedSongs.Remove(item);
                    if(item == CurrentSong)
                    {
                        songDurTb.Text = "";
                        SetPlaySong(null);
                    }
                }
            }
        }

        //pause play click
        private void Path_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if(waveOut != null && CurrentSong != null)
            {
                if(!IsPlaying)
                {
                    waveOut.Play();
                }
                else
                {
                    waveOut.Stop();
                }
            }
            IsPlaying = !IsPlaying;
        }

        //lb item right mouse click
        private void ListBoxItem_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = sender as ListBoxItem;

            //if mouse is in queued songs lb
            if (queuedSongsLb.IsMouseOver)
            {
                var songIndex = queuedSongsLb.ItemContainerGenerator.IndexFromContainer(item);
                SetPlaySong(QueuedSongs[songIndex]);
            }
            //else is in opened fodler songs lb?
            else
            {
                var songIndex = openedFolderSongsLb.ItemContainerGenerator.IndexFromContainer(item);
                var song = OpenedFolderFiles[songIndex];
                QueuedSongs.Insert(QueuedSongs.Count, new WrapSong(song));
                SetPlaySong(QueuedSongs[QueuedSongs.Count - 1]);
            }
        }

        //song dur slider drag end
        private void songDurSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            draggingSlider = false;
            var slider = sender as Slider;
            try
            {
                //werid bUG wTFTFTTFFTFT
                if(slider.Value == 1)
                {
                    audioFile.SetPosition(audioFile.Length - 1);
                }
                else
                {
                    audioFile.SetPosition(audioFile.TotalTime.TotalSeconds * slider.Value);
                }
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                Log(ex.StackTrace);
            }
        }

        //song dur slider drag start
        private void songDurSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            draggingSlider = true;
            IsPlaying = false;
            waveOut.Pause();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        //replay button click
        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            if(audioFile != null && waveOut != null)
            {
                try
                {
                    audioFile.SetPosition(0);
                    waveOut.Play();
                    IsPlaying = true;
                }
                catch(Exception ex)
                {
                    if (!(ex is NullReferenceException))
                    {
                        Log(ex.Message);
                        Log(ex.StackTrace);
                        Info("Could not replay the video. Reason: " + ex.Message);
                    }
                }
            }
        }

        //skip button click
        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            var index = QueuedSongs.IndexOf(CurrentSong);
            
            if(QueuedSongs.Count > index + 1)
            {
                SetPlaySong(QueuedSongs[index + 1]);
            }
            else
            {
                waveOut?.Pause();
                IsPlaying = false;
                Info("Cannot skip song. Reason: End of queue.");
            }
        }

        //volume slider value cvhanged
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            settings.Volume = (float)e.NewValue;
            if (waveOut != null)
            {
                waveOut.Volume = (float)e.NewValue;
            }
        }

        //save ququq asynxc??asdasdsad v2q  call it async??
        private void SaveQueue(string savePath, string rootPath, IEnumerable<string> paths)
        {
            StreamWriter writer = null;
            try
            {
                writer = new StreamWriter(savePath);
                writer.WriteLine("NileBeats v1");
                writer.WriteLine(rootPath);
                foreach (var item in paths)
                {
                    writer.WriteLine(Path.GetFileName(item));
                }
            }
            finally
            {
                writer?.Dispose();
            }
        }


        //save queeu btn clcik
        private async void Button_Click_7(object sender, RoutedEventArgs e)
        {
            if(QueuedSongs.Count < 1)
            {
                Info("Queue it empty. Will not save it.");
            }
            else
            {
                var fileDialog = new SaveFileDialog();
                fileDialog.Filter = "Text files (*.txt)|*.txt;";
                fileDialog.Title = "Save queue";
                var result = fileDialog.ShowDialog();
                if(result == true)
                {
                    try
                    {
                        var paths = from song in QueuedSongs select song.Song.Path;
                        var name = fileDialog.FileName;
                        var path = OpenedFolderPath;
                        await Task.Run(() => {
                            SaveQueue(name, path, paths);
                        });
                        Info("Saving the queue to " + name + " was successful.");
                    }
                    catch (AggregateException ae)
                    {
                        var ex = ae.Flatten();
                        Log("Aggregate exception in saving queue:");
                        foreach (var exx in ex.InnerExceptions)
                        {
                            Log(exx.Message);
                            Log(exx.StackTrace);
                        }

                        Info("Saving of the queue failed. See the error log for details.");
                    }
                    catch(Exception ex)
                    {
                        Log(ex.Message);
                        Log(ex.StackTrace);
                        Info("Saving of the queue failed. Reason: " + ex.Message);
                    }
                }
                else
                {
                    Info("Saving of queue cancelled.");
                }
            }
        }

        private void LoadQueue(string path)
        {
            StreamReader reader = null;
            try
            {
                reader = new StreamReader(path);
                var v = reader.ReadLine();
                if (v != "NileBeats v1")
                    throw new Exception("The queue was made by another version of NileBeats.");
                var folder = reader.ReadLine();
                var openedFolderSongs = new List<Song>();
                string songPath;
                while (!reader.EndOfStream)
                {
                    songPath = reader.ReadLine();
                    openedFolderSongs.Add(new Song(Path.Combine(folder, songPath)));
                }

                if (openedFolderSongs.Count < 1)
                    throw new Exception("Queue is empty.");

                var queuedSongs = new ObservableCollection<WrapSong>(
                                    from song in openedFolderSongs
                                    select new WrapSong(song)
                                    );

                Dispatcher.Invoke(() =>
                {
                    OpenedFolderFiles = openedFolderSongs;
                    OpenedFolderPath = folder;
                    //clear queue button click
                    Button_Click_1(null, null);
                    QueuedSongs = queuedSongs;

                    Info("Loaded the queue at " + path + " successfully.");
                });
            }
            finally
            {
                reader?.Dispose();
            }
        }

        //load queeu btn click
        private async void Button_Click_8(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Open queue";
            var res = dialog.ShowDialog();
            if(res == true)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        LoadQueue(dialog.FileName);
                    });
                }
                catch (AggregateException ae)
                {
                    var ex = ae.Flatten();
                    Log("Aggregate exception in loading queue at " + dialog.FileName + " :");
                    foreach (var exx in ex.InnerExceptions)
                    {
                        Log(exx.Message);
                        Log(exx.StackTrace);
                    }

                    Info("Loading of the queue at " + dialog.FileName + " failed. See the error log for details.");
                }
                catch (Exception ex)
                {
                    Log(ex.Message);
                    Log(ex.StackTrace);
                    Info("Loading of the queue at " + dialog.FileName + " failed. Reason: " + ex.Message);
                }
            }
            else
            {
                Info("Loading of queue was cancelled.");
            }
        }

        //help btn click
        private void Button_Click_9(object sender, RoutedEventArgs e)
        {
            Info(@"Ask The Nile, he was too lazy to write anything here ¯\_(ツ)_/¯");
        }
    }
}
