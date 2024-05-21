using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Notes.Controls;
using Notes.AI.Embeddings;
using Notes.Models;
using Notes.Pages;
using Notes.ViewModels;

namespace Notes
{
    public sealed partial class MainWindow : Window
    {

        public static Phi3View Phi3View;
        public static SearchView SearchView;
        public static MainWindow Instance;
        public ViewModel VM;

        public MainWindow()
        {
            VM = new ViewModel();
            this.InitializeComponent();

            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(TitleBar);

            Instance = this;
            Phi3View = phi3View;
            SearchView = searchView;

            VM.Notes.CollectionChanged += Notes_CollectionChanged;
        }

        public async Task SelectNoteById(int id, int? attachmentId = null, string? attachmentText = null)
        {
            var note = VM.Notes.Where(n => n.Note.Id == id).FirstOrDefault();
            if (note != null)
            {
                navView.SelectedItem = note;

                if (attachmentId.HasValue)
                { 
                    var attachmentViewModel = note.Attachments.Where(a => a.Attachment.Id == attachmentId).FirstOrDefault();
                    if (attachmentViewModel == null)
                    {
                        var context = await AppDataContext.GetCurrentAsync();
                        var attachment = context.Attachments.Where(a => a.Id == attachmentId.Value).FirstOrDefault();
                        if (attachment == null)
                        {
                            return;
                        }

                        attachmentViewModel = new AttachmentViewModel(attachment);
                    }

                    OpenAttachmentView(attachmentViewModel, attachmentText);
                }
            }
        }

        private void navView_Loaded(object sender, RoutedEventArgs e)
        {
            if (navView.MenuItems.Count > 0)
                navView.SelectedItem = navView.MenuItems[0];
        }

        private void Notes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (navView.SelectedItem == null && VM.Notes.Count > 0)
                navView.SelectedItem = VM.Notes[0];
        }

        private async void NewButton_Click(object sender, RoutedEventArgs e)
        {
            var note = await VM.CreateNewNote();
            navView.SelectedItem = note;
        }

        private void navView_SelectionChanged(NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NoteViewModel note)
            {
                navFrame.Navigate(typeof(NotesPage), note);
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            searchView.Show();
        }

        private void AskMyNotesClicked(object sender, RoutedEventArgs e)
        {
            phi3View.ShowForRag();
        }

        public void OpenAttachmentView(AttachmentViewModel attachment, string? attachmentText = null)
        {
            attachmentView.UpdateAttachment(attachment, attachmentText);
            attachmentView.Show();
        }
    }

    class MenuItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate NoteTemplate { get; set; }
        public DataTemplate DefaultTemplate { get; set; }
        protected override DataTemplate SelectTemplateCore(object item)
        {
            return item is NoteViewModel ? NoteTemplate : DefaultTemplate;
        }
    }
}
