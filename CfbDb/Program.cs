using CsvHelper;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace CfbDb
{
    class Program
    {
        private const int defaultScore = 1500;
        private const int kValue = 20;

        static void Main(string[] args)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var optionsBuilder = new DbContextOptionsBuilder<CfbDbContext>().UseInMemoryDatabase(databaseName: "CfbDatabase").Options;

            CfbDbContext context = new CfbDbContext(optionsBuilder);

            Seed(context);

            List<DateTime> gameDays = context.Games.Select(g => g.GameDate).Distinct().OrderBy(d => d).ToList();

            foreach (DateTime gameDay in gameDays)
            {
                List<Game> games = GetGamesPlayedOnDay(context, gameDay);

                ConcurrentQueue<EloRecord> gameResults = new ConcurrentQueue<EloRecord>();


                foreach (Game g in games)
                {
                    ProcessGame(g, context, gameDay, gameResults);
                }

                //Parallel.ForEach(games, g =>
                //{
                //    CfbDbContext processContext = new CfbDbContext(optionsBuilder);

                //    ProcessGame(g, processContext, gameDay, gameResults);
                //});

                context.AddRange(gameResults);
                context.SaveChanges();
            }


            List<EloRecord> allElos = context.EloRecords.OrderBy(e=>e.Date).ToList();
            using (StreamWriter writer = System.IO.File.AppendText("AllEloValues.txt"))
            {
                foreach (var item in allElos)
                {

                    string line = "";
                    line += item.Date.ToString("MM/dd/yyyy")+",";
                    line += item.TeamName + ",";
                    line += item.EloScore.ToString();

                    writer.WriteLine(line);
                }
            }

            Console.WriteLine("Execution Took " + stopwatch.Elapsed.ToString());
        }

        private static void ProcessGame(Game g, CfbDbContext context, DateTime gameDay, ConcurrentQueue<EloRecord> gameResults)
        {
            EloRecord visitingTeamElo = GetLastEloRecordTillDate(context, g.VisitingTeamName, gameDay);
            EloRecord homeTeamElo = GetLastEloRecordTillDate(context, g.HomeTeamName, gameDay);

            Decimal expectedVisitingTeamScore = CalculateExpectedScore(visitingTeamElo.EloScore, homeTeamElo.EloScore);
            Decimal expectedHomeTeamScore = 1 - expectedVisitingTeamScore;

            Decimal actualVisitingTeamScore = CalculateActualScore(g.VisitingTeamScore, g.HomeTeamScore);
            Decimal actualHomeTeamScore = CalculateActualScore(g.HomeTeamScore, g.VisitingTeamScore);

            int newVisitinTeamEloScore = CalculateNewRating(visitingTeamElo.EloScore, kValue, actualVisitingTeamScore, expectedVisitingTeamScore);
            int newHomeTeamEloScore = CalculateNewRating(homeTeamElo.EloScore, kValue, actualHomeTeamScore, expectedHomeTeamScore);

            EloRecord newVisitingTeamElo = new EloRecord() { Date = g.GameDate, EloScore = newVisitinTeamEloScore, TeamName = g.VisitingTeamName };
            EloRecord newHomeTeamElo = new EloRecord() { Date = g.GameDate, EloScore = newHomeTeamEloScore, TeamName = g.HomeTeamName };

            gameResults.Enqueue(newVisitingTeamElo);
            gameResults.Enqueue(newHomeTeamElo);
        }

        private static int CalculateNewRating(int eloScore, int kValue, decimal actualVisitingTeamScore, decimal expectedVisitingTeamScore)
        {
            return decimal.ToInt32(Math.Round(eloScore + kValue * (actualVisitingTeamScore - expectedVisitingTeamScore)));
        }

        public static decimal CalculateActualScore(int teamAScore, int teamBScore)
        {
            if (teamAScore > teamBScore)
            {
                return 1;
            }
            else if (teamAScore == teamBScore)
            {
                return .5M;
            }
            else
            {
                return 0;
            }
        }

        public static decimal CalculateExpectedScore(int teamARating, int teamBRating)
        {
            return (Decimal)(1 / (1 + Math.Pow(10, ((teamARating - teamBRating) / 400))));
        }

        public static List<Game> GetGamesPlayedOnDay(CfbDbContext context, DateTime gameDay)
        {
            return context.Games.Where(g => g.GameDate == gameDay).ToList();
        }

        public static void Seed(CfbDbContext context)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = "CfbDb.CFB Dataset - AllHistoricalGames.csv";
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    CsvReader csvReader = new CsvReader(reader);
                    var games = csvReader.GetRecords<Game>().ToArray();
                    context.Games.AddRange(games);
                }
            }

            context.SaveChanges();
        }

        public static EloRecord GetLastEloRecordTillDate(CfbDbContext context, string teamName, DateTime? endDate =null)
        {
            IOrderedQueryable<EloRecord> intermediary = context.EloRecords.Where(er => er.TeamName == teamName).OrderByDescending(er => er.Date);

            List<EloRecord> result = new List<EloRecord>();
            if (endDate.HasValue)
            {
                result = intermediary.Where(er => er.Date <= endDate.Value).ToList();
            }
            else
            {
                result = intermediary.ToList();
            }

            if (result.Count >= 1)
            {
                return result.First();
            }
            else
            {
                return new EloRecord() { Date = endDate ?? DateTime.Now, TeamName = teamName, EloScore = defaultScore };
            }
        }
    }

}
