using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.UI.Xaml.Media.Imaging;
using NAudio.Wave;
using NAudio.WaveFormRenderer;
using Windows.Storage;

namespace Notes
{
    public static class WaveformRenderer
    {
        public enum PeakProvider
        {
            Max,
            RMS,
            Sampling,
            Average
        }
        
        private static Image Render(StorageFile audioFile, int height, int width, PeakProvider peakProvider)
        {
            WaveFormRendererSettings settings = new StandardWaveFormRendererSettings();
            settings.BackgroundColor = Color.Transparent;
            settings.SpacerPixels = 0;
            settings.TopHeight = height;
            settings.BottomHeight = height;
            settings.Width = width;
            settings.TopPeakPen = new Pen(Color.DarkGray);
            settings.BottomPeakPen = new Pen(Color.DarkGray);
            AudioFileReader audioFileReader = new AudioFileReader(audioFile.Path);

            IPeakProvider provider;
            switch (peakProvider)
            {
                case PeakProvider.Max:
                    provider = new MaxPeakProvider();
                    break;
                case PeakProvider.RMS:
                    provider = new RmsPeakProvider(200);
                    break;
                case PeakProvider.Sampling:
                    provider = new SamplingPeakProvider(1600);
                    break;
                default:
                    provider = new AveragePeakProvider(4);
                    break;
            }

            WaveFormRenderer renderer = new WaveFormRenderer();
            return renderer.Render(audioFileReader, provider, settings);
        }

        public static async System.Threading.Tasks.Task<BitmapImage> GetWaveformImage(StorageFile audioFile)
        {
                StorageFile imageFile;
                StorageFolder attachmentsFolder = await Utils.GetAttachmentsTranscriptsFolderAsync();
                string waveformFileName = Path.ChangeExtension(Path.GetFileName(audioFile.Path) + "-waveform", ".png");
                try
                {
                    imageFile = await attachmentsFolder.CreateFileAsync(waveformFileName, CreationCollisionOption.FailIfExists);
                    using (var stream = await imageFile.OpenStreamForWriteAsync())
                    {
                        System.Drawing.Image image = Render(audioFile, 400, 800, PeakProvider.Average);
                        image.Save(stream, ImageFormat.Png);
                    }
                }
                catch
                {
                    imageFile = await attachmentsFolder.GetFileAsync(waveformFileName);
                }


                Uri uri = new Uri(imageFile.Path);
                BitmapImage bi = new BitmapImage(uri);

            return bi;
        }
    }
}
