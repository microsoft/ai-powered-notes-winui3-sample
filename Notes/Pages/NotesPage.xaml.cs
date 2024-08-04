using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Notes.Models;
using Notes.AI.Phi;
using Notes.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Notes.Pages
{
    public sealed partial class NotesPage : Page
    {
        DispatcherTimer _autosugestionTimer;
        string _textToAutoComplete = string.Empty;
        string _suggestedText = string.Empty;
        CancellationTokenSource _autosuggestCts;

        NoteViewModel ViewModel;

        public NotesPage()
        {
            this.InitializeComponent();

            _autosugestionTimer = new DispatcherTimer();
            _autosugestionTimer.Interval = TimeSpan.FromMilliseconds(600);
            _autosugestionTimer.Tick += Timer_Tick;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter != null)
            {
                ViewModel = (NoteViewModel)e.Parameter;
                ViewModel.DispatcherQueue = DispatcherQueue;

                await ViewModel.LoadContentAsync();

                var paragraphFormat = ContentsRichEditBox.Document.GetDefaultParagraphFormat();
                paragraphFormat.SetLineSpacing(Microsoft.UI.Text.LineSpacingRule.OneAndHalf, 2f);
                ContentsRichEditBox.Document.SetDefaultParagraphFormat(paragraphFormat);

                ContentsRichEditBox.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, ViewModel.Content);
                ContentsRichEditBox.IsEnabled = true;
                ContentsRichEditBox.Focus(FocusState.Programmatic);
            }
        }
        
        private void ContentsRichEditBox_Loaded(object sender, RoutedEventArgs e)
        {
            ContentsRichEditBox.SelectionFlyout.Opening += Menu_Opening;
            ContentsRichEditBox.ContextFlyout.Opening += Menu_Opening;

        }

        private void ContentsRichEditBox_Unloaded(object sender, RoutedEventArgs e)
        {
            ContentsRichEditBox.SelectionFlyout.Opening -= Menu_Opening;
            ContentsRichEditBox.ContextFlyout.Opening -= Menu_Opening;

        }

        private void Menu_Opening(object sender, object e)
        {
            CommandBarFlyout myFlyout = sender as CommandBarFlyout;
            if (myFlyout.Target == ContentsRichEditBox)
            {
                AppBarButton summarizeButton = new AppBarButton();
                summarizeButton.Icon = new SymbolIcon(Symbol.Document);
                summarizeButton.Label = "Summarize";
                summarizeButton.Click += (object sender, RoutedEventArgs e) =>
                {
                    var text = ContentsRichEditBox.Document.Selection.Text;

                    myFlyout.Hide();

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return;
                    }

                    MainWindow.Phi3View.ShowAndSummarize(text);
                };
                myFlyout.PrimaryCommands.Add(summarizeButton);


                AppBarButton fixButton = new AppBarButton();
                fixButton.Icon = new SymbolIcon(Symbol.Document);
                fixButton.Label = "Fix and clean up";
                fixButton.Click += (object sender, RoutedEventArgs e) =>
                {
                    var text = ContentsRichEditBox.Document.Selection.Text;

                    myFlyout.Hide();

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return;
                    }

                    MainWindow.Phi3View.FixAndCleanUp(text);
                };
                myFlyout.PrimaryCommands.Add(fixButton);
            }
        }

        private void ContentsRichEditBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
        }

        private void ProcessAutosuggestionIfNeeded()
        {
            CancelSuggestion();

            ContentsRichEditBox.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out var text);
            var startPosition = ContentsRichEditBox.Document.Selection.StartPosition;
            var lines = text.Split('\r');
            var lineStartPositions = new List<int>();
            var count = 0;
            foreach (var line in lines)
            {
                // Add line number or empty space
                lineStartPositions.Add(count);
                count += line != "" ? line.Length + 1 : 1;
            }

            var lineIndex = 0;
            while (startPosition >= lineStartPositions[lineIndex])
            {
                lineIndex++;
            }
            var currentLine = lines[lineIndex - 1];
            var currentLineStartPosition = startPosition - lineStartPositions[lineIndex - 1];
            var begininingOfLine = currentLine.Substring(0, currentLineStartPosition);
            var restOfLIne = currentLine.Substring(currentLineStartPosition);



            if (string.IsNullOrWhiteSpace(restOfLIne) && !string.IsNullOrWhiteSpace(begininingOfLine))
            {
                _autosugestionTimer.Stop();
                _textToAutoComplete = begininingOfLine;
                _autosugestionTimer.Start();
            }
            else
            {
                CancelSuggestion();
            }
        }

        private void CancelSuggestion()
        {
            _autosugestionTimer.Stop();
            AutoSuggestBorder.Visibility = Visibility.Collapsed;
            _textToAutoComplete = string.Empty;

            if (_autosuggestCts != null)
            {
                _autosuggestCts.Cancel();
                _autosuggestCts = null;
            }

            _suggestedText = string.Empty;
        }

        private async void Timer_Tick(object sender, object e)
        {
            _autosugestionTimer.Stop();

            if (string.IsNullOrWhiteSpace(_textToAutoComplete))
            {
                return;
            }

            var line = _textToAutoComplete.Trim();
            //var sentences = line.Split('.');
            //string sentenceToComplete = sentences.Last();

            //if (string.IsNullOrWhiteSpace(sentenceToComplete) && sentences.Length > 1)
            //{
            //    sentenceToComplete = sentences[sentences.Length - 2];
            //}
            //else if (string.IsNullOrWhiteSpace(sentenceToComplete))
            //{
            //    return;
            //}

            var suggestion = await AutoComplete(line);

            if (!string.IsNullOrWhiteSpace(suggestion))
            {
                _suggestedText = suggestion;
                PositionSuggestion(suggestion);
            }
        }

        private void PositionSuggestion(string suggestion)
        {
            ContentsRichEditBox.Document.Selection.GetRect(Microsoft.UI.Text.PointOptions.ClientCoordinates, out var rect, out var hit);
            //GeneralTransform transform = ContentsRichEditBox.TransformToVisual(this);
            //Point caretPositionInWindow = transform.TransformPoint(new Point(rect.X, rect.Y));
            var margin = 0;
            AutoSuggestBorder.Margin = new Thickness(rect.X + margin, rect.Y + margin, 0, 0);

            SuggestedTextBlock.Text = (_textToAutoComplete.Last() == ' ' ? "" : " ") + suggestion.Trim();
            AutoSuggestBorder.Visibility = Visibility.Visible;
        }

        int count = 0;

        private Task<string?> AutoComplete(string input)
        {
            var id = count++;
            var cts = new CancellationTokenSource();
            _autosuggestCts = cts;
            return Task.Run(async () =>
            {
                string suggestion = string.Empty;
                Debug.WriteLine($"[{id}]Autosuggestion for {input}: ");
                await foreach (var partial in Phi3.Instance.AutocompleteSentenceAsync(input, cts.Token))
                {
                    if (partial.Contains(".") || partial.Contains("!") || partial.Contains("?") || partial.Contains("\r"))
                    {
                        suggestion += partial;
                        Debug.WriteLine($"[{id}]{suggestion}");
                        break;
                    }

                    if (cts.IsCancellationRequested)
                    {
                        Debug.WriteLine($"[{id}]Autosuggestion for {input} was canceled");
                        return null;
                    }

                    suggestion += partial;
                    Debug.WriteLine($"[{id}]{suggestion}");
                }

                if (cts.IsCancellationRequested)
                {
                    Debug.WriteLine($"[{id}]Autosuggestion for {input} was canceled");
                    return null;
                }

                cts.Cancel();
                Debug.WriteLine($"[{id}]Autosuggestion for {input} returning {suggestion}");
                return suggestion;
            });
        }

        private void ContentsRichEditBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Tab && !string.IsNullOrWhiteSpace(_suggestedText))
            {
                ContentsRichEditBox.Document.Selection.SetText(Microsoft.UI.Text.TextSetOptions.None, (_textToAutoComplete.Last() == ' ' ? "" : " ") + _suggestedText);
                ContentsRichEditBox.Document.Selection.StartPosition += _suggestedText.Length;
                _suggestedText = string.Empty;
                e.Handled = true;
            }
        }

        private void ContentsRichEditBox_TextChanged(object sender, RoutedEventArgs e)
        {
            if (!ContentsRichEditBox.IsEnabled)
            {
                return;
            }

            ContentsRichEditBox.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out var content);
            if (ViewModel.Content.Trim() != content.Trim())
            {
                Debug.WriteLine("Text changed");
                ViewModel.Content = content.Trim();
                ProcessAutosuggestionIfNeeded();
            }

        }

        private async void ContentsRichEditBox_Paste(object sender, TextControlPasteEventArgs e)
        {
            var dataPackage = Clipboard.GetContent();

            if (dataPackage.Contains(StandardDataFormats.Text) ||
                dataPackage.Contains(StandardDataFormats.Rtf))
            {
                return;
            }

            e.Handled = true;

            await HandleDataPackage(dataPackage);
        }

        private async Task HandleDataPackage(DataPackageView dataPackage)
        {
            if (dataPackage.Contains(StandardDataFormats.Bitmap))
            {
                var imageStreamReference = await dataPackage.GetBitmapAsync();
                var imageStream = await imageStreamReference.OpenReadAsync();
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(imageStream);

                await ViewModel.AddAttachmentAsync(await decoder.GetSoftwareBitmapAsync());
            }
            else if (dataPackage.Contains(StandardDataFormats.StorageItems))
            {
                var storageItems = await dataPackage.GetStorageItemsAsync();

                foreach (var storageItem in storageItems)
                {
                    if (storageItem is StorageFile storageFile)
                    {
                        await ViewModel.AddAttachmentAsync(storageFile);
                    }
                }
            }
        }
        public static string AttachmentTypeToEmoji(NoteAttachmentType type)
        {
            return type switch
            {
                NoteAttachmentType.Image => "🖼️",
                NoteAttachmentType.Audio => "🎙️",
                NoteAttachmentType.Video => "🎞️",
                NoteAttachmentType.Document => "📄"
            };
        }

        private void TodosClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.ShowTodos();
        }

        private void ContactDeleteMenuyItem_Click(object sender, RoutedEventArgs e)
        {
            var attachment = (sender as FrameworkElement).DataContext as AttachmentViewModel;
            ViewModel.RemoveAttachmentAsync(attachment);

        }

        private void AttachmentsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            AttachmentViewModel attachmentViewModel = (AttachmentViewModel)e.ClickedItem;
            ((Application.Current as App)?.Window as MainWindow).OpenAttachmentView(attachmentViewModel);
        }

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            await HandleDataPackage(e.DataView);
        }
    }
}
