using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Notes.Models;
using Windows.Storage;

namespace Notes.ViewModels
{
    public class ViewModel
    {
        public ObservableCollection<NoteViewModel> Notes { get; set; } = new();

        public ViewModel()
        {
            LoadNotes();
        }

        public async Task<NoteViewModel> CreateNewNote()
        {
            var title = "New note";

            var folder = await Utils.GetLocalFolderAsync();
            var file = await folder.CreateFileAsync(title + Utils.FileExtension, CreationCollisionOption.GenerateUniqueName);

            var note = new Note()
            {
                Title = title,
                Created = DateTime.Now,
                Modified = DateTime.Now,
                Filename = file.Name
            };

            var noteViewModel = new NoteViewModel(note);
            Notes.Insert(0, noteViewModel);
            var dataContext = await AppDataContext.GetCurrentAsync();
            dataContext.Notes.Add(note);
            await dataContext.SaveChangesAsync();

            return noteViewModel;
        }

        private async Task LoadNotes()
        {
            var dataContext = await AppDataContext.GetCurrentAsync();
            var savedNotes = dataContext.Notes.Select(note => note).ToList();

            StorageFolder notesFolder = await Utils.GetLocalFolderAsync();
            var files = await notesFolder.GetFilesAsync();
            var filenames = files.ToDictionary(f => f.Name, f=> f);

            foreach (var note in savedNotes)
            {
                if (filenames.ContainsKey(note.Filename))
                {
                    filenames.Remove(note.Filename);
                    Notes.Add(new NoteViewModel(note));
                }
                else
                {
                    // delete note from db
                    dataContext.Notes.Remove(note);
                }
            }

            foreach (var filename in filenames)
            {
                if (filename.Key.EndsWith(Utils.FileExtension))
                {
                    var file = filename.Value;
                    var note = new Note()
                    {
                        Title = file.DisplayName,
                        Created = file.DateCreated.DateTime,
                        Filename = file.Name,
                        Modified = DateTime.Now
                    };
                    dataContext.Notes.Add(note);
                    Notes.Add(new NoteViewModel(note));
                }
            }

            await dataContext.SaveChangesAsync();
        }
    }
}
