using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CfbDb.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TeamAWins()
        {
            int TeamAScore = 51;
            int TeamBScore = 0;

            var result = Program.CalculateActualScore(TeamAScore, TeamBScore);

            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public void TeamALoses()
        {
            int TeamAScore = 0;
            int TeamBScore = 51;

            var result = Program.CalculateActualScore(TeamAScore, TeamBScore);

            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void Tie()
        {
            int TeamAScore = 51;
            int TeamBScore = 51;

            var result = Program.CalculateActualScore(TeamAScore, TeamBScore);

            Assert.AreEqual(.5M, result);
        }

        [TestMethod]
        public void ExpectedScoreEven()
        {
            int teamAElo = 1500;
            int teamBElo = 1500;

            var result = Program.CalculateExpectedScore(teamAElo, teamBElo);

            Assert.AreEqual(.5M, result);
        }

        [TestMethod]
        public void ExpectedScoreAbout75()
        {
            int teamAElo = 1700;
            int teamBElo = 1500;

            var result = Program.CalculateExpectedScore(teamAElo, teamBElo);

            var expected = .7597469M;

            Assert.IsTrue(Math.Abs(expected - result) < .00005M);
        }

        [TestMethod]
        public void NewScorePlus10()
        {
            int teamAElo = 1500;

            var result = Program.CalculateNewRating(teamAElo,20,1,.5M);

            Assert.AreEqual(1510, result);
        }


    }
}
