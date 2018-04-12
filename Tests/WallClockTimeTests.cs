using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using WritingTests.WallClockTime;

namespace Tests
{
    [TestClass]
    public class WallClockTimeTests
    {
        [DataTestMethod]
        [DataRow(1900, 1, 27)]
        [DataRow(1972, 1, 27)]
        [DataRow(1972, 7, 26)]
        [DataRow(1973, 1, 25)]
        [DataRow(1974, 1, 24)]
        [DataRow(1975, 1, 23)]
        [DataRow(1976, 1, 22)]
        [DataRow(1977, 1, 21)]
        [DataRow(1978, 1, 20)]
        [DataRow(1979, 1, 19)]
        [DataRow(1980, 1, 18)]
        [DataRow(1981, 7, 17)]
        [DataRow(1982, 7, 16)]
        [DataRow(1983, 7, 15)]
        [DataRow(1985, 7, 14)]
        [DataRow(1988, 1, 13)]
        [DataRow(1990, 1, 12)]
        [DataRow(1991, 1, 11)]
        [DataRow(1992, 7, 10)]
        [DataRow(1993, 7, 9)]
        [DataRow(1994, 7, 8)]
        [DataRow(1996, 1, 7)]
        [DataRow(1997, 7, 6)]
        [DataRow(1999, 1, 5)]
        [DataRow(2006, 1, 4)]
        [DataRow(2009, 1, 3)]
        [DataRow(2012, 7, 2)]
        [DataRow(2015, 7, 1)]
        [DataRow(2017, 1, 0)]
        public void Test_WallClockTime_FromApproximateDateTimeOffset_And_FromPseudoMilliseconds_Difference_Between_Dates_Results_In_Expected_Number_Of_Leap_Seconds(
            int startYear, int startMonth, int expectedSeconds)
        {
            //Arrange
            var dtoStart = new DateTimeOffset(startYear, startMonth, 1, 0, 0, 0, TimeSpan.Zero);
            var dtoNow = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);

            var wctStart = WallClockTime.FromApproximateDateTimeOffset(dtoStart);
            var wctNow = WallClockTime.FromApproximateDateTimeOffset(dtoNow);
            var wctStart2 = WallClockTime.FromPseudoMilliseconds(dtoStart.ToUnixTimeMilliseconds());
            var wctNow2 = WallClockTime.FromPseudoMilliseconds(dtoNow.ToUnixTimeMilliseconds());

            //Act
            var dtoDiff = dtoNow - dtoStart;
            var wctDiff = wctNow - wctStart;
            var wctDiff2 = wctNow2 - wctStart2;
            var leapSeconds = (wctDiff - dtoDiff).TotalSeconds;
            var leapSeconds2 = (wctDiff2 - dtoDiff).TotalSeconds;

            //Assert
            Assert.AreEqual(expectedSeconds, leapSeconds);
            Assert.AreEqual(expectedSeconds, leapSeconds2);
        }

        [DataTestMethod]
        [DataRow(2016, 12, 31, 50, 10, 0)]
        [DataRow(2016, 12, 31, 50, 11, 1)]
        [DataRow(2015, 6, 30, 59, 1, 0)]
        [DataRow(2015, 6, 30, 59, 2, 1)]
        [DataRow(2013, 12, 31, 50, 10, 0)]
        [DataRow(2013, 12, 31, 50, 11, 0)]
        [DataRow(2013, 6, 30, 59, 1, 0)]
        [DataRow(2013, 6, 30, 59, 2, 0)]
        public void Test_WallClockTime_AddTrueMilliseconds_And_SubtractTrueMilliseconds_Results_In_Expected_Date(
            int year, int month, int day, int second, int secondsToAddAndSubstract, int expectedLeapSeconds)
        {
            //Arrange
            var msToAddAndSubstract = secondsToAddAndSubstract * 1000;
            var dto = new DateTimeOffset(year, month, day, 23, 59, second, TimeSpan.Zero);
            var wct = WallClockTime.FromApproximateDateTimeOffset(dto);

            //Act
            var addResult = wct.AddTrueMilliseconds(msToAddAndSubstract);
            var substractResult = addResult.SubtractTrueMilliseconds(msToAddAndSubstract);

            //Assert
            Assert.IsTrue(substractResult.Equals(wct));
            Assert.IsTrue(substractResult >= wct);
            Assert.IsTrue(substractResult == wct);
            Assert.IsTrue(substractResult <= addResult);
            Assert.IsTrue(substractResult < addResult);
            Assert.IsTrue(substractResult != addResult);
            Assert.IsTrue(addResult > wct);
            Assert.AreEqual(substractResult.ToApproximateDateTimeOffset(), dto);
            Assert.AreEqual(addResult.ToApproximateDateTimeOffset(), dto.AddMilliseconds(msToAddAndSubstract - expectedLeapSeconds*1000));
        }

