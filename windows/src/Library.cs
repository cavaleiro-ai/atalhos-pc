// Biblioteca de sons: cada subpasta de "biblioteca", ao lado do programa, e uma categoria. Um PNG com o nome da pasta e o icone dela.
// O aparelho nunca manda caminho, so categoria e nome de arquivo procurados no indice: nada fora da pasta pode ser pedido.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AtalhosPC
{
    public sealed class LibraryItem
    {
        public string Category;
        public string File;
        public string Name;
        public string Path;
        public string Search;
    }

    public sealed class LibraryCategory
    {
        public string Folder;
        public string Title;
        public string Icon; // null quando a pasta nao tem PNG
        public List<LibraryItem> Items;
    }

    public sealed class Library
    {
        // Nomes das pastas copiadas como vieram; uma pasta nova aparece com o proprio nome.
        static readonly Dictionary<string, string> Titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Anime & Manga", "Anime e mangá" }, { "Games", "Jogos" }, { "Memes", "Memes" }, { "Movies", "Filmes" },
            { "Music", "Música" }, { "Politics", "Política" }, { "Pranks", "Pegadinhas" }, { "Reactions", "Reações" },
            { "Sound Effects", "Efeitos sonoros" }, { "Sports", "Esportes" }, { "Television", "Televisão" },
            { "Tiktok Trends", "Tendências do TikTok" }, { "Viral", "Virais" }, { "Whatsapp Audios", "Áudios de WhatsApp" }
        };

        public readonly string Root;
        volatile List<LibraryCategory> index;

        public Library(string root)
        {
            Root = root;
        }

        // Le a pasta de novo: e chamado ao abrir a biblioteca, entao arquivo novo aparece sem reiniciar o programa.
        public List<LibraryCategory> Categories()
        {
            List<LibraryCategory> found = new List<LibraryCategory>();
            if (Directory.Exists(Root))
            {
                foreach (string dir in Directory.GetDirectories(Root))
                {
                    string folder = System.IO.Path.GetFileName(dir);
                    string title;
                    if (!Titles.TryGetValue(folder, out title)) title = folder;
                    string icon = System.IO.Path.Combine(Root, folder + ".png");
                    List<LibraryItem> items = Directory.GetFiles(dir).Where(Store.IsAudio).Select(f =>
                    {
                        string name = System.IO.Path.GetFileNameWithoutExtension(f);
                        return new LibraryItem { Category = folder, File = System.IO.Path.GetFileName(f), Name = name, Path = f, Search = Fold(name) };
                    }).OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                    if (items.Count == 0) continue;
                    found.Add(new LibraryCategory { Folder = folder, Title = title, Icon = File.Exists(icon) ? icon : null, Items = items });
                }
            }
            found = found.OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
            index = found;
            return found;
        }

        List<LibraryCategory> Index()
        {
            return index ?? Categories();
        }

        // Sem acento e sem maiuscula: "agua" acha "Água".
        static string Fold(string text)
        {
            StringBuilder sb = new StringBuilder(text.Length);
            foreach (char c in text.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        // Categoria vazia procura em todas. Todas as palavras precisam aparecer, em qualquer ordem.
        public List<LibraryItem> Search(string category, string query)
        {
            IEnumerable<LibraryItem> items;
            if (string.IsNullOrEmpty(category))
            {
                items = Index().SelectMany(c => c.Items);
                if (string.IsNullOrWhiteSpace(query)) return items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            }
            else
            {
                LibraryCategory c = FindCategory(category);
                if (c == null) return new List<LibraryItem>();
                items = c.Items;
            }
            if (string.IsNullOrWhiteSpace(query)) return items.ToList();
            string[] words = Fold(query).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            return items.Where(i => words.All(w => i.Search.Contains(w))).OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public LibraryCategory FindCategory(string folder)
        {
            return Index().FirstOrDefault(c => string.Equals(c.Folder, folder, StringComparison.OrdinalIgnoreCase));
        }

        public LibraryItem Find(string category, string file)
        {
            LibraryCategory c = category == null ? null : FindCategory(category);
            if (c == null || file == null) return null;
            return c.Items.FirstOrDefault(i => string.Equals(i.File, file, StringComparison.OrdinalIgnoreCase));
        }

        public string TitleOf(string folder)
        {
            string title;
            return Titles.TryGetValue(folder, out title) ? title : folder;
        }

        // O som fica onde esta na biblioteca, e o icone da categoria vira uma copia so dele, que pode ser trocada depois.
        public SoundItem AddTo(Store store, LibraryItem item)
        {
            int before = store.Sounds().Count;
            SoundItem sound = store.AddSound(item.Path, null);
            if (store.Sounds().Count == before) return sound; // ja estava na lista: mantem o nome e o icone escolhidos
            LibraryCategory c = FindCategory(item.Category);
            if (c == null || c.Icon == null) return sound;
            try
            {
                string target = System.IO.Path.Combine(store.SoundIconDir, sound.id + ".png");
                Directory.CreateDirectory(store.SoundIconDir);
                File.Copy(c.Icon, target, true);
                store.SetSoundIcon(sound.id, target);
                return store.FindSound(sound.id);
            }
            catch (IOException e)
            {
                Log.Write("Icone da biblioteca: " + e.Message);
                return sound;
            }
        }
    }
}
