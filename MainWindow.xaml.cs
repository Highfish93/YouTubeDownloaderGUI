using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos.Streams;
namespace YouTubeDownloaderGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            string url = tb_Link.Text;
            tb_Link.Text = "";
            YoutubeClient youtube = new YoutubeClient();
            if (url.Contains("watch?") || (url.Contains("shorts")))
            {
                string outputPath = Directory.GetCurrentDirectory() + @"\Downloads\";
                var video = await youtube.Videos.GetAsync(url);
                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Url);
                var audioStreamInfo = streamManifest.GetAudioStreams().GetWithHighestBitrate();
                var videoStreamInfo = streamManifest.GetVideoStreams().First();
                var streamInfos = new IStreamInfo[] { audioStreamInfo, videoStreamInfo };
                tb_Author.Text = "Author: " + video.Author.ChannelTitle;
                tb_Title.Text = "Title: " + video.Title;
                tb_Duration.Text = "Duration: " + video.Duration + "h";
                await Task.Delay(200);
                MessageBox.Show(ConvertBytes(videoStreamInfo.Size.Bytes));
                MessageBox.Show(ConvertBytes(audioStreamInfo.Size.Bytes));
                tb_FileSize.Text = "Filesize: " + ConvertBytes(audioStreamInfo.Size.Bytes + videoStreamInfo.Size.Bytes);
                var result = MessageBox.Show("Really download?", "Download", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
                if (result == MessageBoxResult.Yes)
                {
                    outputPath += @$"{video.Author}\";
                    Directory.CreateDirectory(outputPath);
                    var progress = new Progress<double>(p =>
                    {
                        var newP = Math.Round((decimal)p * 100, 2);
                        downloadProgress.Value = Convert.ToDouble(newP);
                        TbProgress.Text = newP.ToString() + "%";
                    });
                    string downloadPath = outputPath + RemoveForbiddenChars(video.Title) + ".mp4";
                    //await youtube.Videos.DownloadAsync(streamInfos, new ConversionRequestBuilder(downloadPath).Build(), progress);
                    await youtube.Videos.DownloadAsync(url, downloadPath, progress);
                    await Task.Delay(500);
                }
                    downloadProgress.Value = 0;
                    tb_Author.Text = "Author:";
                    tb_Title.Text = "Title:";
                    tb_Duration.Text = "Duration:";
                    TbProgress.Text = "0%";
                    tb_FileSize.Text = "Filesize:";
            
            }
            else if (url.Contains("playlist?list="))
            {
                var mbRes = MessageBox.Show("Really download?", "Download", MessageBoxButton.YesNo, MessageBoxImage.None, MessageBoxResult.Yes);
                if (mbRes == MessageBoxResult.Yes)
                {
                    int counter = 1;
                    var pl = await youtube.Playlists.GetAsync(url);
                    var plTitle = pl.Title;
                    await foreach (var video in youtube.Playlists.GetVideosAsync(url))
                    {
                        string outputPath = Directory.GetCurrentDirectory() + @"\Downloads\";
                        var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Url);
                        var audioStreamInfo = streamManifest.GetAudioStreams().GetWithHighestBitrate();
                        var videoStreamInfo = streamManifest.GetVideoStreams().GetWithHighestVideoQuality();
                        var streamInfos = new IStreamInfo[] { audioStreamInfo, videoStreamInfo };

                        tb_Author.Text = "Author: " + video.Author.ChannelTitle;
                        tb_Title.Text = "Title: " + video.Title;
                        tb_Duration.Text = "Duration: " + video.Duration + "h";
                        tb_FileSize.Text = "Filesize: " + ConvertBytes(audioStreamInfo.Size.Bytes + videoStreamInfo.Size.Bytes);
                        outputPath += @$"{video.Author}\{RemoveForbiddenChars(plTitle)}\";
                        Directory.CreateDirectory(outputPath);
                        var progress = new Progress<double>(p =>
                        {
                            var newP = Math.Round((decimal)p * 100, 2);
                            downloadProgress.Value = Convert.ToDouble(newP);
                            TbProgress.Text = newP.ToString() + "%";
                        });
                        string downloadPath = outputPath + counter.ToString("00") + " - " + RemoveForbiddenChars(video.Title) + ".mp4";
                        if (!File.Exists(downloadPath))
                        {
                            await youtube.Videos.DownloadAsync(streamInfos, new ConversionRequestBuilder(downloadPath).Build(), progress);
                            await Task.Delay(500);
                            downloadProgress.Value = 0;
                            tb_Author.Text = "Author:";
                            tb_Title.Text = "Title:";
                            tb_Duration.Text = "Duration:";
                            TbProgress.Text = "0%";
                            tb_FileSize.Text = "Filesize:";
                        }
                        counter += 1;
                    }
                }
            }
            
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            CreateVlcPlaylist(@"C:\Users\skrie\Desktop\Projekte\C#\YouTubeDownloaderGUI\YouTubeDownloaderGUI\bin\Debug\net7.0-windows\Downloads\Yannick\HerrAnwalt Das Spiel\");
        }
    }
}
