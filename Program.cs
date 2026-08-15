using System.Diagnostics;
using System.Threading.Tasks;

namespace TwitchRestreamer
{
    internal class Program
    {
        static int refreshIntervalSeconds;
        static int cooldownIntervalSeconds;

        static string? channelName = Environment.GetEnvironmentVariable("TWITCH_CHANNEL");

        static string? youtubeStreamUrl = Environment.GetEnvironmentVariable("YOUTUBE_URL");
        static string? kickStreamUrl = Environment.GetEnvironmentVariable("KICK_URL");

        static string? youtubeStreamKey = Environment.GetEnvironmentVariable("YOUTUBE_KEY");
        static string? kickStreamKey = Environment.GetEnvironmentVariable("KICK_KEY");

        static void Main(string[] args)
        {
            //Validate and parse env vars
            if (channelName == null)
            {
                Console.WriteLine("[FATAL]: No Twitch channel provided!");
                return;
            }

            if (youtubeStreamUrl == null)
            {
                Console.WriteLine("[INFO]: No YouTube stream URL provided! Defaulting to rtmp://a.rtmp.youtube.com/live2");
                youtubeStreamUrl = "rtmp://a.rtmp.youtube.com/live2";
            }

            if (kickStreamUrl == null)
                Console.WriteLine("[INFO]: No Kick stream URL provided!");

            if (kickStreamKey == null)
                Console.WriteLine("[INFO]: No Kick stream key provided!");

            if (kickStreamKey == null)
                Console.WriteLine("[INFO]: No YouTube stream key provided!");

            if (kickStreamKey == null && youtubeStreamKey == null)
            {
                Console.WriteLine("[FATAL]: You must provide a stream key for Kick and/or YouTube!");
                return;
            }

            if (!int.TryParse(Environment.GetEnvironmentVariable("REFRESH_INTERVAL"), out refreshIntervalSeconds))
            {
                Console.WriteLine("[WARN]: Could not parse REFRESH_INTERVAL evironment variable! Defaulting to 5 seconds.");
                refreshIntervalSeconds = 5;
            }

            if (!int.TryParse(Environment.GetEnvironmentVariable("COOLDOWN"), out cooldownIntervalSeconds))
            {
                Console.WriteLine("[WARN]: Could not parse COOLDOWN evironment variable! Defaulting to 120 seconds.");
                cooldownIntervalSeconds = 120;
            }

            //TODO: Not while(true)
            while (true)
            {
                string? twitchLink = getStreamLink();

                if(twitchLink == null || twitchLink.StartsWith("error"))
                {
                    Thread.Sleep(refreshIntervalSeconds * 1000);
                    continue;
                }

                startRestream(twitchLink);
                Thread.Sleep(cooldownIntervalSeconds * 1000);
            }
        }

        static void startRestream(string twitchLink)
        {
            Process process = new Process();
            process.StartInfo.FileName = "ffmpeg";

            process.StartInfo.ArgumentList.Add("-fflags");
            process.StartInfo.ArgumentList.Add("nobuffer");

            process.StartInfo.ArgumentList.Add("-flags");
            process.StartInfo.ArgumentList.Add("low_delay");

            process.StartInfo.ArgumentList.Add("-i");
            process.StartInfo.ArgumentList.Add(twitchLink);

            if (kickStreamKey != null)
            {
                process.StartInfo.ArgumentList.Add("-map");
                process.StartInfo.ArgumentList.Add("0:v:0");
                process.StartInfo.ArgumentList.Add("-map");
                process.StartInfo.ArgumentList.Add("0:a:0");

                process.StartInfo.ArgumentList.Add("-c");
                process.StartInfo.ArgumentList.Add("copy");

                process.StartInfo.ArgumentList.Add("-f");
                process.StartInfo.ArgumentList.Add("flv");
                process.StartInfo.ArgumentList.Add("-flvflags");
                process.StartInfo.ArgumentList.Add("no_duration_filesize");
                process.StartInfo.ArgumentList.Add(kickStreamUrl + "/" + kickStreamKey);
            }
            if (youtubeStreamKey != null)
            {
                process.StartInfo.ArgumentList.Add("-map");
                process.StartInfo.ArgumentList.Add("0:v:0");
                process.StartInfo.ArgumentList.Add("-map");
                process.StartInfo.ArgumentList.Add("0:a:0");

                process.StartInfo.ArgumentList.Add("-c");
                process.StartInfo.ArgumentList.Add("copy");

                process.StartInfo.ArgumentList.Add("-c:a");
                process.StartInfo.ArgumentList.Add("aac");
                process.StartInfo.ArgumentList.Add("-b:a");
                process.StartInfo.ArgumentList.Add("128k");
                process.StartInfo.ArgumentList.Add("-ar");
                process.StartInfo.ArgumentList.Add("48000");

                process.StartInfo.ArgumentList.Add("-f");
                process.StartInfo.ArgumentList.Add("flv");
                process.StartInfo.ArgumentList.Add("-flvflags");
                process.StartInfo.ArgumentList.Add("no_duration_filesize");
                process.StartInfo.ArgumentList.Add(youtubeStreamUrl + "/" + youtubeStreamKey);
            }

            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            using var outputLog = new StreamWriter("logs/output.log", true)
            {
                AutoFlush = true
            };

            using var errorLog = new StreamWriter("logs/error.log", true)
            {
                AutoFlush = true
            };

            process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                if (e.Data == null)
                    return;

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                string line = "[" + timestamp + "] " + e.Data;

                Console.WriteLine(line);
                errorLog.Write(line + "\n");
            };

            process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                if (e.Data == null)
                    return;

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                string line = "[" + timestamp + "] " + e.Data;

                Console.WriteLine(line);
                outputLog.Write(line + "\n");
            };

            process.Start();

            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            process.WaitForExit();
        }

        static string? getStreamLink()
        {
            Process process = new Process();
            process.StartInfo.FileName = "streamlink";
            process.StartInfo.Arguments = "--stream-url --hls-live-edge 1 \"twitch.tv/" + channelName + "\" best";
            process.StartInfo.RedirectStandardOutput = true;

            process.Start();

            string? link = process.StandardOutput.ReadToEnd().Trim();

            process.WaitForExit();

            return string.IsNullOrWhiteSpace(link) ? null : link;
        }
    }
}
