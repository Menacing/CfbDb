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
            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<CfbDbContext>().EnableSensitiveDataLogging(true).UseInMemoryDatabase(databaseName: "CfbDatabase").Options;

                CfbDbContext context = new CfbDbContext(optionsBuilder);

                SeedGames(context);

                Boolean seedElo = true;
                if (seedElo)
                {
                    SeedElo(context);
                }

                List<DateTime> gameDays = context.Games.Select(g => g.GameDate).Distinct().OrderBy(d => d).ToList();

                DateTime lastDayToRun = context.EloRecords.Max(e=>e.Date);

                gameDays = gameDays.Where(gd => gd >= lastDayToRun).ToList();

                context.EloRecords.RemoveRange(context.EloRecords.Where(el => el.Date == lastDayToRun));
                context.SaveChanges();

                foreach (DateTime gameDay in gameDays)
                {
                    List<Game> games = GetGamesPlayedOnDay(context, gameDay);

                    ConcurrentQueue<EloRecord> gameResults = new ConcurrentQueue<EloRecord>();

                    //foreach (Game g in games)
                    //{
                    //    ProcessGame(g, context, gameDay, gameResults);
                    //}

                    List<EloRecord> eloRecords = context.EloRecords.ToList();

                    Parallel.ForEach(games, g =>
                    {
                        EloRecord visitingTeamElo = GetLastEloRecordTillDate(eloRecords, g.VisitingTeamName, g.GameDate);
                        EloRecord homeTeamElo = GetLastEloRecordTillDate(eloRecords, g.HomeTeamName, g.GameDate);
                        ProcessGame(g,visitingTeamElo, homeTeamElo, gameDay, gameResults);
                    });

                    context.AddRange(gameResults);
                    context.SaveChanges();
                    Console.WriteLine(String.Format("Curent date {0} Execution Took {1}" , gameDay.ToString(),stopwatch.Elapsed.ToString()));
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<CfbDbContext>().EnableSensitiveDataLogging(true).UseInMemoryDatabase(databaseName: "CfbDatabase").Options;
                CfbDbContext context = new CfbDbContext(optionsBuilder);
                List<EloRecord> allElos = context.EloRecords.OrderBy(e=>e.Date).ToList();
                using (StreamWriter writer = System.IO.File.AppendText("AllEloValues.txt"))
                {
                    writer.WriteLine("Date,TeamName,EloScore");
                    foreach (var item in allElos)
                    {
                        string line = "";
                        line += item.Date.ToString("MM/dd/yyyy")+",";
                        line += item.TeamName + ",";
                        line += item.EloScore.ToString();

                        writer.WriteLine(line);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            Console.WriteLine("Execution Took " + stopwatch.Elapsed.ToString());
        }

        private static void ProcessGame(Game g, EloRecord visitingTeamElo, EloRecord homeTeamElo, DateTime gameDay, ConcurrentQueue<EloRecord> gameResults)
        {
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

        public static void SeedGames(CfbDbContext context)
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

        public static void SeedElo(CfbDbContext context)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = "CfbDb.AllEloValues.csv";
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    CsvReader csvReader = new CsvReader(reader);
                    var eloRecords = csvReader.GetRecords<EloRecord>().ToArray();
                    context.EloRecords.AddRange(eloRecords);
                }
            }

            context.SaveChanges();
        }

        public static EloRecord GetLastEloRecordTillDate(List<EloRecord> eloRecords, string teamName, DateTime? endDate =null)
        {
            var intermediary = eloRecords.Where(er => er.TeamName == teamName).OrderByDescending(er => er.Date);

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
