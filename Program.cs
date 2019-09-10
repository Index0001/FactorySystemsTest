using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Data.SQLite;


namespace FactorySystemsTest
{
    class Data { //A data type to hold the SQL data in a List
        public double x {get;set;}
        public double y {get;set;}
        public double height {get;set;}
    }
    class Program
    {
        static void Main(string[] args)
        {
            string DatabaseLoc;
            int limit;
            Console.WriteLine("Please type the location of the database");
            DatabaseLoc = Console.ReadLine();
            Console.WriteLine("Please type the filter size. (Typing nothing will default to 3, Typing 0 will include all datapoints)");
            string limitInput = Console.ReadLine();
            if (limitInput == "") {
                limit = 3;
            }
            else {
                try {
                    limit = Convert.ToInt32(limitInput);
                }
                catch {
                    Console.WriteLine("This is not a valid input. Defaulting to 3");
                    limit = 3;
                }
            }
            SQLiteConnection con;
            StringBuilder csvcontent = new StringBuilder();
            string csvpath = Directory.GetCurrentDirectory()+"/FactorySystemsTestSummary.csv";
            csvcontent.AppendLine("Factory Systems Test Summary");
            csvcontent.AppendLine("");
            
            try {
                con = new SQLiteConnection("Data Source="+DatabaseLoc);
                con.Open();
                string TestCount = "SELECT test_uid FROM Tests";

                List<int> Tests = new List<int>();
                List<Data> DataFull = new List<Data>();
                List<Data> DataLimited = new List<Data>();

                SQLiteCommand TestCountCommand = new SQLiteCommand(TestCount, con);
                SQLiteDataReader reader = TestCountCommand.ExecuteReader();
                while(reader.Read()) {
                    int test = Convert.ToInt32(reader["test_uid"]);
                    Tests.Add(test);
                }
                //Limit calculations based on the Test UID
                for (int i = 0; i < Tests.Count; i++) {
                    string DataDB = "SELECT x, y, height FROM Measurements WHERE test_uid = "+(i+1);
                    SQLiteCommand HeightCommand = new SQLiteCommand(DataDB, con);
                    SQLiteDataReader HeightReader = HeightCommand.ExecuteReader();
                    //Put SQL Data into a List to be used in calculations.
                    while(HeightReader.Read()){
                        Data datapoint = new Data();
                        datapoint.x = (double) HeightReader["x"];
                        datapoint.y = (double) HeightReader["y"];
                        datapoint.height = (double) HeightReader["height"];
                        DataFull.Add(datapoint); 
                    }

                    double OriginalMeanHeight = 0;
                    int Outliers = 0;
                    int NonOutliers = 0;
                    Data MinHeight = new Data();
                    Data MaxHeight = new Data();
                    List<double> XCount = new List<double>();
                    List<double> YCount = new List<double>();
                    double PSD = 0;
                    double MeanHeight = 0;
                    double RA = new double();
                    double RQ = new double();


                    //Calculate Original Mean using all data for a particular Test UID
                    for(int h = 0; h < DataFull.Count; h++) {
                        OriginalMeanHeight = OriginalMeanHeight + DataFull[h].height;
                    }
                    OriginalMeanHeight = OriginalMeanHeight / DataFull.Count;
                
               
                    //Calculate Population Standard Deviation
                    for (int h = 0; h < DataFull.Count; h++) {
                        PSD = PSD + Math.Pow(DataFull[h].height - OriginalMeanHeight,2);
                    }
                    PSD = Math.Sqrt(PSD/DataFull.Count);
                    //Applying Filter
                    double LxPSD = PSD * limit;

                    //Removing Outliers
                    for (int h = 0; h < DataFull.Count; h++) {
                        if (LxPSD == 0) {
                            DataLimited.Add(DataFull[h]);
                            NonOutliers++;
                        }
                        else if(!(DataFull[h].height < OriginalMeanHeight-LxPSD) && !(DataFull[h].height > OriginalMeanHeight + LxPSD) ) {
                            DataLimited.Add(DataFull[h]);
                            NonOutliers++;
                        }
                        else {
                            Outliers++;
                        }
                    }

                    //Calculating Min, Max, and new Mean for the filtered Data
                    for(int h = 0; h < DataLimited.Count; h++) {
                        if(h == 0) {
                            MinHeight = DataLimited[h];
                            MaxHeight = DataLimited[h];
                        }
                        else {
                            if(DataLimited[h].height < MinHeight.height) {
                                MinHeight = DataLimited[h];
                            }
                            if(DataLimited[h].height > MaxHeight.height) {
                                MaxHeight = DataLimited[h];
                            }
                            if(!XCount.Contains(DataLimited[h].x)) {
                                XCount.Add(DataLimited[h].x);
                            }
                            if(!YCount.Contains(DataLimited[h].y)) {
                                YCount.Add(DataLimited[h].y);
                            }
                        }
                        MeanHeight = MeanHeight + DataLimited[h].height;
                    }
                    MeanHeight = MeanHeight / DataLimited.Count;

                    //Calculate Mu
                    double Mu = MeanHeight; //After running a full calculation, it turns out Mu is just the mean.
               
                    //Calculate Average Roughness (RA)
                    for (int k = 0; k < XCount.Count; k++){
                        for(int l = 0; l < DataLimited.Count; l++) {
                            if (DataLimited[l].x == XCount[k]) {
                                RA = RA +  Math.Abs((DataLimited[l].height - Mu));
                            }
                        }
                    }
                    RA = RA / (XCount.Count * YCount.Count);
               
                    //Calculate Root mean square Roughness (RQ)
                    for (int k = 0; k < XCount.Count; k++){
                        for(int l = 0; l < DataLimited.Count; l++) {
                            if (DataLimited[l].x == XCount[k]) {
                                RQ = RQ + Math.Pow((DataLimited[l].height - Mu),2);
                            }
                        }
                    }
                    RQ = Math.Sqrt(RQ / (XCount.Count * YCount.Count));
 
                    //Write to CSV File
                    csvcontent.AppendLine("Summary for Test #"+(i+1));
                    if(DataLimited.Count == 0) {
                        csvcontent.AppendLine("No Available Data.");
                        csvcontent.AppendLine("");
                    }
                    else {
                        csvcontent.AppendLine("Minimum Height and Location: "+MinHeight.height+" mm at ("+MinHeight.x+", "+MinHeight.y+").");
                        csvcontent.AppendLine("Maximum Height and Location: "+MaxHeight.height+" mm at ("+MaxHeight.x+", "+MaxHeight.y+").");
                        csvcontent.AppendLine("Mean Height: "+MeanHeight+" mm.");
                        csvcontent.AppendLine("Height Range: "+MinHeight.height+" mm - "+MaxHeight.height+" mm.");
                        csvcontent.AppendLine("Average Roughness (RA): "+RA+" mm.");
                        csvcontent.AppendLine("Root mean square Roughness (RQ): "+RQ+" mm.");
                        csvcontent.AppendLine("Count of measurements inside the filter input: "+NonOutliers+".");
                        csvcontent.AppendLine("Count of measurements outside filter input: "+Outliers+".");
                        csvcontent.AppendLine("");
                    }
                    DataFull.Clear();
                    DataLimited.Clear();
                    Outliers = 0;
                    NonOutliers = 0;
               
                }
                con.Close();
                File.AppendAllText(csvpath, csvcontent.ToString());
        
            }
            catch {
                Console.WriteLine("No database file found in this location. Please include the name of the Database in the address.");
                
            }
        }
    }
}
