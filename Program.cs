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
using FileHelpers;

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
        public string DestId { get; set; }


    }
    [DelimitedRecord("\t")]
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
            // Check system name and set paths
            if (args == null || args.Length == 0)
            {
                args = SetPathBySystem();
            }



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

        /// <summary>
        /// Set project specific path when path or file is not passed in args. Base path on machine 
        /// </summary>
        private static string[] SetPathBySystem()
        {
            var _machine = Environment.MachineName;
            string[] _argpath = new string[1];
            Console.WriteLine("Setting path for MachineName: {0}", _machine);

            if (_machine == "EXPANSE") { _argpath[0] = @"C:\Users\Richa\OneDrive\1R\Hawaii\FHB_Testing\FHB"; }

            else if (_machine == "HHSCCONVM01" || _machine == "ECCONTROL3") { _argpath[0] = "C:\\Users\\rpearce\\Documents\\FHB"; }

            //TBD new server
            else if (_machine == "NewServerName") { _argpath[0] = "C:\\Users\\rpearce\\Documents\\FHB"; }

            return _argpath;

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

            // Remove subdirectory processing, Hold code for selective historical rebuilding
            // Recurse into subdirectories of this directory.
            //  //string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
            //  //foreach (string subdirectory in subdirectoryEntries)
            //  //ProcessDirectory(subdirectory);
        }


        public static void ProcessFile(string path)
        {
            // Collect & prep misc info and records for payment file and pre-loop work fields
            int[] countBoxAcctRows = new int[10];
            int lines = File.ReadAllLines(path).Length;
            string[] records = File.ReadAllLines(path);
            int i = 0; int cnt = 0; string newName = ""; string newXlsName = " "; 
            Snap($"........Processing....... {Path.GetFileName(path)}", 1, 0);
            List<FHB_BoxAccts> accts = BoxAccts();
            List<FHB_Payment> rcds = PmtList(records, accts);



            foreach (var FHB_BoxAccts in accts)
            {
                countBoxAcctRows[i] = File.ReadAllLines(path).Count(l => l.Contains(accts[i].BoxAndAcct));

                Snap($"BoxAndAcct {accts[i].BoxAndAcct} for {accts[i].Facility} has a count of:  {countBoxAcctRows[i]} lines (Index {i})", 1, 0);
                cnt = countBoxAcctRows[i];

                // If records found for Hospitals LockBox ID and Account number, create split file of transactions.
                if (cnt > 0)
                {
                    // Split and Write separate distribution file for Facility, in original format

                    newName = path.Replace(".txt", ("_" + accts[i].DestId + "_FHB_")) + accts[i].Facility + ".txt";
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
                        
                        Snap($"New file '{newName}' \n\t\t\twritten from inbound file {Path.GetFileName(path)} with {cnt} records, for Facility {accts[i].Facility}.", 1, 1);
                    }



                    // Generate payment report for facility in .xls (tab delimited) format
                    newXlsName = (Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path))) +
                                  "_FHB_Payments_" + ("_" + accts[i].DestId + "_") + accts[i].Facility + ".xls";
                   
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
                        xl.WriteLine("\t" + countBoxAcctRows[i] + " payment records written to: " + Path.GetFileName(newName) + "\n");
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
                        
                        Snap($"Report (tab delimited .xls file) '{newXlsName}' \n\t\t\twritten with total payments of {totalPmts} for Facility {accts[i].Facility} ", 0, 1);

                    }
                }

                i++;
            }
            int totalBoxCounts = countBoxAcctRows.Sum();
            Snap($"Processed file '{Path.GetFileName(path)}' with {lines} total lines, {totalBoxCounts} written to separate files, at {DateTime.Now}.", 1, 0);
            Snap(string.Concat(Enumerable.Repeat("_", 70)), 0, 1);

            WriteFullList(path, lines, rcds, totalBoxCounts); 
            AppendHistory(path, rcds);
            AppendAllPaymentsDump(path, rcds);
            File.Move(path, path + ".Processed");
        }

     

        private static List<FHB_BoxAccts> BoxAccts()
        {
            // Load First Hawaiian LockBox IDs and name strings for Hospitals
            string[] _boxAcctsIn = File.ReadAllLines(@"Logs_n_Data\FHB_Facility_BoxAccounts.csv");

            // Load to class list
            IEnumerable<FHB_BoxAccts> _fhbAccts =
            from fhbAcctLine in _boxAcctsIn.Skip(1)
            let splitName = fhbAcctLine.Split(',')
            select new FHB_BoxAccts()
            {
                BoxAndAcct = Convert.ToString(splitName[0]),
                Facility = splitName[1],
                Box = Convert.ToString(splitName[2]),
                HspAcct = Convert.ToString(splitName[3]),
                Hspnbr3 = Convert.ToInt32(splitName[4]),
                ID = Convert.ToInt32(splitName[5]),
                DestId = (splitName[6])
            };

            List<FHB_BoxAccts> acct = _fhbAccts.ToList();
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
            // Write (.xls tab delimited type) FHB_All_Payment_History.txt
            string histFileName = Path.Combine(Path.GetDirectoryName(path),"Logs_n_Data", "FHB_All_Payment_History.txt");
            using StreamWriter xh = File.AppendText(histFileName);
            xh.WriteLine("\n");
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
                   rcd.FhAcct + "\t" +
                   rcd.F1BoxAcct + "\t" +
                   Path.GetFileName(path) + "\t" +
                   DateTime.Now.ToString());

            }
            xh.Close();
            string _x = histFileName.Replace(".txt", "xls");
            File.Copy(histFileName, _x, true); 

            
        }

        private static void AppendAllPaymentsDump(string path, List<FHB_Payment> rcds)
        {
            // Write emulated CPWFHBP1 database. Append records to History File
            string newXlsName2 = (Path.Combine(Path.GetDirectoryName(path), "Logs_n_Data", Path.GetFileName(path).Substring(0, 12) + "_All_Payment_History_tab.txt"));
            FileHelperEngine<FHB_Payment> engine = new FileHelperEngine<FHB_Payment>();
            engine.AppendToFile(newXlsName2, rcds);
        }

        private static void WriteFullList(string path, int lines, List<FHB_Payment> rcds, int totalBoxCounts)
        {
            // Single File/Day with dated name, for analysis as needed.  All payments from todays file.
            decimal totalPmts = 0;
            string newXlsName = (Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path))) +
                                  "_Payment_list.xls";
            using StreamWriter xa = File.AppendText(newXlsName);
            // TITLES and Column Header
            xa.WriteLine("\tFIRST HAWAIIAN BANK LOCK BOX PAYMENTS, Full List of ALL Payments");
            xa.WriteLine("\tFile name " + Path.GetFileName(path) + " processed on " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString());
            xa.WriteLine("\t" + lines + " payment records received and " + totalBoxCounts + " written." + "\n");
            xa.WriteLine("\tHospital\tPayment Date\tAccount#\tPayment Amt\tAmount Due\tStatement Date\t" + "Admit/Other Date\tLockBox#\tFHBAcct\tBox&Acct\tOrigin File\tTimestamp");

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
                   rcd.FhBoxn + "\t" +
                   rcd.FhAcct + "\t" +
                   rcd.F1BoxAcct + "\t" +
                   Path.GetFileName(path) + "\t" +
                   DateTime.Now.ToString() 
                   );
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
            
            string logName = "LOG_Activity.txt";
            logName = @"Logs_n_Data\LOG_Activity.txt";
            using (StreamWriter lg = File.AppendText(logName))
            { lg.WriteLine($"{msg1} "); }
        }
    }
}

