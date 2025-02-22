using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Notes.AI.Embeddings;
using Notes.Models;
using Notes.AI.VoiceRecognition;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using Notes.AI.TextRecognition;
using System.Text.Json;

namespace Notes
{
    public static class AttachmentProcessor
    {
        private static List<Attachment> _toBeProcessed = new();
        private static bool _isProcessing = false;

        public static EventHandler<AttachmentProcessedEventArgs> AttachmentProcessed;

        public async static Task AddAttachment(Attachment attachment)
        {
            _toBeProcessed.Add(attachment);

            if (!_isProcessing)
            {
                try
                {
                    _isProcessing = true;
                    await Process();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing attachment: {ex.Message}");
                }
                _isProcessing = false;
            }
        }

        private static async Task Process()
        {
            while (_toBeProcessed.Count > 0)
            {
                var attachment = _toBeProcessed[0];
                _toBeProcessed.RemoveAt(0);

                if (attachment.IsProcessed)
                {
                    continue;
                }

                if (attachment.Type == NoteAttachmentType.Image)
                {
                    await ProcessImage(attachment);
                }
                else if (attachment.Type == NoteAttachmentType.Audio || attachment.Type == NoteAttachmentType.Video)
                {
                    await ProcessAudio(attachment);
                }
            }
        }

        private static async Task ProcessImage(Models.Attachment attachment, EventHandler<float>? progress = null)
        {
            // get softwarebitmap from file
            var attachmentsFolder = await Utils.GetAttachmentsFolderAsync();
            var file = await attachmentsFolder.GetFileAsync(attachment.Filename);

            using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                var recognizedText = await TextRecognition.GetTextFromImage(softwareBitmap);
                if (recognizedText == null)
                {
                    attachment.IsProcessed = true;
                    InvokeAttachmentProcessedComplete(attachment);
                    return;
                }

                var joinedText = string.Join("\n", recognizedText.Lines.Select(l => l.Text));
                var serializedText = JsonSerializer.Serialize(recognizedText);

                var filename = await SaveTextToFileAsync(serializedText, file.DisplayName + ".txt");
                attachment.FilenameForText = filename;

                await SemanticIndex.Instance.AddOrReplaceContent(joinedText, attachment.Id, "attachment", (o, p) =>
                {
                    if (progress != null)
                    {
                        progress.Invoke("Indexing image", 0.5f + (p / 2));
                    }
                });
                attachment.IsProcessed = true;
                InvokeAttachmentProcessedComplete(attachment);

                var context = await AppDataContext.GetCurrentAsync();
                context.Update(attachment);
                await context.SaveChangesAsync();
            }
        }

        private static async Task ProcessAudio(Attachment attachment)
        {
            await Task.Run(async () =>
            {
                var attachmentsFolder = await Utils.GetAttachmentsFolderAsync();
                var file = await attachmentsFolder.GetFileAsync(attachment.Filename);

                var transcribedChunks = await Whisper.TranscribeAsync(file, (o, p) =>
                {
                    if (AttachmentProcessed != null)
                    {
                        AttachmentProcessed.Invoke(null, new AttachmentProcessedEventArgs
                        {
                            AttachmentId = attachment.Id,
                            Progress = p / 2,
                            ProcessingStep = "Transcribing audio"
                        });
                    }
                });

                var textToSave = string.Join("\n", transcribedChunks.Select(t => $@"<|{t.Start:0.00}|>{t.Text}<|{t.End:0.00}|>"));

                var filename = await SaveTextToFileAsync(textToSave, file.DisplayName + ".txt");
                attachment.FilenameForText = filename;

                var textToIndex = string.Join(" ", transcribedChunks.Select(t => t.Text));

                await SemanticIndex.Instance.AddOrReplaceContent(textToIndex, attachment.Id, "attachment", (o, p) =>
                {
                    if (AttachmentProcessed != null)
                    {
                        AttachmentProcessed.Invoke(null, new AttachmentProcessedEventArgs
                        {
                            AttachmentId = attachment.Id,
                            Progress = 0.5f + p / 2,
                            ProcessingStep = "Indexing audio transcript"
                        });
                    }
                });
                attachment.IsProcessed = true;
                InvokeAttachmentProcessedComplete(attachment);

                var context = await AppDataContext.GetCurrentAsync();
                context.Update(attachment);
                await context.SaveChangesAsync();
            });
        }

        private async static Task<string> SaveTextToFileAsync(string text, string filename)
        {
            var stateAttachmentsFolder = await Utils.GetAttachmentsTranscriptsFolderAsync();

            var file = await stateAttachmentsFolder.CreateFileAsync(filename, CreationCollisionOption.GenerateUniqueName);
            await FileIO.WriteTextAsync(file, text);
            return file.Name;
        }

        private static void InvokeAttachmentProcessedComplete(Attachment attachment)
        {
            if (AttachmentProcessed != null)
            {
                AttachmentProcessed.Invoke(null, new AttachmentProcessedEventArgs
                {
                    AttachmentId = attachment.Id,
                    Progress = 1,
                    ProcessingStep = "Complete"
                });
            }
        }

    }

    public class  AttachmentProcessedEventArgs
    {
        public int AttachmentId { get; set; }
        public float Progress { get; set; }
        public string? ProcessingStep { get; set; }
    }
}
