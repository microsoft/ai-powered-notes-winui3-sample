using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Notes.ViewModels;
using System.Threading.Tasks;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Notes.Controls
{
    public sealed partial class SearchView : UserControl
    {
        public SearchViewModel ViewModel { get; } = new SearchViewModel();


        public SearchView()
        {
            this.InitializeComponent();
            this.KeyDown += SearchView_KeyDown;
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var context = await AppDataContext.GetCurrentAsync();

            var item = e.ClickedItem as SearchResult;
            if (item.ContentType == ContentType.Note)
            {
                MainWindow.Instance.SelectNoteById(item.SourceId);
            }
            else
            {
                var attachment = context.Attachments.Where(a => a.Id == item.SourceId).FirstOrDefault();
                if (attachment != null)
                {
                    var note = context.Notes.Where(n => n.Id == attachment.NoteId).FirstOrDefault();
                    MainWindow.Instance.SelectNoteById(note.Id, attachment.Id, item.MostRelevantSentence ?? null);
                }
            }

            this.Visibility = Visibility.Collapsed;
        }

        private void SearchView_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                this.Visibility = Visibility.Collapsed;
            }
        }

        public void Show(string? text = null)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                SearchBox.Text = text;
                ViewModel.Reset();
            }

            this.Visibility = Visibility.Visible;
            Task.Run(async () => 
            {
                await Task.Delay(100);
                DispatcherQueue.TryEnqueue(() =>
                {
                    SearchBox.Focus(FocusState.Programmatic);
                });
            });
        }



        private void BackgroundTapped(object sender, TappedRoutedEventArgs e)
        {
            // hide the search view only when the backround was tapped but not any of the content inside
            if (e.OriginalSource == Root)
                this.Visibility = Visibility.Collapsed;
        }

        private async void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            Debug.WriteLine("Text changed");
            ViewModel.HandleTextChanged(sender.Text);
        }
    }
}
