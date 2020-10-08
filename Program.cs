// Pearce Consulting Group for HHSC
// Richard Pearce
// Transition Programs and Databases on iSeries/PowerI/As400 to Windows/C# environment
// First Hawaiian Bank LockBox Payments Processing: 
//  Splitting by Facility,
//  Payment Lists/Reports/Excel sheet, 
//  Multi-destination distribution/routing, 
//  Automated Payment Transaction Posting - this will no longer be done. Payment Posting was on AS400 Series application. 
// 

using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Diagnostics;

namespace FHB_Split_and_Distribute_HHSC
{
    public class FHB_BoxAccts
    {
        public string BoxAndAcct { get; set; }
        public string Facility { get; set; }
        public string Box { get; set; }
        public string HspAcct { get; set; }
        public int Hspnbr3 { get; set; }
        public int ID { get; set; }


    }
    public class FHB_Payment
    {
        public int FhChkd { get; set; }
        public decimal FhAmtd { get; set; }
        public string FhAc16 { get; set; }
        public string FhBoxn { get; set; }
        public string FhAcct { get; set; }
        public string FhAdmt { get; set; }
        public string FhStmt { get; set; }
        public decimal FhPmt { get; set; }
        public string FhDate { get; set; }
        public string F1BoxAcct { get; set; }
        public string F1FacName { get; set; }
        public DateTime F1Admt { get; set; }
        public DateTime F1Stmt { get; set; }
        public DateTime F1Date { get; set; }
        public string F1Full { get; set; }
    }
    public class MSGs
    {
        public DateTime MsgDT { get; set; }
        public string Msg { get; set; }
    }
    public class FHBFileProcessor
    {

        public static void Main(string[] args)
        {
            // Process File(s) in Path
            foreach (string path in args)
            {
                if (File.Exists(path))
                {
                    // This path is a file
                    Snap($"*Processing File...'{path}' \n ================================================================== ", 1, 0);
                    ProcessFile(path);
                }
                else if (Directory.Exists(path))
                {
                    // This path is a directory
                    Snap($"*Processing Path...'{path}'\n ================================================================== ", 1, 0);
                    ProcessDirectory(path);
                }
                else
                {
                    Snap($"arg '{path}' is not a valid file or directory.", 0, 0);
                }
            }
        }


        // Process all files in the directory passed in, //*removed(not doing subdirectories): and recurse on any subdirectories*//
        public static void ProcessDirectory(string targetDirectory)
        {

            // Process the list of files found in the directory - *FHB Specific.
            // Select only inbound First Hawaiian Bank original naming convention (expecting only 1 per day). 
            string[] fileEntries = Directory.GetFiles(targetDirectory, "lbx-hihealth-out1-d-20??????.txt");

            // Get number of valid files found to process
            int n = fileEntries.Length;

            Snap($"There {(n != 1 ? "are " : "is ")}  {n} file{(n != 1 ? "s" : "")} found that match 'lbx-hihealth-out1-d-20??????.txt'.", 0, 0);


            foreach (string fileName in fileEntries)
                ProcessFile(fileName);

            // Remove subdirectory processing, Hold for selective historical rebuilding
            ///    Recurse into subdirectories of this directory.
            ///     string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
            ///     foreach (string subdirectory in subdirectoryEntries)
            ///     ProcessDirectory(subdirectory);
        }


