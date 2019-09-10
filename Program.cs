using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Data.SQLite;


namespace FactorySystemsTest
{
    class Test
    { //Holds Identifying information for Tests
        public int test_uid { get; set; }
        public DateTime sTime { get; set; }

        public string PlaneID { get; set; }

        public string Operator { get; set; }
    }
    class Data
    { //A data type to hold the SQL data in a List
        public double x { get; set; }
        public double y { get; set; }
        public double height { get; set; }
    }
    class Program
    {
        static void Main(string[] args)
        {
            string DatabaseLoc;
            int limit;
            Console.WriteLine("Please type the location of the Database");
            DatabaseLoc = Console.ReadLine();
            Console.WriteLine("Please type the filter size. (Typing nothing will default to 3, Typing 0 will include all datapoints)");
            string limitInput = Console.ReadLine();
            if (limitInput == "")
            {
                limit = 3;
            }
            else
            {
                try
                {
                    limit = Convert.ToInt32(limitInput);
                }
                catch
                {
                    Console.WriteLine("This is not a valid input. Defaulting to 3");
                    limit = 3;
                }
            }
            SQLiteConnection con;
            StringBuilder csvcontent = new StringBuilder();
            string csvpath = System.IO.Path.GetDirectoryName(DatabaseLoc) + "/FactorySystemsTestSummary.csv";
            csvcontent.AppendLine("Factory Systems Test Summary");
            csvcontent.AppendLine("");
            csvcontent.AppendLine("Test UID, Start Time, PlaneID, Operator, Minimum Height, X Coordinate for Minimum Height, Y Coordinate for Minimum Height, Maximum Height, X Coordinate for Maximum Height, Y Coordinate for Maximum Height, Mean Height, Height Range, Average Roughness (RA), Root Mean Square Roughness (RQ), Count of Measurements inside filter input, Count of measurements outside filter input");
            try
            {
                con = new SQLiteConnection("Data Source=" + DatabaseLoc);
                con.Open();
                string TestCount = "SELECT test_uid, sTime, PlaneID, Operator FROM Tests";

                List<Test> Tests = new List<Test>();
                List<Data> DataFull = new List<Data>();
                List<Data> DataLimited = new List<Data>();

                SQLiteCommand TestCountCommand = new SQLiteCommand(TestCount, con);
                SQLiteDataReader reader = TestCountCommand.ExecuteReader();
                while (reader.Read())
                {
                    Test newTest = new Test();
                    newTest.test_uid = Convert.ToInt32(reader["test_uid"]);
                    newTest.sTime = Convert.ToDateTime(reader["sTime"]);
                    newTest.PlaneID = Convert.ToString(reader["PlaneID"]);
                    newTest.Operator = Convert.ToString(reader["Operator"]);
                    Tests.Add(newTest);
                }

                //Limit calculations based on the Test UID
                for (int i = 0; i < Tests.Count; i++)
                {
                    string DataDB = "SELECT x, y, height FROM Measurements WHERE test_uid = " + (i + 1);
                    SQLiteCommand HeightCommand = new SQLiteCommand(DataDB, con);
                    SQLiteDataReader HeightReader = HeightCommand.ExecuteReader();
                    //Put SQL Data into a List to be used in calculations.
                    while (HeightReader.Read())
                    {
                        Data datapoint = new Data();
                        datapoint.x = (double)HeightReader["x"];
                        datapoint.y = (double)HeightReader["y"];
                        datapoint.height = (double)HeightReader["height"];
                        DataFull.Add(datapoint);
                    }

                    double OriginalMeanHeight = 0;
                    int Outliers = 0;
                    int NonOutliers = 0;
                    Data MinHeight = new Data();
                    Data MaxHeight = new Data();
                    List<double> XCount = new List<double>();
                    List<double> YCount = new List<double>();
                    double PopulationStandardDev = 0;
                    double MeanHeight = 0;
                    double HeightRange = 0;
                    double RA = 0;
                    double RQ = 0;


                    //Calculate Original Mean using all data for a particular Test UID
                    for (int h = 0; h < DataFull.Count; h++)
                    {
                        OriginalMeanHeight = OriginalMeanHeight + DataFull[h].height;
                    }
                    OriginalMeanHeight = OriginalMeanHeight / DataFull.Count;


                    //Calculate Population Standard Deviation
                    for (int h = 0; h < DataFull.Count; h++)
                    {
                        PopulationStandardDev = PopulationStandardDev + Math.Pow(DataFull[h].height - OriginalMeanHeight, 2);
                    }
                    PopulationStandardDev = Math.Sqrt(PopulationStandardDev / DataFull.Count);
                    //Applying Filter
                    double LimitedPopulationStandardDev = PopulationStandardDev * limit;

                    //Removing Outliers
                    for (int h = 0; h < DataFull.Count; h++)
                    {
                        if (LimitedPopulationStandardDev == 0)
                        {
                            DataLimited.Add(DataFull[h]);
                            NonOutliers++;
                        }
                        else if (!(DataFull[h].height < OriginalMeanHeight - LimitedPopulationStandardDev) && !(DataFull[h].height > OriginalMeanHeight + LimitedPopulationStandardDev))
                        {
                            DataLimited.Add(DataFull[h]);
                            NonOutliers++;
                        }
                        else
                        {
                            Outliers++;
                        }
                    }

                    //Calculating Min, Max, Range, and new Mean for the filtered Data
                    for (int h = 0; h < DataLimited.Count; h++)
                    {
                        if (h == 0)
                        {
                            MinHeight = DataLimited[h];
                            MaxHeight = DataLimited[h];
                        }
                        else
                        {
                            if (DataLimited[h].height < MinHeight.height)
                            {
                                MinHeight = DataLimited[h];
                            }
                            if (DataLimited[h].height > MaxHeight.height)
                            {
                                MaxHeight = DataLimited[h];
                            }
                            if (!XCount.Contains(DataLimited[h].x))
                            {
                                XCount.Add(DataLimited[h].x);
                            }
                            if (!YCount.Contains(DataLimited[h].y))
                            {
                                YCount.Add(DataLimited[h].y);
                            }
                        }
                        MeanHeight = MeanHeight + DataLimited[h].height;
                    }
                    MeanHeight = MeanHeight / DataLimited.Count;
                    HeightRange = MaxHeight.height - MinHeight.height;

                    //Calculate Mu
                    double Mu = MeanHeight; //After running a full calculation, it turns out Mu is just the mean.

                    //Calculate Average Roughness (RA)
                    for (int k = 0; k < XCount.Count; k++)
                    {                     //Goes through each instance of X
                        for (int l = 0; l < DataLimited.Count; l++)
                        {            //Checks for each Y of a given X
                            if (DataLimited[l].x == XCount[k])
                            {
                                RA = RA + Math.Abs((DataLimited[l].height - Mu));
                            }
                        }
                    }
                    RA = RA / (DataLimited.Count);

                    //Calculate Root mean square Roughness (RQ)
                    for (int k = 0; k < XCount.Count; k++)
                    {
                        for (int l = 0; l < DataLimited.Count; l++)
                        {
                            if (DataLimited[l].x == XCount[k])
                            {
                                RQ = RQ + Math.Pow((DataLimited[l].height - Mu), 2);
                            }
                        }
                    }
                    RQ = Math.Sqrt(RQ / (DataLimited.Count));

                    //Write to CSV File

                    if (DataLimited.Count == 0)
                    {
                        csvcontent.AppendLine(Tests[i].test_uid + "," + Tests[i].sTime + "," + Tests[i].PlaneID + "," + Tests[i].Operator + ", No Data, No Data, No Data, No Data, No Data, No Data, No Data, No Data, No Data, No Data, No Data, No Data");
                    }
                    else
                    {
                        csvcontent.AppendLine(Tests[i].test_uid + "," + Tests[i].sTime + "," + Tests[i].PlaneID + "," + Tests[i].Operator + "," + MinHeight.height + " mm," + MinHeight.x + "," + MinHeight.y + "," + MaxHeight.height + " mm," + MaxHeight.x + "," + MaxHeight.y + "," + MeanHeight + " mm," + HeightRange + " mm," + RA + " mm," + RQ + " mm," + NonOutliers + "," + Outliers);
                    }

                    DataFull.Clear();
                    DataLimited.Clear();

                }
                con.Close();
                File.WriteAllText(csvpath, csvcontent.ToString());
                Console.WriteLine("The Summary has been saved to " + System.IO.Path.GetDirectoryName(DatabaseLoc));
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();

            }
            catch
            {
                Console.WriteLine("No database file found in this location. Please include the name of the Database in the address.");
                Console.WriteLine("Press Any Key to exit");
                Console.ReadKey();

            }
        }
    }
}
