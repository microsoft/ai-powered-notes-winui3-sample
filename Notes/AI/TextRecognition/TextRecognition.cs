using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Microsoft.Windows.AI.Imaging;

namespace Notes.AI.TextRecognition
{
    internal static class TextRecognition
    {
        public static async Task<string[]> GetTextFromImage(SoftwareBitmap image)
        {
            await TextRecognizer.MakeAvailableAsync();
            var textRecognizer = await TextRecognizer.CreateAsync();

            var options = new TextRecognizerOptions();

            // create ImageBuffer from image
            var imageBuffer = ImageBuffer.CreateBufferAttachedToBitmap(image);

            var recognizedText = await textRecognizer.RecognizeTextFromImageAsync(imageBuffer, options);
            return recognizedText.Lines.Select(n => n.Text).ToArray();
        }

        public static async Task<RecognizedText> GetSavedText(string filename)
        {
            var folder = await Utils.GetAttachmentsTranscriptsFolderAsync();
            var file = await folder.GetFileAsync(filename);

            var text = await FileIO.ReadTextAsync(file);

            var lines = JsonSerializer.Deserialize<RecognizedText>(text);
            return lines;
        }
    }
}
