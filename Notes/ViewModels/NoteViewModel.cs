using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Notes.AI.Embeddings;
using Notes.Models;
using Notes.AI.Phi;
using Windows.Graphics.Imaging;
using Windows.Storage;
using System.Collections.Generic;

namespace Notes.ViewModels
{
    public partial class NoteViewModel : ObservableObject
    {
        public readonly Note Note;

        [ObservableProperty]
        private ObservableCollection<AttachmentViewModel> attachments = new();

        [ObservableProperty]
        private ObservableCollection<string> todos = new();

        [ObservableProperty]
        private bool todosLoading = false;

        private DispatcherTimer _saveTimer;
        private bool _contentLoaded = false;

        public DispatcherQueue DispatcherQueue { get; set; }

        public NoteViewModel(Note note)
        {
            Note = note;
            _saveTimer = new DispatcherTimer();
            _saveTimer.Interval = TimeSpan.FromSeconds(5);
            _saveTimer.Tick += SaveTimerTick;
        }

        public string Title
        {
            get => Note.Title;
            set => SetProperty(Note.Title, value, Note, (note, value) =>
            {
                note.Title = value;
                HandleTitleChanged(value);
            });
        }

        public DateTime Modified
        {
            get => Note.Modified;
            set => SetProperty(Note.Modified, value, Note, (note, value) => note.Modified = value);
        }

        [ObservableProperty]
        private string content;

        private async Task HandleTitleChanged(string value)
        {
            var folder = await Utils.GetLocalFolderAsync();
            var file = await folder.GetFileAsync(Note.Filename);

            await file.RenameAsync(value.Trim() + Utils.FileExtension, NameCollisionOption.GenerateUniqueName);
            Note.Filename = file.Name;
            await AppDataContext.SaveCurrentAsync();
        }

        private async Task SaveContentAsync()
        {
            var folder = await Utils.GetLocalFolderAsync();
            var file = await folder.GetFileAsync(Note.Filename);
            await FileIO.WriteTextAsync(file, Content);
        }

        public async Task LoadContentAsync()
        {
            if (_contentLoaded)
            {
                return;
            }

            _contentLoaded = true;

            var folder = await Utils.GetLocalFolderAsync();
            var file = await folder.GetFileAsync(Note.Filename);
            content = await FileIO.ReadTextAsync(file);

            var context = await AppDataContext.GetCurrentAsync();
            var attachments = context.Attachments.Where(a => a.NoteId == Note.Id).ToList();
            foreach (var attachment in attachments)
            {
                Attachments.Add(new AttachmentViewModel(attachment));
            }
        }

        partial void OnContentChanged(string value)
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        public async Task AddAttachmentAsync(StorageFile file)
        {
            var attachmentsFolder = await Utils.GetAttachmentsFolderAsync();
            bool shouldCopyFile = true;

            var attachment = new Attachment()
            {
                IsProcessed = false,
                Note = Note
            };

            if (new string[] { ".png", ".jpg", ".jpeg"}.Contains(file.FileType))
            {
                attachment.Type = NoteAttachmentType.Image;
            }
            else if (new string[] { ".mp3", ".wav", ".m4a", ".opus", ".waptt" }.Contains(file.FileType))
            {
                attachment.Type = NoteAttachmentType.Audio;
                file = await Utils.SaveAudioFileAsWav(file, attachmentsFolder);
                shouldCopyFile = false;
            }
            else if (file.FileType == ".mp4")
            {
                attachment.Type = NoteAttachmentType.Video;
            }
            else
            {
                attachment.Type = NoteAttachmentType.Document;
            }

            if (shouldCopyFile && !file.Path.StartsWith(attachmentsFolder.Path))
            {
                file = await file.CopyAsync(attachmentsFolder, file.Name, NameCollisionOption.GenerateUniqueName);
            }

            attachment.Filename = file.Name;

            Attachments.Add(new AttachmentViewModel(attachment));

            var context = await AppDataContext.GetCurrentAsync();
            await context.Attachments.AddAsync(attachment);

            await context.SaveChangesAsync();

            AttachmentProcessor.AddAttachment(attachment);
        }

        public async Task RemoveAttachmentAsync(AttachmentViewModel attachmentViewModel)
        {
            Attachments.Remove(attachmentViewModel);

            var attachment = attachmentViewModel.Attachment;
            Note.Attachments.Remove(attachment);

            var attachmentsFolder = await Utils.GetAttachmentsFolderAsync();
            var file = await attachmentsFolder.GetFileAsync(attachment.Filename);
            await file.DeleteAsync();

            if (attachment.IsProcessed && !string.IsNullOrEmpty(attachment.FilenameForText))
            {
                var attachmentsTranscriptFolder = await Utils.GetAttachmentsTranscriptsFolderAsync();
                var transcriptFile = await attachmentsTranscriptFolder.GetFileAsync(attachment.FilenameForText);
                await transcriptFile.DeleteAsync();
            }

            var context = await AppDataContext.GetCurrentAsync();
            context.Attachments.Remove(attachment);
            context.TextChunks.RemoveRange(context.TextChunks.Where(tc => tc.SourceId == attachment.Id && tc.ContentType == "attachment"));

            await context.SaveChangesAsync();
        }

        public async Task ShowTodos()
        {
            if (!TodosLoading && (Todos == null || Todos.Count == 0))
            {
                DispatcherQueue.TryEnqueue(() => TodosLoading = true);
                var todos = await Phi3.Instance.GetTodoItemsFromText(Content);
                if (todos != null && todos.Count > 0)
                {
                    DispatcherQueue.TryEnqueue(() => Todos = new ObservableCollection<string>(todos));
                }
            }

            DispatcherQueue.TryEnqueue(() => TodosLoading = false);
        }

        public async Task AddAttachmentAsync(SoftwareBitmap bitmap)
        {
            // save bitmap to file
            var attachmentsFolder = await Utils.GetAttachmentsFolderAsync();
            var file = await attachmentsFolder.CreateFileAsync(Guid.NewGuid().ToString() + ".png", CreationCollisionOption.GenerateUniqueName);
            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetSoftwareBitmap(bitmap);
                await encoder.FlushAsync();
            }

            await AddAttachmentAsync(file);
        }

        private void SaveTimerTick(object? sender, object e)
        {
            _saveTimer.Stop();
            SaveContentToFileAndReIndex();
        }

        private async Task SaveContentToFileAndReIndex()
        {
            var folder = await Utils.GetLocalFolderAsync();
            var file = await folder.GetFileAsync(Note.Filename);

            Debug.WriteLine("Saving note " + Note.Title + " to filename " + Note.Filename);
            await FileIO.WriteTextAsync(file, Content);

            await SemanticIndex.Instance.AddOrReplaceContent(Content, Note.Id, "note", (o, p) => Debug.WriteLine($"Indexing note {Note.Title} {p * 100}%"));
        }
    }
}
