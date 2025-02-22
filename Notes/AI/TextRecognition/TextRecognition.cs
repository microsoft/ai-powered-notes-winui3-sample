//using Microsoft.Windows.Imaging;
//using Microsoft.Windows.Vision;
using Microsoft.Graphics.Imaging;
using Microsoft.Windows.Vision;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace Notes.AI.TextRecognition
{
    internal static class TextRecognition
    {
        public static async Task<ImageText?> GetTextFromImage(SoftwareBitmap image)
        {
            if (!TextRecognizer.IsAvailable())
            {
                var op = await TextRecognizer.MakeAvailableAsync();
                if (op.Status != Microsoft.Windows.Management.Deployment.PackageDeploymentStatus.CompletedSuccess)
                {
                    return null;
                }
            }

            var textRecognizer = await TextRecognizer.CreateAsync();

            using var imageBuffer = ImageBuffer.CreateBufferAttachedToBitmap(image);
            RecognizedText? result = textRecognizer?.RecognizeTextFromImage(imageBuffer, new TextRecognizerOptions());

            return ImageText.GetFromRecognizedText(result);
        }

        public static async Task<ImageText> GetSavedText(string filename)
        {
            var folder = await Utils.GetAttachmentsTranscriptsFolderAsync();
            var file = await folder.GetFileAsync(filename);

            var text = await FileIO.ReadTextAsync(file);

            var lines = JsonSerializer.Deserialize<ImageText>(text);
            return lines ?? new ImageText();
        }
    }
}
