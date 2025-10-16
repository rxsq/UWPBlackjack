using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace UWPBlackjack.Core
{
    public class HighScoreManager
    {
        private const string FileName = "highscores.txt";
        private static readonly char Delimiter = '|';
        private static List<(int Score, DateTime Date)> _scores = [];

        public static async Task LoadAsync()
        {
            var folder = ApplicationData.Current.LocalFolder;

            try
            {
                var file = await folder.GetFileAsync(FileName);
                var lines = await FileIO.ReadLinesAsync(file);

                _scores = lines
                    .Select(line => line.Split(Delimiter))
                    .Where(parts => parts.Length == 2)
                    .Select(parts => (int.Parse(parts[0]), DateTime.Parse(parts[1])))
                    .OrderByDescending(s => s.Item1)
                    .ToList();
            } 
            catch (FileNotFoundException)
            {
                _scores.Clear();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading high scores: {ex.Message}");
                _scores.Clear();
            }
        }

        public static async Task AddScoreAsync(int score)
        {
            _scores.Add((score, DateTime.Now));
            await SaveAsync();
        }
        private static async Task SaveAsync()
        {
            var folder = ApplicationData.Current.LocalFolder;
            var file = await folder.CreateFileAsync(FileName, CreationCollisionOption.OpenIfExists);
            var latest = _scores.Last();
            await FileIO.AppendTextAsync(file, $"{latest.Score}{Delimiter}{latest.Date:O}\n");
        }

        public static IEnumerable<(int Score, DateTime Date)> GetScores() => _scores;

        public static int GetHighestScore() => _scores.Any() ? _scores.Max(s => s.Score) : 500;

    }
}
