using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
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
        public List<VideoInfo> VideoInfos = new List<VideoInfo>();
        public List<VideoInfo> VideoQueue = new List<VideoInfo>();

        public MainWindow()
        {
            InitializeComponent();
        }

        public class VideoInfo
        {
            public string? url { get; set; }
            public string? title { get; set; }
            public string? playlistTitle { get; set; }
            public string? author { get; set; }
            public string? duration { get; set; }
            public DateTimeOffset? uploadDate { get; set; }
            public bool downloadChecked { get; set; } = true;
            public bool likeCheck { get; set; }

            public double progress { get; set; }
        }

        public static string Encode2URL(string file)
        {
            file = file.Replace("%", "%25");
            file = file.Replace("Ä", "%C3%84");
            file = file.Replace("Ö", "%C3%96");
            file = file.Replace("Ü", "%C§%9C");
            file = file.Replace("ß", "%C3%9F");
            file = file.Replace("ä", "%C3%A4");
            file = file.Replace("ö", "%C3%B6");
            file = file.Replace("ü", "%C3%BC");
            file = file.Replace("!", "%21");
            file = file.Replace("#", "%23");
            file = file.Replace("$","%24");
            file = file.Replace("&", "%26");
            file = file.Replace("'", "%27");
            file = file.Replace("(", "%28");
            file = file.Replace(")", "%29");
            file = file.Replace("*", "%2A");
            file = file.Replace("+", "%2B");
            file = file.Replace(",", "%2C");
            //file = file.Replace(".", "%2E");
            file = file.Replace("/", "%2F");
            //file = file.Replace(":", "%3A");
            file = file.Replace(";", "%3B");
            file = file.Replace("=", "%3D");
            file = file.Replace("?", "%3F");
            file = file.Replace("@", "%40");
            file = file.Replace("[", "%5B");
            file = file.Replace("]", "5D");
            file = file.Replace(" ","%20");
            return file;
        }
        public static async void CreateVlcPlaylist(string path)
        {
            if (File.Exists(path+"00 - Wiedergabeliste.xspf"))
            {
                File.Delete(path + "00 - Wiedergabeliste.xspf");
            }
            string header = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<playlist xmlns=\"http://xspf.org/ns/0/\" xmlns:vlc=\"http://www.videolan.org/vlc/playlist/ns/0/\" version=\"1\">\r\n";
            await File.WriteAllTextAsync(path+"00 - Wiedergabeliste.xspf", header);
            using StreamWriter textfile = new(path+"00 - Wiedergabeliste.xspf", append: true);
            string nextPart = "\t<title>Wiedergabeliste</title>\r\n\t<trackList>";
            await textfile.WriteLineAsync(nextPart);
            List<string> videoFiles = new List<string>();
            DirectoryInfo rootpath = new DirectoryInfo(path);
            int counter = 0;
            foreach (var file in rootpath.GetFiles("*.mp4", SearchOption.TopDirectoryOnly))
            {
                videoFiles.Add(Encode2URL(file.FullName));
                using (var shell = ShellObject.FromParsingName(file.FullName))
                {
                    IShellProperty prop = shell.Properties.System.Media.Duration;
                    var t = (ulong)prop.ValueAsObject;
                    var dur = TimeSpan.FromTicks((long)t);
                    var hours = dur.Hours* 3600000;
                    var minutes = dur.Minutes* 60000;
                    var seconds = dur.Seconds * 1000;
                    var miliseconds = dur.Milliseconds;
                    var videoduration = hours + minutes + seconds + miliseconds;
                    //MessageBox.Show(videoduration.ToString());
                    await textfile.WriteLineAsync("\t\t<track>\r\n\t\t\t<location>file:///" + Encode2URL(file.FullName).Replace(@"\", "/") + "</location>");
                    await textfile.WriteLineAsync("\t\t\t<duration>" + videoduration.ToString()+"</duration>");
                    await textfile.WriteLineAsync("\t\t\t<extension application=\"http://www.videolan.org/vlc/playlist/0\">");
                    await textfile.WriteLineAsync("\t\t\t\t<vlc:id>" + counter.ToString() + "</vlc:id>");
                    await textfile.WriteLineAsync("\t\t\t</extension>");
                    await textfile.WriteLineAsync("\t\t</track>");
                }
                counter++;
                    
            }
            await textfile.WriteLineAsync("\t</trackList>");
            await textfile.WriteLineAsync("\t<extension application=\"http://www.videolan.org/vlc/playlist/0\">");
            for (int i =0;i<=counter-1;i++)
            {
                await textfile.WriteLineAsync($"\t\t<vlc:item tid=\"{i}\"/>");
            }
            await textfile.WriteLineAsync("\t</extension>");
            await textfile.WriteLineAsync("</playlist>");
            
        }
        public static string ConvertBytes(float filesize)
        {
            List<string> filesizeStrings = new List<string> { "", "K", "M", "G", "T" };
            foreach (string c in filesizeStrings)
            {
                if (filesize < 1024)
                {
                    return $"{Math.Round(filesize, 2)}{c}B";
                }
                filesize = filesize / 1024;
            }
            return $"{Math.Round(filesize, 2)}EB";
        }
        public static void ResetUI()
        {

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

        private async void tb_Link_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            VideoInfos.Clear();
            ListViewVideos.ItemsSource = null;
            YoutubeClient youtube = new YoutubeClient();
            if (tb_Link.Text.Contains("watch") | tb_Link.Text.Contains("shorts"))
            {
                downloadProgress.Value = 0;
                TbProgress.Text = "0%";
                var video = await youtube.Videos.GetAsync(tb_Link.Text);
                //video.UploadDate
                VideoInfos.Add(new VideoInfo { url = tb_Link.Text, title = video.Title, author = video.Author.ChannelTitle, duration = video.Duration.Value.ToString() + "h" , uploadDate=video.UploadDate});
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
                await foreach(var i in test)
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

        public async Task DownloadVideo(bool playlist = false)
        {
                YoutubeClient youtube = new YoutubeClient();
                string outputPath = Directory.GetCurrentDirectory() + @"\Downloads\";
                var video = await youtube.Videos.GetAsync(VideoQueue[0].url);

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
                tb_FileSize.Text = tmp.ToString();
                await youtube.Videos.DownloadAsync(streamInfos, new ConversionRequestBuilder(downloadPath).Build(), progress);
                VideoQueue.Remove(VideoQueue[0]);
        }

            
        
        private void ListViewVideos_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            //ListViewVideos.SelectedIndex = -1;
        }

        private async void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            btnDownload.IsEnabled = false;
            bool pl = tb_Link.Text.Contains("playlist?list=");
            foreach (VideoInfo video in ListViewVideos.Items)
            {
                if (video.downloadChecked)
                {
                    //await DownloadVideo(video.url, pl);
                }
            }
            if (pl)
            {
                MessageBox.Show("Download Completed");
            }
            btnDownload.IsEnabled = true;
        }

        private void ListViewVideosQueue_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }

        private void ListViewVideos_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if(e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && ListViewVideos.Items.Count > 0)
            { 
                DragDrop.DoDragDrop(ListViewVideos, ListViewVideos.SelectedItem, DragDropEffects.Move);
            }
        }

        private void ListViewVideosQueue_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && ListViewVideosQueue.Items.Count > 0)
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
                    ListViewVideos.ItemsSource = null;
                    ListViewVideos.ItemsSource = VideoInfos;
                    VideoQueue.Remove(item);
                    ListViewVideosQueue.ItemsSource = null;
                    ListViewVideosQueue.ItemsSource = VideoQueue;
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
                    ListViewVideosQueue.ItemsSource = null;
                    ListViewVideosQueue.ItemsSource = VideoQueue;
                    VideoInfos.Remove(item);
                    ListViewVideos.ItemsSource = null;
                    ListViewVideos.ItemsSource = VideoInfos;
                }
            }
        }

        public class IsIndeterminateConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value == null)
                {
                    return true;
                }
                return false;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            test();
            while (true)
            {
                if (VideoQueue.Count >= 1)
                {
                    await DownloadVideo();
                    ListViewVideosQueue.Items.Refresh();
                }
                await Task.Delay(100);
            }
            
        }

        private void AddAllToListButton_Click(object sender, RoutedEventArgs e)
        {
            VideoInfos.AddRange(VideoQueue);
            ListViewVideos.ItemsSource = null;
            ListViewVideos.ItemsSource = VideoInfos;
            ListViewVideosQueue.ItemsSource = null;
            ListViewVideosQueue.ItemsSource = VideoQueue;
            VideoQueue.Clear();
        }

        private void AddAllToQueueButton_Click(object sender, RoutedEventArgs e)
        {
            VideoQueue.AddRange(VideoInfos);
            ListViewVideos.ItemsSource = null;
            ListViewVideos.ItemsSource = VideoInfos;
            ListViewVideosQueue.ItemsSource = null;
            ListViewVideosQueue.ItemsSource = VideoQueue;
            VideoInfos.Clear();
        }

        public async void test()
        {
            UserCredential credential;
            using (var stream = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read))
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    new[] { YouTubeService.Scope.Youtube },
                    "user",
                    CancellationToken.None,
                    new FileDataStore("YoutubeAPI")
                );
            }

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "YoutubeAPI",
                ApiKey = "AIzaSyALvidCoH9V6qWL12srN_PRxNBbkpcGFBE"
            });

            var subscriptionsRequest = youtubeService.Subscriptions.List("snippet");
            subscriptionsRequest.Mine = true;
            var subscriptionsResponse = await subscriptionsRequest.ExecuteAsync();

            Console.WriteLine("Your subscriptions:");
            foreach (var subscription in subscriptionsResponse.Items)
            {
                MessageBox.Show(subscription.Snippet.Title);
                //ListViewVideos.Items.Add(subscription.Snippet.Title);
            }

            var activitiesRequest = youtubeService.Activities.List("contentDetails");
            activitiesRequest.Home = true;
            activitiesRequest.MaxResults = 10;
            var activitiesResponse = await activitiesRequest.ExecuteAsync();

            Console.WriteLine("Your recent activity:");
            foreach (var activity in activitiesResponse.Items)
            {
                MessageBox.Show(activity.ContentDetails.Upload.VideoId);
            }

        }
    }
}