        [TestMethod]
        [DataRow(2017, 1, 1, 1, 2, 2, 1)]
        [DataRow(2016, 12, 31, 21, -2, 2, 1)]
        public void Test_WallClockTime_AddTrueMilliseconds_And_SubtractTrueMilliseconds_With_Different_Offsets_Results_In_Expected_Date(
            int year, int month, int day,int hour, int offsetHour, int secondsToAddAndSubstract, int expectedLeapSeconds)
        {
            //Arrange
            var msToAddAndSubstract = secondsToAddAndSubstract * 1000;
            var dto = new DateTimeOffset(year, month, day, hour, 59, 59, new TimeSpan(offsetHour, 0,0));
            var wct = WallClockTime.FromApproximateDateTimeOffset(dto);

            //Act
            var addResult = wct.AddTrueMilliseconds(msToAddAndSubstract);
            var substractResult = addResult.SubtractTrueMilliseconds(msToAddAndSubstract);

            //Assert
            Assert.IsTrue(substractResult.Equals(wct));
            Assert.IsTrue(substractResult >= wct);
            Assert.IsTrue(substractResult == wct);
            Assert.IsTrue(substractResult <= addResult);
            Assert.IsTrue(substractResult < addResult);
            Assert.IsTrue(substractResult != addResult);
            Assert.IsTrue(addResult > wct);
            Assert.AreEqual(substractResult.ToApproximateDateTimeOffset(), dto);
            Assert.AreEqual(addResult.ToApproximateDateTimeOffset(), dto.AddMilliseconds(msToAddAndSubstract - expectedLeapSeconds * 1000));
        }

        [DataTestMethod]
        [DataRow(2017, 1, 1, 10, -10, 0)]
        [DataRow(2017, 1, 1, 10, -11, 1)]
        [DataRow(2015, 7, 1, 1, -1, 0)]
        [DataRow(2015, 7, 1, 1, -2, 1)]
        [DataRow(2013, 1, 1, 10, -10, 0)]
        [DataRow(2013, 1, 1, 10, -11, 0)]
        [DataRow(2013, 7, 1, 1, -1, 0)]
        [DataRow(2013, 7, 1, 1, -2, 0)]
        public void Test_WallClockTime_AddTrueMilliseconds_And_SubtractTrueMilliseconds_Works_With_Negative_Numbers(
            int year, int month, int day, int second, int negativeOrZeroSeconds, int expectedLeapSeconds)
        {
            //Arrange
            var negativeOrZeroMs = negativeOrZeroSeconds * 1000;
            var dto = new DateTimeOffset(year, month, day, 0, 0, second, TimeSpan.Zero);
            var wct = WallClockTime.FromApproximateDateTimeOffset(dto);

            //Act
            var addResult = wct.AddTrueMilliseconds(negativeOrZeroMs);
            var substractResult = addResult.SubtractTrueMilliseconds(negativeOrZeroMs);

            //Assert
            Assert.IsTrue(substractResult.Equals(wct));
            Assert.IsTrue(substractResult <= wct);
            Assert.IsTrue(substractResult == wct);
            Assert.IsTrue(substractResult >= addResult);
            Assert.IsTrue(substractResult > addResult);
            Assert.IsTrue(substractResult != addResult);
            Assert.IsTrue(addResult < wct);
            Assert.AreEqual(substractResult.ToApproximateDateTimeOffset(), dto);
            Assert.AreEqual(addResult.ToApproximateDateTimeOffset(), dto.AddMilliseconds(negativeOrZeroMs + expectedLeapSeconds * 1000));
        }

        [DataTestMethod]
        [DataRow(2017, 1, 1, 10)]
        [DataRow(2017, 1, 1, 10)]
        [DataRow(2015, 7, 1, 1)]
        [DataRow(2015, 7, 1, 1)]
        [DataRow(2013, 1, 1, 10)]
        [DataRow(2013, 1, 1, 10)]
        [DataRow(2013, 7, 1, 1)]
        [DataRow(2013, 7, 1, 1)]
        public void Test_WallClockTime_AddTrueMilliseconds_And_SubtractTrueMilliseconds_Works_With_Zero(
            int year, int month, int day, int second)
        {
            //Arrange
            var dto = new DateTimeOffset(year, month, day, 0, 0, second, TimeSpan.Zero);
            var wct = WallClockTime.FromApproximateDateTimeOffset(dto);

            //Act
            var addResult = wct.AddTrueMilliseconds(0);
            var substractResult = addResult.SubtractTrueMilliseconds(0);

            //Assert
            Assert.IsTrue(substractResult.Equals(wct));
            Assert.IsTrue(substractResult.Equals(addResult));
            Assert.IsTrue(wct.Equals(addResult));
            Assert.AreEqual(substractResult.ToApproximateDateTimeOffset(), dto);
            Assert.AreEqual(addResult.ToApproximateDateTimeOffset(), dto);
        }
    }
}
