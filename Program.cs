using System;
using System.IO;
using System.Linq;
using System.CommandLine;
using System.CommandLine.Invocation;
using Serilog;

namespace VideoApp
{
  class Program
  {

    static void SplitVideoIntoSegments(FileInfo inputVideoFile, DirectoryInfo segmentsFolder) {

      if (inputVideoFile == null) {
        Console.WriteLine("Input video file must be a file path.");
        return;
      }

      try {

        if (!File.Exists(inputVideoFile.FullName)) {
          throw new Exception("Input video file does not exist.");
        }

        var baseInputVideoFileName = inputVideoFile.Name.Substring(0, inputVideoFile.Name.Length - 4);

        if (segmentsFolder == null) {
          segmentsFolder = new DirectoryInfo(Path.Join(inputVideoFile.DirectoryName, baseInputVideoFileName + "-Segments"));
        }

        if (Directory.Exists(segmentsFolder.FullName)) {
          throw new Exception("Segments folder should not exist.");
        }

        Directory.CreateDirectory(segmentsFolder.FullName);

        var ffmpegArgs = new string[] {
          "-i",
          inputVideoFile.FullName,
          "-c",
          "copy",
          "-f",
          "segment",
          "-segment_time",
          "600",
          "-reset_timestamps",
          "1",
          Path.Join(segmentsFolder.FullName, baseInputVideoFileName + "-%03d.mp4")
        };

        using (var process = new System.Diagnostics.Process()) {
          process.StartInfo.FileName = Path.Join(Environment.CurrentDirectory, "ffmpeg");
          process.StartInfo.Arguments = String.Join(" ", ffmpegArgs);
          process.StartInfo.RedirectStandardError = true;

          process.ErrorDataReceived += new System.Diagnostics.DataReceivedEventHandler((sender, e) => {
            Log.Error(e.Data);
          });

          process.Start();
          process.WaitForExit();
        }

      } catch(Exception exc) {
        Log.Error(exc.Message);
        Console.WriteLine("Error while trying to split the video. Please review the logs.");
      }
    }

    static void JoinVideoSegments(DirectoryInfo segmentsFolder, FileInfo outputVideoFile) {

      if (segmentsFolder == null) {
        Console.WriteLine("The segments folder must be a folder path.");
        return;
      }

      if (outputVideoFile == null) {
        Console.WriteLine("The output video file must be a file path.");
        return;
      }

      try {

        if (!Directory.Exists(segmentsFolder.FullName)) {
          throw new Exception("Video segments folder must exist");
        }

        if (File.Exists(outputVideoFile.FullName)) {
          throw new Exception("Output video file must not exist.");
        }

        var files = Directory
          .GetFiles(segmentsFolder.FullName, "*.mp4")
          .Select(fileName => "file '" + Path.GetFileName(fileName) + "'")
          .ToList();
      
        files.Sort();

        var listFileName = Path.Join(segmentsFolder.FullName, "list.txt");

        System.IO.File.WriteAllText(
          listFileName,
          String.Join(Environment.NewLine, files)
        );

        var ffmpegArgs = new string[] {
          "-f",
          "concat",
          "-i",
          listFileName,
          "-c",
          "copy",
          outputVideoFile.FullName,
        };

        using (var process = new System.Diagnostics.Process()) {
          process.StartInfo.FileName = Path.Join(Environment.CurrentDirectory, "ffmpeg");
          process.StartInfo.Arguments = String.Join(" ", ffmpegArgs);
          // process.StartInfo.RedirectStandardError = true;
          // process.ErrorDataReceived += new System.Diagnostics.DataReceivedEventHandler((sender, e) => {
          //  Log.Error(e.Data);
          // });

          process.Start();
          process.WaitForExit();
        }

      } catch (Exception exc) {
        Log.Error(exc.Message);
        Console.WriteLine("Error while trying to join video segments into a single video. Please review the logs.");
      }

    }


    static int CreateCommands(string[] args) {
      var rootCommand = new RootCommand();

      var splitCommand = new Command("split");
      splitCommand.Add(new Option<FileInfo>("--input-video-file", "The path to the video file to split into segments."));
      splitCommand.Add(new Option<DirectoryInfo>("--segments-folder", "The folder to create and output video segments to. The folder must not exist."));
      splitCommand.Handler = CommandHandler.Create<FileInfo, DirectoryInfo>(SplitVideoIntoSegments);
      rootCommand.AddCommand(splitCommand);

      var joinCommand = new Command("join");
      joinCommand.Add(new Option<DirectoryInfo>("--segments-folder", "The folder which contains the segments of the video to be joined together."));
      joinCommand.Add(new Option<FileInfo>("--output-video-file", "The file to which the join video will be saved. Must not exist."));
      joinCommand.Handler = CommandHandler.Create<DirectoryInfo, FileInfo>(JoinVideoSegments);
      rootCommand.AddCommand(joinCommand);

      rootCommand.Description = "Splits large videos into segments and joins the segments of large videos into a single video.";

      return rootCommand.InvokeAsync(args).Result;
    }

    static int Main(string[] args)
    {
      Log.Logger = new LoggerConfiguration()
        .WriteTo.File(Path.Join("logs", "app.log"), rollingInterval: RollingInterval.Day)
        .CreateLogger();

      return CreateCommands(args);
    }
  }
}
