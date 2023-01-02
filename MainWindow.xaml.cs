using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using YoutubeExplode;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos.Streams;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
            tb_Link.Text = "https://www.youtube.com/watch?v=rJNBGqiBI7s";
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
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DownloadVideo(tb_Link.Text);
            
        }

        private async void tb_Link_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            YoutubeClient youtube = new YoutubeClient();
            if (tb_Link.Text.Contains("watch") | tb_Link.Text.Contains("shorts"))
            {
                tb_FileSize.Text = "";
                downloadProgress.Value = 0;
                TbProgress.Text = "0%";
                var video = await youtube.Videos.GetAsync(tb_Link.Text);
                tb_Author.Text = "Author: " + video.Author.ChannelTitle;
                tb_Title.Text = "Title: " + video.Title;
                tb_Duration.Text = "Duration: " + video.Duration.Value.ToString() + "h";
            }
            else if (tb_Link.Text.Contains("playlist?list="))
            {
                var counter = 0;
                await foreach (var batch in youtube.Playlists.GetVideoBatchesAsync(tb_Link.Text))
                {
                    foreach (var video in batch.Items)
                    {
                        counter += 1;
                        tb_Author.Text = "Author: " + video.Author;
                        tb_Title.Text = "Title: " + video.Title;
                        tb_Duration.Text = "Duration: " + video.Duration.Value.ToString() + "h";
                        DownloadVideo(video.Url);
                    }
                }

            }
        }

        public async void DownloadVideo(string url)
        {
            YoutubeClient youtube = new YoutubeClient();
            string outputPath = Directory.GetCurrentDirectory() + @"\Downloads\";
            var video = await youtube.Videos.GetAsync(url);
            outputPath += @$"{video.Author}\";
            Directory.CreateDirectory(outputPath);
            string downloadPath = outputPath + RemoveForbiddenChars(video.Title) + ".mp4";

            var progress = new Progress<double>(p =>
            {
                var newP = Math.Round((decimal)p * 100, 2);
                downloadProgress.Value = Convert.ToDouble(newP);
                TbProgress.Text = newP.ToString() + "%";
            });
            /*
            await youtube.Videos.DownloadAsync(url, downloadPath, progress);
            */
            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);

            // Select streams (1080p60 / highest bitrate audio)
            var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            //var videoStreamInfo = streamManifest.GetVideoStreams().First(s => s.VideoQuality.Label == "1080p");

            var streams = streamManifest.GetVideoOnlyStreams().Where(s => !(s.VideoQuality.Label.Contains("2160") | s.VideoQuality.Label.Contains("1440")));
            var streamInfos = new IStreamInfo[] { audioStreamInfo, streams.First() };
            var tmp = streams.First().Size.MegaBytes + audioStreamInfo.Size.MegaBytes;
            tb_FileSize.Text = tmp.ToString();
            await youtube.Videos.DownloadAsync(streamInfos, new ConversionRequestBuilder(downloadPath).Build(), progress);
        }
    }
}
