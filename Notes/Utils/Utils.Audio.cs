using NReco.VideoConverter;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace Notes
{
    internal partial class Utils
    {
        public static byte[] LoadAudioBytes(string file)
        {
            var ffmpeg = new FFMpegConverter();
            var output = new MemoryStream();

            var extension = Path.GetExtension(file).Substring(1);

            // Convert to PCM
            ffmpeg.ConvertMedia(inputFile: file,
                                inputFormat: null,
                                outputStream: output,
                                //  DE s16le PCM signed 16-bit little-endian
                                outputFormat: "s16le",
                                new ConvertSettings()
                                {
                                    AudioCodec = "pcm_s16le",
                                    AudioSampleRate = 16000,
                                    // Convert to mono
                                    CustomOutputArgs = "-ac 1"
                                });
            return output.ToArray();
        }

        public static byte[] ExtractAudioSegment(string inPath, double startTimeInSeconds, double segmentDurationInSeconds)
        {
            try
            {
                var extension = System.IO.Path.GetExtension(inPath).Substring(1);
                var output = new MemoryStream();

                var convertSettings = new ConvertSettings
                {
                    Seek = (float?)startTimeInSeconds,
                    MaxDuration = (float?)segmentDurationInSeconds,
                    //AudioCodec = "pcm_s16le",
                    AudioSampleRate = 16000,
                    CustomOutputArgs = "-vn -ac 1",
                };

                var ffMpegConverter = new FFMpegConverter();
                ffMpegConverter.ConvertMedia(
                    inputFile: inPath,
                    inputFormat: null,
                    outputStream: output,
                    outputFormat: "wav",
                    convertSettings);

                return output.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during the audio extraction: " + ex.Message);
                return new byte[0]; // Return an empty array in case of exception
            }
        }

        public static async Task<StorageFile> SaveAudioFileAsWav(StorageFile file, StorageFolder folderToSaveTo)
        {
            var ffmpeg = new FFMpegConverter();
            var newFilePath = $"{folderToSaveTo.Path}\\{file.DisplayName}.wav";
            ffmpeg.ConvertMedia(file.Path, newFilePath, "wav");
            var newFile = await StorageFile.GetFileFromPathAsync(newFilePath);
            return newFile;
        }
    }
}
