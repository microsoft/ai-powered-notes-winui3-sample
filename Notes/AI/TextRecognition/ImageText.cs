using Microsoft.Windows.Vision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notes.AI.TextRecognition
{
    internal class ImageText
    {
        public List<RecognizedTextLine> Lines { get; set; } = new();
        public double ImageAngle { get; set; }

        public static ImageText GetFromRecognizedText(RecognizedText? recognizedText)
        {
            ImageText attachmentRecognizedText = new();

            if (recognizedText == null)
            {
                return attachmentRecognizedText;
            }

            attachmentRecognizedText.ImageAngle = recognizedText.ImageAngle;
            attachmentRecognizedText.Lines = recognizedText.Lines.Select(l => new RecognizedTextLine
            {
                Text = l.Text,
                X = l.BoundingBox.TopLeft.X,
                Y = l.BoundingBox.TopLeft.Y,
                Width = Math.Abs(l.BoundingBox.TopRight.X - l.BoundingBox.TopLeft.X),
                Height = Math.Abs(l.BoundingBox.BottomLeft.Y - l.BoundingBox.TopLeft.Y)
            }).ToList();

            return attachmentRecognizedText;
        }
    }

    internal class RecognizedTextLine
    {
        public string Text { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
