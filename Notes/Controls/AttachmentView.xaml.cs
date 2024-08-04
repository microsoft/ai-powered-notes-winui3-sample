using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Threading;
using Windows.Storage;
using Windows.Media.Core;
using Microsoft.UI.Dispatching;
using Notes.Models;
using Notes.AI.VoiceRecognition;
using Notes.ViewModels;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using Notes.AI.TextRecognition;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Notes.Controls
{
    public sealed partial class AttachmentView : UserControl
    {
        private CancellationTokenSource _cts;
        private DispatcherQueue _dispatcher;
        private Timer _timer;

        public ObservableCollection<TranscriptionBlock> TranscriptionBlocks { get; set; } = new ObservableCollection<Models.TranscriptionBlock>();
        public AttachmentViewModel AttachmentVM { get; set; }
    public bool AutoScrollEnabled { get; set; } = true;

        public AttachmentView()
        {
            this.InitializeComponent();
            this.Visibility = Visibility.Collapsed;
            this._dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        public async Task Show()
        {
            this.Visibility = Visibility.Visible;
        }

        public void Hide()
        {
            TranscriptionBlocks.Clear();
            transcriptLoadingProgressRing.IsActive = false;
            AttachmentImage.Source = null;
            WaveformImage.Source = null;
            this.Visibility = Visibility.Collapsed;

            if (AttachmentVM.Attachment.Type == NoteAttachmentType.Video || AttachmentVM.Attachment.Type == NoteAttachmentType.Audio)
            {
                ResetMediaPlayer();  
            }
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }

        private void BackgroundTapped(object sender, TappedRoutedEventArgs e)
        {
            // hide the search view only when the backround was tapped but not any of the content inside
            if (e.OriginalSource == Root)
                this.Hide();
        }

        public async Task UpdateAttachment(AttachmentViewModel attachment, string? attachmentText = null)
        {
            AttachmentImageTextCanvas.Children.Clear();

            AttachmentVM = attachment;
            StorageFolder attachmentsFolder = await Utils.GetAttachmentsFolderAsync();
            StorageFile attachmentFile = await attachmentsFolder.GetFileAsync(attachment.Attachment.Filename);
            switch(AttachmentVM.Attachment.Type)
            {
                case NoteAttachmentType.Audio: 
                    ImageGrid.Visibility = Visibility.Collapsed;
                    MediaGrid.Visibility = Visibility.Visible;
                    RunWaitForTranscriptionTask(attachmentText);
                    WaveformImage.Source = await WaveformRenderer.GetWaveformImage(attachmentFile);
                    SetMediaPlayerSource(attachmentFile);
                    break;
                case NoteAttachmentType.Image:
                    ImageGrid.Visibility = Visibility.Visible;
                    MediaGrid.Visibility = Visibility.Collapsed;
                    AttachmentImage.Source = new BitmapImage(new Uri(attachmentFile.Path));
                    LoadImageText(attachment.Attachment.Filename);
                    break;
                case NoteAttachmentType.Video:
                    ImageGrid.Visibility = Visibility.Collapsed;
                    MediaGrid.Visibility = Visibility.Visible;
                    RunWaitForTranscriptionTask(attachmentText);
                    SetMediaPlayerSource(attachmentFile);
                    break;
            }
        }

        private async Task LoadImageText(string fileName)
        {
            var text = await TextRecognition.GetSavedText("for-demo-only.txt"); //(fileName.Split('.')[0] + ".txt");
            foreach (var line in text.Lines)
            {
                AttachmentImageTextCanvas.Children.Add(
                    new Border()
                    {
                        Child = new Viewbox()
                        {
                            Child = new TextBlock()
                            {
                                Text = line.Text,
                                FontSize = 16,
                                IsTextSelectionEnabled = true,
                                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                            },
                            Stretch = Stretch.Fill,
                            StretchDirection = StretchDirection.Both,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(4),
                            Height = line.Height,
                            Width = line.Width,
                        },
                        Height = line.Height + 8,
                        Width = line.Width + 8,
                        CornerRadius = new CornerRadius(8),
                        Margin = new Thickness(line.X - 4, line.Y - 4, 0, 0),
                        RenderTransform = new RotateTransform() { Angle = text.ImageAngle },
                        BorderThickness = new Thickness(0),
                        Background = new LinearGradientBrush()
                        {
                            GradientStops = new GradientStopCollection()
                            {
                                new GradientStop() { Color = Color.FromArgb(20, 52, 185, 159), Offset = 0.1 },
                                new GradientStop() { Color = Color.FromArgb(20, 50, 181, 173), Offset = 0.5 },
                                new GradientStop() { Color = Color.FromArgb(20, 59, 177, 119), Offset = 0.9 }
                            }
                        },
                        //BorderBrush = new LinearGradientBrush()
                        //{
                        //    GradientStops = new GradientStopCollection()
                        //    {
                        //        new GradientStop() { Color = Color.FromArgb(255, 147, 89, 248), Offset = 0.1 },
                        //        new GradientStop() { Color = Color.FromArgb(255, 203, 123, 190), Offset = 0.5 },
                        //        new GradientStop() { Color = Color.FromArgb(255, 240, 184, 131), Offset = 0.9 },
                        //    },
                        //},
                    }
                );
            }
        }

        private void SetMediaPlayerSource(StorageFile file)
        {
            mediaPlayer.Source = MediaSource.CreateFromStorageFile(file);
            mediaPlayer.MediaPlayer.CurrentStateChanged += MediaPlayer_CurrentStateChanged;
        }

        private async void RunWaitForTranscriptionTask(string? transcriptionTextToTryToShow = null)
        {
            transcriptLoadingProgressRing.IsActive = true;
            _ = Task.Run(async () =>
            {
                while (AttachmentVM.IsProcessing)
                {
                    Thread.Sleep(500);
                }
                StorageFile transcriptFile = await (await Utils.GetAttachmentsTranscriptsFolderAsync()).GetFileAsync(AttachmentVM.Attachment.FilenameForText);
                string rawTranscription = File.ReadAllText(transcriptFile.Path);
                _dispatcher.TryEnqueue(() =>
                {
                    transcriptLoadingProgressRing.IsActive = false;
                    var transcripts = WhisperUtils.ProcessTranscriptionWithTimestamps(rawTranscription);
                    
                    foreach (var t in transcripts)
                    {
                        TranscriptionBlocks.Add(new TranscriptionBlock(t.Text, t.Start, t.End));
                    }

                    if (transcriptionTextToTryToShow != null)
                    {
                        var block = TranscriptionBlocks.Where(t => t.Text.Contains(transcriptionTextToTryToShow)).FirstOrDefault();
                        if (block != null)
                        {
                            transcriptBlocksListView.SelectedItem = block;
                            ScrollTranscriptionToItem(block);
                        }
                    }
                });
            });
        }

        private void MediaPlayer_CurrentStateChanged(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            if (sender.CurrentState.ToString() == "Playing")
            {
                _timer = new Timer(CheckTimestampAndSelectTranscription, null, 0, 250);
            }
            else if(_timer != null)
            {
                _timer.Dispose();
            }
        }

        private void CheckTimestampAndSelectTranscription(object? state)
        {
            _dispatcher.TryEnqueue(() => {
                TimeSpan currentPos = mediaPlayer.MediaPlayer.Position;
                foreach (TranscriptionBlock block in TranscriptionBlocks)
                {
                    if (block.Start < currentPos & block.End > currentPos)
                    {
                        transcriptBlocksListView.SelectionChanged -= TranscriptBlocksListView_SelectionChanged;
                        transcriptBlocksListView.SelectedItem = block;
                        transcriptBlocksListView.SelectionChanged += TranscriptBlocksListView_SelectionChanged;
                        ScrollTranscriptionToItem(block);
                        break;
                    }
                }
            });   
        }

        private void ResetMediaPlayer()
        {
            if(_timer != null)
            {
                _timer.Dispose();
            } 
            mediaPlayer.MediaPlayer.CurrentStateChanged -= MediaPlayer_CurrentStateChanged;
            mediaPlayer.MediaPlayer.Pause();
            mediaPlayer.Source = null;
        }

        private void TranscriptBlocksListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView transcriptListView)
            {
                TranscriptionBlock selectedBlock = (TranscriptionBlock)transcriptListView.SelectedItem;
                if (selectedBlock != null)
                {
                    mediaPlayer.MediaPlayer.Position = selectedBlock.Start;
                }
            }
        }

        private void ScrollTranscriptionToItem(TranscriptionBlock block)
        {
            if(AutoScrollEnabled)
            {
                transcriptBlocksListView.ScrollIntoView(block, ScrollIntoViewAlignment.Leading);
            }
        }
    }
}