        public static void ProcessFile(string path)
        {
            // Collect & prep misc info and records for payment file
            int[] accountCountLines = new int[10];
            int lines = File.ReadAllLines(path).Length;
            string[] records = File.ReadAllLines(path);
            int i = 0; int cnt = 0; string newName = ""; string newXlsName = " "; // pre-loop
            Snap($"........Processing....... {Path.GetFileName(path)}", 1, 0);
            List<FHB_BoxAccts> accts = AcctList();
            List<FHB_Payment> rcds = PmtList(records, accts);



            foreach (var FHB_BoxAccts in accts)
            {
                accountCountLines[i] = File.ReadAllLines(path).Count(l => l.Contains(accts[i].BoxAndAcct));

                Snap($"Index {i} for {accts[i].Facility} - {accts[i].BoxAndAcct} has a count of:  {accountCountLines[i]} lines", 0, 0);
                cnt = accountCountLines[i];

                // If records found for Hospitals LockBox ID and Account number, create split file of transactions.
                if (cnt > 0)
                {
                    // Split and Write separate distribution file for Facility, in original format

                    newName = path.Replace(".txt", "_S_FHB_") + accts[i].Facility + ".txt";
                    if (File.Exists(newName))
                    {
                        File.Delete(newName);
                    }

                    using (StreamWriter sw = File.AppendText(newName))
                    {
                        foreach (string line in File.ReadLines(path))
                        {
                            if (line.Contains(accts[i].BoxAndAcct))
                            {
                                sw.WriteLine(line);
                            }
                        }
                        sw.Close();
                        DisplayResult(cnt, accts[i].Facility, accts[i].BoxAndAcct, path, newName, (Path.GetFileName(path)));

                    }

                    // XLS REPORT for Facility ....
                    newXlsName = (Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path))) +
                                  "_FHB_Payments_" + accts[i].Facility + ".xls";
                    if (File.Exists(newXlsName))
                    {
                        File.Delete(newXlsName);
                    }

                    decimal totalPmts = 0;

                    using (StreamWriter xl = File.AppendText(newXlsName))
                    {
                        // TITLES and Column Header
                        xl.WriteLine("\tFIRST HAWAIIAN BANK LOCK BOX PAYMENTS for " + accts[i].Facility);
                        xl.WriteLine("\tFile name " + Path.GetFileName(path) + " processed on " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString());
                        xl.WriteLine("\t" + accountCountLines[i] + " payment records written to: " + newName + "\n");
                        xl.WriteLine("\tHospital\tPayment Date\tAccount#\tPayment Amt\tAmount Due\tStatement Date\t" + "Admit/Other Date\tLockBox#");

                        foreach (FHB_Payment rcd in rcds.Where(k => k.F1BoxAcct == accts[i].BoxAndAcct))
                        {
                            // Payment detail lines
                            xl.WriteLine("\t" +
                               rcd.F1FacName + "\t" +
                               rcd.F1Date.ToShortDateString() + "\t'" +
                               rcd.FhAc16.TrimStart(trimChars: new Char[] { '0' }) + "\t" +
                               String.Format("{0:C2}", rcd.FhPmt) + "\t" +
                               String.Format("{0:C2}", rcd.FhAmtd) + "\t" +
                               rcd.F1Stmt.ToShortDateString() + "\t" +
                               rcd.F1Admt.ToShortDateString() + "\t" +
                               rcd.FhBoxn);
                            totalPmts += rcd.FhPmt;
                        }
                        // Write total payments line(s)
                        xl.WriteLine("\t\t\t" + " Total =" + "\t" + String.Format("{0:C2}", totalPmts));
                        xl.Close();
                        DisplayResult(cnt, accts[i].Facility, accts[i].BoxAndAcct, path, newName, (Path.GetFileName(path)));

                    }
                }

                i++;
            }
            int totalBoxCounts = accountCountLines.Sum();
            Snap($"Processed file '{Path.GetFileName(path)}' with {lines} total lines, {totalBoxCounts} written to separate files, at {DateTime.Now}.", 1, 0);
            Snap("..........................................................................................................", 0, 1);

