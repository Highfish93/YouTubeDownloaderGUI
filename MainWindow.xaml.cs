using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos.Streams;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace YouTubeDownloaderGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<VideoInfo> VideoInfos = new ObservableCollection<VideoInfo>();
        public ObservableCollection<VideoInfo> VideoQueue = new ObservableCollection<VideoInfo>();

        public MainWindow()
        {
            InitializeComponent();
        }

        public class VideoInfo
        {
            public string? url { get; set; }
            public string? title { get; set; }
            public BitmapImage? Thumbnail { get; set; }
            public string? playlistTitle { get; set; }
            public string? author { get; set; }
            public string? duration { get; set; }
            public DateTimeOffset? uploadDate { get; set; }
            public string? Keywords { get; set; }
            public bool downloadChecked { get; set; } = true;
            public bool likeCheck { get; set; }
            public string? state { get; set; }
            public Brush stateColor { get; set; }
            public double progress { get; set; }
        }
        public static string RemoveForbiddenChars(string text)
        {
            List<string> forbidden = new List<string> { @"~", @"\", "#", "%", "&", "*", ":", "<", ">", "?", "/", @"\", @"\", "{", "|", "}", "\"" };

            foreach (string c in forbidden)
            {
                text = text.Replace(c, "");
            }
            text = text.Replace("  ", " ");
            return text;
        }


        public async void GetUrlFromTextBox()
        {
            VideoInfos.Clear();
            ListViewVideos.ItemsSource = null;
            YoutubeClient youtube = new YoutubeClient();
            if (tb_Link.Text.Contains("watch") | tb_Link.Text.Contains("shorts"))
            {

                try
                {
                    var video = await youtube.Videos.GetAsync(tb_Link.Text);

                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(video.Thumbnails[0].Url);
                    bitmapImage.EndInit();

                    string keyW = "";
                    int index = 0;
                    foreach (var h in video.Keywords)
                    {
                        if (index == video.Keywords.Count - 1)
                            keyW += h;
                        else
                            keyW += h + ", ";
                        index++;
                    }

                    VideoInfos.Add(new VideoInfo { url = tb_Link.Text, title = video.Title, Thumbnail = bitmapImage, author = video.Author.ChannelTitle, Keywords = keyW, duration = video.Duration.Value.ToString(), uploadDate = video.UploadDate });
                }
                catch (System.Net.Http.HttpRequestException ex)
                {
                    var result = MessageBox.Show("No Connection 2 Seconds Retry after OK", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    if (result == MessageBoxResult.OK)
                    {
                        await Task.Delay(2000);
                        GetUrlFromTextBox();
                    }
                }
            }
            else if (tb_Link.Text.Contains("playlist?list="))
            {
                var pl = await youtube.Playlists.GetAsync(tb_Link.Text);
                await foreach (var batch in youtube.Playlists.GetVideoBatchesAsync(tb_Link.Text))
                {
                    foreach (var video in batch.Items)
                    {
                        VideoInfos.Add(new VideoInfo { url = video.Url, title = video.Title, author = video.Author.ChannelTitle, duration = video.Duration.Value.ToString() + "h", playlistTitle = pl.Title });
                    }
                }

            }
            else
            {

                var test = youtube.Search.GetVideosAsync(tb_Link.Text);
                int index = 0;
                await foreach (var i in test)
                {
                    if (index < 20)
                    {
                        try
                        {
                            VideoInfos.Add(new VideoInfo { url = i.Url, title = i.Title, author = i.Author.ChannelTitle, duration = i.Duration.Value.ToString() + "h" });
                            index++;
                        }
                        catch { }

                    }
                    else
                        break;
                }
                //video.UploadDate

            }
            ListViewVideos.ItemsSource = VideoInfos;
        }

        private async void tb_Link_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            GetUrlFromTextBox();
        }

        public async Task DownloadVideo(bool playlist = false)
        {
            YoutubeClient youtube = new YoutubeClient();
            string outputPath = Directory.GetCurrentDirectory() + @"\Downloads\";
            var video = await youtube.Videos.GetAsync(VideoQueue[0].url);
            VideoQueue[0].state = "Downloading in Progress";
            VideoQueue[0].stateColor = Brushes.Green;
            ListViewVideosQueue.Items.Refresh();

            string downloadPath;


            if (playlist)
            {
                var item = VideoInfos.FirstOrDefault(e => e.url == VideoQueue[0].url);
                var index = VideoInfos.IndexOf(item) + 1;
                outputPath += @$"{RemoveForbiddenChars(video.Author.ChannelTitle)}\" + RemoveForbiddenChars(VideoInfos[index].playlistTitle) + "\\";
                downloadPath = outputPath + index.ToString("D2") + " - " + RemoveForbiddenChars(video.Title) + ".mp4";
                //"\\" + VideoInfos[index].playlistTitle + "\\" + index.ToString("D2") + " - " +
            }
            else
            {
                outputPath += @$"{RemoveForbiddenChars(video.Author.ChannelTitle)}\";
                downloadPath = outputPath + RemoveForbiddenChars(video.Title) + ".mp4";
            }
            Directory.CreateDirectory(outputPath);

            var progress = new Progress<double>(p =>
            {
                var newP = Math.Round((decimal)p * 100, 2);
                if (VideoQueue.Count() > 0)
                {
                    VideoQueue[0].progress = Convert.ToDouble(newP);
                    ListViewVideosQueue.Items.Refresh();
                }
            });
            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(VideoQueue[0].url);

            var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

            var streams = streamManifest.GetVideoOnlyStreams().Where(s => !(s.VideoQuality.Label.Contains("2160") | s.VideoQuality.Label.Contains("1440")));
            var streamInfos = new IStreamInfo[] { audioStreamInfo, streams.First() };
            var tmp = streamInfos.First().Size.MegaBytes;
            //tb_FileSize.Text = tmp.ToString();
            await youtube.Videos.DownloadAsync(streamInfos, new ConversionRequestBuilder(downloadPath).Build(), progress);
            VideoQueue.Remove(VideoQueue[0]);
        }

            
        
        private void ListViewVideos_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            //ListViewVideos.SelectedIndex = -1;
        }
        private void ListViewVideosQueue_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }

        private void ListViewVideos_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if(e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && ListViewVideos.Items.Count > 0 && ListViewVideos.SelectedIndex != -1)
            { 
                DragDrop.DoDragDrop(ListViewVideos, ListViewVideos.SelectedItem, DragDropEffects.Move);
            }
        }

        private void ListViewVideosQueue_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && ListViewVideosQueue.Items.Count > 0 && ListViewVideosQueue.SelectedIndex != -1)
            {
                DragDrop.DoDragDrop(ListViewVideosQueue, ListViewVideosQueue.SelectedItem, DragDropEffects.Move);
            }
        }

        private void ListViewVideos_Drop(object sender, DragEventArgs e)
        {
            if(e.Data.GetDataPresent(typeof(VideoInfo)))
            {
                VideoInfo item = (VideoInfo)e.Data.GetData(typeof(VideoInfo));
                if (!VideoInfos.Contains(item))
                {
                    VideoInfos.Add(item);
                    VideoQueue.Remove(item);
                }
            }
        }

        private void ListViewVideosQueue_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(VideoInfo)))
            {
                VideoInfo item = (VideoInfo)e.Data.GetData(typeof(VideoInfo));
                if (!VideoQueue.Contains(item))
                {
                    VideoQueue.Add(item);
                    VideoInfos.Remove(item);
                }
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ListViewVideos.ItemsSource = VideoInfos;
            ListViewVideosQueue.ItemsSource = VideoQueue;
            string ClipBoard = "-";
            while (true)
            {
                if (ClipBoard != Clipboard.GetText())
                {
                    ClipBoard = Clipboard.GetText();
                    if (ClipBoard.Contains("https://www.youtube.com") & tb_Link.Text != ClipBoard)
                        tb_Link.Text = ClipBoard;
                }

                if (VideoQueue.Count >= 1)
                {
                    try
                    {
                        
                        await DownloadVideo();
                    }
                    catch (Exception ex)
                    {
                        VideoQueue[0].state = "ERROR -> Retry";
                        VideoQueue[0].stateColor = Brushes.Red;
                        ListViewVideosQueue.Items.Refresh();
                    }
                    //ListViewVideosQueue.Items.Refresh();
                }
                await Task.Delay(100);
            }
            
        }

        private void AddAllToListButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var i in VideoQueue)
            {
                VideoInfos.Add(i);
            }
            VideoQueue.Clear();
        }

        private void AddAllToQueueButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var i in VideoInfos)
            {
                VideoQueue.Add(i);
            }
            VideoInfos.Clear();
        }
    }
}
