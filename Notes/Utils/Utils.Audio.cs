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

        public static float[] ExtractAudioSegment(string inPath, double startTimeInSeconds, double segmentDurationInSeconds)
        {
            try
            {
                var extension = Path.GetExtension(inPath).Substring(1);
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

                //return output.ToArray();
                var buffer = output.ToArray();
                int bytesPerSample = 2; // Assuming 16-bit depth (2 bytes per sample)

                // Calculate total samples in the buffer
                int totalSamples = buffer.Length / bytesPerSample;
                float[] samples = new float[totalSamples];

                for (int i = 0; i < totalSamples; i++)
                {
                    int bufferIndex = i * bytesPerSample;
                    short sample = (short)(buffer[bufferIndex + 1] << 8 | buffer[bufferIndex]);
                    samples[i] = sample / 32768.0f; // Normalize to range [-1,1] for floating point samples
                }

                return samples;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during the audio extraction: " + ex.Message);
                return []; // Return an empty array in case of exception
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