            WriteFullList(path, lines, rcds, totalBoxCounts);
            AppendHistory(path, rcds);
        }



        private static List<FHB_BoxAccts> AcctList()
        {
            // Load First Hawaiian LockBox IDs and name strings for Hospitals
            string[] FhbAcctsIn = File.ReadAllLines(@"FHB_Facility_BoxAccounts.csv");
            string[] ActivityIn = File.ReadAllLines(@"FHB_FileResults.csv"); // future

            // Load to class list
            IEnumerable<FHB_BoxAccts> FhbAccts =
            from fhbAcctLine in FhbAcctsIn.Skip(1)
            let splitName = fhbAcctLine.Split(',')
            select new FHB_BoxAccts()
            {
                BoxAndAcct = Convert.ToString(splitName[0]),
                Facility = splitName[1],
                Box = Convert.ToString(splitName[2]),
                HspAcct = Convert.ToString(splitName[3]),
                Hspnbr3 = Convert.ToInt32(splitName[4]),
                ID = Convert.ToInt32(splitName[5])
            };

            List<FHB_BoxAccts> acct = FhbAccts.ToList();
            return acct;
        }

        public static List<FHB_Payment> PmtList(string[] rcds, List<FHB_BoxAccts> AcctList)
        {
            var acctDictionary = AcctList.ToDictionary(key => key.BoxAndAcct, value => value.Facility);

            // Load First Hawaiian Payment records to class
            IEnumerable<FHB_Payment> FhbPmts =
            from PmtLine in rcds
            let rcd = PmtLine
            select new FHB_Payment()
            {
                FhChkd = Convert.ToInt32(rcd.Substring(0, 1)),
                FhAmtd = (Convert.ToDecimal(rcd.Substring(1, 10)) / 100),
                FhAc16 = rcd.Substring(11, 16),
                FhBoxn = rcd.Substring(27, 5),
                FhAcct = rcd.Substring(32, 8),
                FhAdmt = rcd.Substring(40, 8),
                FhStmt = rcd.Substring(48, 8),
                FhPmt = (Convert.ToDecimal(rcd.Substring(56, 14)) / 100),
                FhDate = rcd.Substring(70, 8),
                F1BoxAcct = rcd.Substring(27, 13),
                F1FacName = acctDictionary[rcd.Substring(27, 13)],
                F1Date = ToDateTime(rcd.Substring(70, 8), "MMddyyyy", DateTimeKind.Local),
                F1Full = rcd.Substring(0, 77),

                // Admit & Statement Dates: Hospitals use different formats cymd vs mdcy mixed in records. 
                // Determine type and convert string to date
                F1Admt = (Convert.ToInt32(rcd.Substring(40, 8)) > 13000000) ? (ToDateTime(rcd.Substring(40, 8), "yyyyMMdd", DateTimeKind.Local)) : (ToDateTime(rcd.Substring(40, 8), "MMddyyyy", DateTimeKind.Local)),
                F1Stmt = (Convert.ToInt32(rcd.Substring(48, 8)) > 13000000) ? (ToDateTime(rcd.Substring(48, 8), "yyyyMMdd", DateTimeKind.Local)) : (ToDateTime(rcd.Substring(48, 8), "MMddyyyy", DateTimeKind.Local))
            };

            List<FHB_Payment> pmts = FhbPmts.ToList();
            return pmts;
        }


        private static void DisplayResult(int hspCount, string facility, string boxAndAcct, string origPath, string newName, string fileName)
        {
            Snap(
                $"New file '{newName}' " +
                $"\n     written from inbound file {fileName} " +
                $"\n     with {hspCount} records, for Facility {facility}.", 1, 1);
        }



        /// <summary>
        /// Converts a string to a dateTime with the given format and kind.
        /// </summary>
        /// <param name="dateTimeString">The date time string.</param>
        /// <param name="dateTimeFormat">The date time format.</param>
        /// <param name="dateTimeKind">Kind of the date time.</param>
        /// <returns></returns>
        public static DateTime ToDateTime(string dateTimeString, string dateTimeFormat, DateTimeKind dateTimeKind)
        {
            if (string.IsNullOrEmpty(dateTimeString))
            {
                return DateTime.MinValue;
            }

            DateTime dateTime;
            try
            {
                dateTime = DateTime.SpecifyKind(DateTime.ParseExact(dateTimeString, dateTimeFormat, CultureInfo.InvariantCulture), dateTimeKind);
            }
            catch (FormatException)
            {
                dateTime = DateTime.MinValue;
            }

            return dateTime;

        }

        private static void AppendHistory(string path, List<FHB_Payment> rcds)
        {
            string newXlsName = Path.Combine(Path.GetDirectoryName(path), "_FHB_All_Payment_History.txt");
            using StreamWriter xh = File.AppendText(newXlsName);
            foreach (FHB_Payment rcd in rcds)
            {
                // Payment detail lines
                xh.WriteLine("\t" +
                   rcd.F1FacName + "\t" +
                   rcd.F1Date.ToShortDateString() + "\t'" +
                   rcd.FhAc16.TrimStart(trimChars: new Char[] { '0' }) + "\t" +
                   String.Format("{0:C2}", rcd.FhPmt) + "\t" +
                   String.Format("{0:C2}", rcd.FhAmtd) + "\t" +
                   rcd.F1Stmt.ToShortDateString() + "\t" +
                   rcd.F1Admt.ToShortDateString() + "\t" +
                   rcd.FhBoxn + "\t" +
                   Path.GetFileName(path) + "\t" +
                   DateTime.Now.ToString());

            }
            xh.Close();

            string newXlsName2 = (Path.Combine(Path.GetDirectoryName(path), Path.GetFileName(path).Substring(0, 20) + "_2FHB_All_Payment_History.txt"));
            using StreamWriter xh2 = File.AppendText(newXlsName2);

            //foreach (FHB_Payment rcd in rcds)
            // {
            // Dump payment rcd to xls
            string tabbedData = GetConcatedString(rcds);  // ,"\t"); 
            xh2.WriteLine("\t   {0} \n", tabbedData);

            //}
            xh2.Close();

            string GetConcatedString<T>(List<T> listItems)  //, char delimiter)
            {
                char[] delimiter = "\t".ToCharArray();
                var fields = Type.GetType(listItems.GetType().GetGenericArguments()[0].FullName).GetProperties();
                return string.Join("", listItems.Select(x =>
                                 string.Join(delimiter[0], fields.Select(f => f.GetValue(x))).TrimEnd(delimiter[0])));
            }
        }


        private static void WriteFullList(string path, int lines, List<FHB_Payment> rcds, int totalBoxCounts)
        {
            decimal totalPmts = 0;
            string newXlsName = (Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path))) +
                                  "_FHB_All_Payments.xls";
            using StreamWriter xa = File.AppendText(newXlsName);
            // TITLES and Column Header
            xa.WriteLine("\tFIRST HAWAIIAN BANK LOCK BOX PAYMENTS, Full List of ALL Payments");
            xa.WriteLine("\tFile name " + Path.GetFileName(path) + " processed on " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString());
            xa.WriteLine("\t" + lines + " payment records received and " + totalBoxCounts + " written." + "\n");
            xa.WriteLine("\tHospital\tPayment Date\tAccount#\tPayment Amt\tAmount Due\tStatement Date\t" + "Admit/Other Date\tLockBox#");

            foreach (FHB_Payment rcd in rcds)
            {
                // Payment detail lines
                xa.WriteLine("\t" +
                   rcd.F1FacName + "\t" +
                   rcd.F1Date.ToShortDateString() + "\t'" +
                   rcd.FhAc16.TrimStart(trimChars: new Char[] { '0' }) + "\t" +
                   String.Format("{0:C2}", rcd.FhPmt) + "\t" +
                   String.Format("{0:C2}", rcd.FhAmtd) + "\t" +
                   rcd.F1Stmt.ToShortDateString() + "\t" +
                   rcd.F1Admt.ToShortDateString() + "\t" +
                   rcd.FhBoxn);
                totalPmts += rcd.FhPmt;
            }
            // Write total payments line(s)
            xa.WriteLine("\t\t\t" + " Total =" + "\t" + String.Format("{0:C2}", totalPmts));
            xa.Close();

        }

        public static void Snap(string msg, int b4, int ba)  //Snapshot message to display and save log: Text, blank lines before, after.
        {
            string msg1 = new String('\n', b4) + $" {DateTime.Now}:\t" + msg + string.Concat(Enumerable.Repeat("\n", ba));

            Console.WriteLine(msg1);
            Debug.WriteLine(msg1);
            //const string LogName = "C:\\Users\\Richa\\OneDrive\\1R\\Hawaii\\TestingData\\FHB\\LOG_Activity.txt";
            const string LogName = "LOG_Activity.txt";
            using (StreamWriter lg = File.AppendText(LogName))
            { lg.WriteLine($"{msg1} "); }
        }
    }
}

